using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using MoodPickup.Api.DTOs.Media;
using MoodPickup.Api.DTOs.Menu;
using MoodPickup.Api.DTOs.Menu.Admin;
using MoodPickup.Api.DTOs.Menu.Public;
using MoodPickup.Api.Infrastructure;
using SkiaSharp;

namespace MoodPickup.Api.Tests;

public sealed class PostgresMediaApiTests(PostgresMoodPickupApiFactory factory)
    : IClassFixture<PostgresMoodPickupApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    [PostgresFact]
    public async Task Upload_EnforcesAuthorizationMatrix()
    {
        await factory.ResetAsync();
        using var client = factory.CreateSecureClient();
        var png = CreateImage(SKEncodedImageFormat.Png);

        using var anonymous = await UploadAsync(
            client,
            null,
            png,
            "image/png",
            "anonymous.png");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        var customerToken = await factory.CreateCustomerTokenAsync();
        using var customer = await UploadAsync(
            client,
            customerToken,
            png,
            "image/png",
            "customer.png");
        Assert.Equal(HttpStatusCode.Forbidden, customer.StatusCode);

        var kitchenToken = await factory.CreateEmployeeTokenAsync(
            "media-kitchen",
            AuthenticationConstants.Roles.Kitchen);
        using var kitchen = await UploadAsync(
            client,
            kitchenToken,
            png,
            "image/png",
            "kitchen.png");
        Assert.Equal(HttpStatusCode.Forbidden, kitchen.StatusCode);

        var managerToken = await factory.CreateEmployeeTokenAsync(
            "media-manager",
            AuthenticationConstants.Roles.MenuManager);
        using var manager = await UploadAsync(
            client,
            managerToken,
            png,
            "image/png",
            "manager.png");
        Assert.Equal(HttpStatusCode.Created, manager.StatusCode);

        var administratorToken = await factory.GetAdministratorTokenAsync();
        using var administrator = await UploadAsync(
            client,
            administratorToken,
            png,
            "image/png",
            "administrator.png");
        Assert.Equal(HttpStatusCode.Created, administrator.StatusCode);
    }

    [PostgresFact]
    public async Task Upload_AcceptsJpegPngAndWebpAndPersistsSafeMetadata()
    {
        await factory.ResetAsync();
        using var client = factory.CreateSecureClient();
        var token = await factory.GetAdministratorTokenAsync();
        var cases = new[]
        {
            (SKEncodedImageFormat.Jpeg, "image/jpeg", @"..\..\cappuccino.jpg"),
            (SKEncodedImageFormat.Png, "image/png", "latte.png"),
            (SKEncodedImageFormat.Webp, "image/webp", "americano.webp")
        };

        foreach (var (format, contentType, fileName) in cases)
        {
            using var response = await UploadAsync(
                client,
                token,
                CreateImage(format),
                contentType,
                fileName);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var uploaded = await ReadAsync<MediaImageDto>(response);
            Assert.Equal(contentType, uploaded.ContentType);
            Assert.Equal(32, uploaded.Width);
            Assert.Equal(24, uploaded.Height);
            Assert.DoesNotContain("..", uploaded.OriginalFileName, StringComparison.Ordinal);
            Assert.DoesNotContain('\\', uploaded.OriginalFileName);
            Assert.DoesNotContain('/', uploaded.OriginalFileName);
            Assert.StartsWith("/media/", uploaded.Url, StringComparison.Ordinal);

            using var delivery = await client.GetAsync(uploaded.Url);
            Assert.True(
                delivery.StatusCode == HttpStatusCode.OK,
                $"Expected media delivery 200, received {(int)delivery.StatusCode}: " +
                await delivery.Content.ReadAsStringAsync());
            Assert.Equal(contentType, delivery.Content.Headers.ContentType?.MediaType);
            Assert.True((await delivery.Content.ReadAsByteArrayAsync()).Length > 0);
            Assert.Contains("immutable", delivery.Headers.CacheControl?.ToString());
        }

        var metadata = await factory.ReadDatabaseAsync(db => db.MediaFiles
            .AsNoTracking()
            .OrderBy(media => media.CreatedAt)
            .ToListAsync());
        Assert.Equal(3, metadata.Count);
        Assert.All(metadata, media =>
        {
            Assert.Equal("Local", media.StorageProvider);
            Assert.Matches(
                "^[a-f0-9]{2}/[a-f0-9]{2}/[a-f0-9]{32}\\.(jpg|png|webp)$",
                media.StorageKey);
            Assert.DoesNotContain(factory.MediaRootPath, media.StorageKey);
            Assert.NotNull(media.CreatedByEmployeeId);
            Assert.False(media.IsDeleted);
        });

        var auditCount = await factory.ReadDatabaseAsync(db =>
            db.EmployeeActionLogs.CountAsync(log =>
                log.ActionType == "MediaImageUploaded" &&
                log.EntityType == "MediaFile"));
        Assert.Equal(3, auditCount);
    }

    [PostgresFact]
    public async Task Upload_RejectsEmptyOversizedUnsupportedAndMismatchedFiles()
    {
        await factory.ResetAsync();
        using var client = factory.CreateSecureClient();
        var token = await factory.GetAdministratorTokenAsync();

        using var empty = await UploadAsync(
            client,
            token,
            [],
            "image/jpeg",
            "empty.jpg");
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);

        using var oversized = await UploadAsync(
            client,
            token,
            new byte[131073],
            "image/jpeg",
            "large.jpg");
        Assert.Equal(HttpStatusCode.BadRequest, oversized.StatusCode);

        using var executable = await UploadAsync(
            client,
            token,
            Encoding.ASCII.GetBytes("MZ executable content"),
            "image/jpeg",
            "renamed.jpg");
        Assert.Equal(HttpStatusCode.BadRequest, executable.StatusCode);
        Assert.Equal("UNSUPPORTED_IMAGE_FORMAT", await ProblemCodeAsync(executable));

        using var fakeMime = await UploadAsync(
            client,
            token,
            CreateImage(SKEncodedImageFormat.Png),
            "image/jpeg",
            "fake.jpg");
        Assert.Equal(HttpStatusCode.BadRequest, fakeMime.StatusCode);
        Assert.Equal("UNSUPPORTED_IMAGE_FORMAT", await ProblemCodeAsync(fakeMime));

        using var unsupported = await UploadAsync(
            client,
            token,
            Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"/>"),
            "image/svg+xml",
            "vector.svg");
        Assert.Equal(HttpStatusCode.BadRequest, unsupported.StatusCode);

        Assert.Empty(
            Directory.Exists(factory.MediaRootPath)
                ? Directory.EnumerateFiles(
                    factory.MediaRootPath,
                    "*",
                    SearchOption.AllDirectories)
                : []);
        Assert.Equal(
            0,
            await factory.ReadDatabaseAsync(db => db.MediaFiles.CountAsync()));
    }

    [PostgresFact]
    public async Task Upload_RemovesPhysicalFileWhenAuditOrMetadataUnitFails()
    {
        await factory.ResetAsync();
        using var client = factory.CreateSecureClient();
        var token = await factory.GetAdministratorTokenAsync();
        factory.FailAuditWrites = true;

        using var response = await UploadAsync(
            client,
            token,
            CreateImage(SKEncodedImageFormat.Jpeg),
            "image/jpeg",
            "rollback.jpg");
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        factory.FailAuditWrites = false;

        Assert.Empty(
            Directory.Exists(factory.MediaRootPath)
                ? Directory.EnumerateFiles(
                    factory.MediaRootPath,
                    "*",
                    SearchOption.AllDirectories)
                : []);
        Assert.Equal(
            0,
            await factory.ReadDatabaseAsync(db => db.MediaFiles.CountAsync()));
    }

    [PostgresFact]
    public async Task Delivery_ReturnsNotFoundForMissingOrTraversalKeys()
    {
        await factory.ResetAsync();
        using var client = factory.CreateSecureClient();

        using var missing = await client.GetAsync(
            "/media/aa/bb/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.jpg");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        using var traversal = await client.GetAsync("/media/%2e%2e/appsettings.json");
        Assert.True(
            traversal.StatusCode == HttpStatusCode.NotFound,
            $"Expected traversal delivery 404, received {(int)traversal.StatusCode}: " +
            await traversal.Content.ReadAsStringAsync());
    }

    [PostgresFact]
    public async Task ProductImageAssignment_ProducesRetrievablePublicUrls()
    {
        await factory.ResetAsync();
        using var client = factory.CreateSecureClient();
        var token = await factory.GetAdministratorTokenAsync();

        using var uploadResponse = await UploadAsync(
            client,
            token,
            CreateImage(SKEncodedImageFormat.Webp),
            "image/webp",
            "public-product.webp");
        var image = await ReadAsync<MediaImageDto>(uploadResponse);
        var product = await GetAdminProductByNameAsync(
            client,
            token,
            "Cappuccino");

        using var assignResponse = await SendAuthorizedJsonAsync(
            client,
            HttpMethod.Put,
            $"/api/v1/admin/products/{product.Id}/image",
            token,
            new AssignProductImageRequest(image.Id, product.RowVersion));
        Assert.Equal(HttpStatusCode.OK, assignResponse.StatusCode);
        var assigned =
            await ReadAsync<MenuMutationResponse<AdminProductDto>>(assignResponse);
        Assert.Equal(image.Url, assigned.Resource.Image?.Url);

        using var publicDetailResponse = await client.GetAsync(
            $"/api/v1/products/{product.Id}");
        var publicDetail =
            await ReadAsync<PublicProductDetailDto>(publicDetailResponse);
        Assert.Equal(image.Url, publicDetail.ImageUrl);

        var publicList = await client.GetFromJsonAsync<
            PagedResponse<PublicProductListItemDto>>(
            $"/api/v1/products?search={Uri.EscapeDataString(product.Name)}");
        Assert.Equal(image.Url, Assert.Single(publicList!.Items).ImageUrl);

        using var delivery = await client.GetAsync(image.Url);
        Assert.True(
            delivery.StatusCode == HttpStatusCode.OK,
            $"Expected media delivery 200, received {(int)delivery.StatusCode}: " +
            await delivery.Content.ReadAsStringAsync());
        Assert.Equal("image/webp", delivery.Content.Headers.ContentType?.MediaType);
    }

    private static byte[] CreateImage(
        SKEncodedImageFormat format,
        int width = 32,
        int height = 24)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(SKColors.SaddleBrown);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(
            format,
            format == SKEncodedImageFormat.Png ? 100 : 90);
        return data.ToArray();
    }

    private static async Task<HttpResponseMessage> UploadAsync(
        HttpClient client,
        string? token,
        byte[] content,
        string contentType,
        string fileName)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/v1/admin/media/images");
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        }

        var multipart = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(content);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        multipart.Add(fileContent, "file", fileName);
        request.Content = multipart;
        return await client.SendAsync(request);
    }

    private static async Task<AdminProductDto> GetAdminProductByNameAsync(
        HttpClient client,
        string token,
        string name)
    {
        using var listResponse = await SendAuthorizedAsync(
            client,
            HttpMethod.Get,
            $"/api/v1/admin/products?includeDeleted=true&search={name}",
            token);
        var list = await ReadAsync<PagedResponse<AdminProductListItemDto>>(
            listResponse);
        var item = Assert.Single(list.Items);
        using var detailResponse = await SendAuthorizedAsync(
            client,
            HttpMethod.Get,
            $"/api/v1/admin/products/{item.Id}",
            token);
        return await ReadAsync<AdminProductDto>(detailResponse);
    }

    private static async Task<HttpResponseMessage> SendAuthorizedAsync(
        HttpClient client,
        HttpMethod method,
        string path,
        string token)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> SendAuthorizedJsonAsync<T>(
        HttpClient client,
        HttpMethod method,
        string path,
        string token,
        T body)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(body);
        return await client.SendAsync(request);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        var raw = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(raw, JsonOptions)
               ?? throw new InvalidOperationException(
                   $"Response did not contain {typeof(T).Name}: {raw}");
    }

    private static async Task<string?> ProblemCodeAsync(
        HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());
        return document.RootElement.TryGetProperty("code", out var code)
            ? code.GetString()
            : null;
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
