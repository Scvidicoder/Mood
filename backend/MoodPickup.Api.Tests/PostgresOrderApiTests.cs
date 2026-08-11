using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MoodPickup.Api.Data;
using MoodPickup.Api.DTOs.Menu;
using MoodPickup.Api.DTOs.Orders;
using MoodPickup.Api.Entities;
using MoodPickup.Api.Interfaces;
using MoodPickup.Api.Options;
using MoodPickup.Api.Services;

namespace MoodPickup.Api.Tests;

public sealed class PostgresOrderApiTests(PostgresMoodPickupApiFactory factory)
    : IClassFixture<PostgresMoodPickupApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    [PostgresFact]
    public async Task Checkout_ServiceUsesPostgreSqlTransactionAndDailySequence()
    {
        await factory.ResetAsync();
        var request = await CreateCappuccinoRequestAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MoodPickupDbContext>();
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            Name = "Direct checkout customer",
            PhoneNumber = "+992900000099"
        };
        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync();
        var service = new OrderService(
            dbContext,
            new CustomerContext(customer.Id),
            new MenuConfigurationValidator(),
            scope.ServiceProvider.GetRequiredService<IOptionsMonitor<CheckoutOptions>>(),
            factory.TimeProvider);

        var created = await service.CreateAsync(request, CancellationToken.None);

        Assert.EndsWith("-00001", created.OrderNumber);
        Assert.Equal(48m, created.Total);
    }

    [PostgresFact]
    public async Task Checkout_CreatesSnapshotsAndLimitsRetrievalToTheOwner()
    {
        await factory.ResetAsync();
        using var client = factory.CreateSecureClient();
        var request = await CreateCappuccinoRequestAsync();

        using var anonymous = await client.PostAsJsonAsync("/api/v1/orders", request);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        var ownerToken = await factory.CreateCustomerTokenAsync();
        var created = await CreateOrderAsync(client, ownerToken, request);
        Assert.StartsWith("MP-", created.OrderNumber);
        Assert.Equal(OrderStatus.PendingConfirmation, created.Status);
        Assert.Equal(48m, created.Total);
        var item = Assert.Single(created.Items);
        Assert.Equal("Cappuccino", item.ProductName);
        Assert.True(item.IsAvailableAtPurchase);
        Assert.Equal(22m, item.BasePrice);
        Assert.Equal(24m, item.FinalPrice);
        Assert.Equal(2, item.Quantity);
        Assert.Equal("Size", Assert.Single(item.Options).OptionGroupName);

        await factory.ReadDatabaseAsync(async db =>
        {
            var product = await db.Products.SingleAsync(item => item.Id == request.Items[0].ProductId);
            product.Name = "Changed after checkout";
            product.BasePrice = 99m;
            var option = await db.ProductOptionValues.SingleAsync(
                item => item.OptionValueId == request.Items[0].OptionValueIds[0]);
            option.PriceModifier = 11m;
            await db.SaveChangesAsync();
            return true;
        });

        using var detailResponse = await SendAuthorizedAsync(
            client,
            HttpMethod.Get,
            $"/api/v1/orders/{created.Id}",
            ownerToken);
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        var detail = await ReadAsync<OrderDetailDto>(detailResponse);
        var historicalItem = Assert.Single(detail.Items);
        Assert.Equal("Cappuccino", historicalItem.ProductName);
        Assert.True(historicalItem.IsAvailableAtPurchase);
        Assert.Equal(22m, historicalItem.BasePrice);
        Assert.Equal(24m, historicalItem.FinalPrice);
        Assert.Equal(2m, Assert.Single(historicalItem.Options).PriceModifier);

        using var mineResponse = await SendAuthorizedAsync(
            client,
            HttpMethod.Get,
            "/api/v1/orders/mine?page=1&pageSize=20",
            ownerToken);
        Assert.Equal(HttpStatusCode.OK, mineResponse.StatusCode);
        var mine = await ReadAsync<PagedResponse<OrderSummaryDto>>(mineResponse);
        Assert.Equal(created.Id, Assert.Single(mine.Items).Id);

        var otherToken = await factory.CreateCustomerTokenAsync();
        using var otherResponse = await SendAuthorizedAsync(
            client,
            HttpMethod.Get,
            $"/api/v1/orders/{created.Id}",
            otherToken);
        Assert.Equal(HttpStatusCode.NotFound, otherResponse.StatusCode);
    }

    [PostgresFact]
    public async Task Checkout_ValidationFailureLeavesNoPartialOrderOrSequence()
    {
        await factory.ResetAsync();
        using var client = factory.CreateSecureClient();
        var valid = await CreateCappuccinoRequestAsync();
        var token = await factory.CreateCustomerTokenAsync();
        var invalid = valid with
        {
            Items =
            [new CreateOrderItemRequest(valid.Items[0].ProductId, [], 1, null)]
        };

        using var invalidResponse = await SendAuthorizedJsonAsync(
            client,
            HttpMethod.Post,
            "/api/v1/orders",
            token,
            invalid);
        Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);

        var countsAfterFailure = await factory.ReadDatabaseAsync(async db =>
            (await db.Orders.CountAsync(), await db.OrderDailySequences.CountAsync()));
        Assert.Equal((0, 0), countsAfterFailure);

        var created = await CreateOrderAsync(client, token, valid);
        Assert.EndsWith("-00001", created.OrderNumber);
        var countsAfterSuccess = await factory.ReadDatabaseAsync(async db =>
            (await db.Orders.CountAsync(), await db.OrderDailySequences.CountAsync()));
        Assert.Equal((1, 1), countsAfterSuccess);
    }

    [PostgresFact]
    public async Task Profile_UpdateUsesOwnershipValidationAndOptimisticConcurrency()
    {
        await factory.ResetAsync();
        using var client = factory.CreateSecureClient();

        using var anonymous = await client.GetAsync("/api/v1/profile");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        var token = await factory.CreateCustomerTokenAsync();
        using var getResponse = await SendAuthorizedAsync(
            client,
            HttpMethod.Get,
            "/api/v1/profile",
            token);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var original = await ReadAsync<MoodPickup.Api.DTOs.CustomerProfileDto>(getResponse);
        Assert.True(original.PhoneVerified);

        var update = new MoodPickup.Api.DTOs.UpdateCustomerProfileRequest(
            "  Updated Customer  ",
            original.RowVersion);
        using var updateResponse = await SendAuthorizedJsonAsync(
            client,
            HttpMethod.Put,
            "/api/v1/profile",
            token,
            update);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await ReadAsync<MoodPickup.Api.DTOs.CustomerProfileDto>(updateResponse);
        Assert.Equal("Updated Customer", updated.Name);
        Assert.NotEqual(original.RowVersion, updated.RowVersion);

        using var staleResponse = await SendAuthorizedJsonAsync(
            client,
            HttpMethod.Put,
            "/api/v1/profile",
            token,
            update with { Name = "Stale name" });
        Assert.Equal(HttpStatusCode.Conflict, staleResponse.StatusCode);
        using var problem = JsonDocument.Parse(await staleResponse.Content.ReadAsStringAsync());
        Assert.Equal(
            "PROFILE_VERSION_CONFLICT",
            problem.RootElement.GetProperty("code").GetString());
    }

    [PostgresFact]
    public async Task CustomerHistoryFiltersSearchesAndRepeatStaysOwnerScoped()
    {
        await factory.ResetAsync();
        using var client = factory.CreateSecureClient();
        var request = await CreateCappuccinoRequestAsync();
        var ownerToken = await factory.CreateCustomerTokenAsync();
        var created = await CreateOrderAsync(client, ownerToken, request);
        await factory.ReadDatabaseAsync(async db =>
        {
            var order = await db.Orders.SingleAsync(item => item.Id == created.Id);
            order.Status = OrderStatus.Completed;
            order.CompletedAt = factory.TimeProvider.GetUtcNow();
            await db.SaveChangesAsync();
            return true;
        });

        using var filteredResponse = await SendAuthorizedAsync(
            client,
            HttpMethod.Get,
            "/api/v1/orders/mine?filter=Completed&search=cappuccino&page=1&pageSize=10",
            ownerToken);
        Assert.Equal(HttpStatusCode.OK, filteredResponse.StatusCode);
        var filtered = await ReadAsync<PagedResponse<OrderSummaryDto>>(filteredResponse);
        Assert.Equal(created.Id, Assert.Single(filtered.Items).Id);

        using var repeatResponse = await SendAuthorizedAsync(
            client,
            HttpMethod.Post,
            $"/api/v1/orders/{created.Id}/repeat",
            ownerToken);
        Assert.Equal(HttpStatusCode.OK, repeatResponse.StatusCode);
        var repeat = await ReadAsync<RepeatOrderResultDto>(repeatResponse);
        Assert.Single(repeat.AvailableItems);
        Assert.Empty(repeat.UnavailableItems);

        var otherToken = await factory.CreateCustomerTokenAsync();
        using var otherResponse = await SendAuthorizedAsync(
            client,
            HttpMethod.Post,
            $"/api/v1/orders/{created.Id}/repeat",
            otherToken);
        Assert.Equal(HttpStatusCode.NotFound, otherResponse.StatusCode);
    }

    private async Task<CreateOrderRequest> CreateCappuccinoRequestAsync()
    {
        var data = await factory.ReadDatabaseAsync(async db =>
        {
            var product = await db.Products
                .Include(item => item.OptionGroups)
                    .ThenInclude(item => item.OptionGroup)
                .Include(item => item.OptionGroups)
                    .ThenInclude(item => item.Values)
                        .ThenInclude(item => item.OptionValue)
                .SingleAsync(item => item.Name == "Cappuccino");
            return new
            {
                product.Id,
                SmallId = product.OptionGroups
                    .Single(group => group.OptionGroup.Name == "Size")
                    .Values.Single(value => value.OptionValue.Name == "Small")
                    .OptionValueId
            };
        });
        return new CreateOrderRequest(
            [new CreateOrderItemRequest(data.Id, [data.SmallId], 2, null)],
            "Integration test checkout",
            PaymentMethod.PayOnPickup,
            PickupMode.AsSoonAsPossible,
            null);
    }

    private static async Task<OrderDetailDto> CreateOrderAsync(
        HttpClient client,
        string token,
        CreateOrderRequest request)
    {
        using var response = await SendAuthorizedJsonAsync(
            client,
            HttpMethod.Post,
            "/api/v1/orders",
            token,
            request);
        Assert.True(
            response.StatusCode == HttpStatusCode.Created,
            await response.Content.ReadAsStringAsync());
        return await ReadAsync<OrderDetailDto>(response);
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
        request.Content = JsonContent.Create(body, options: JsonOptions);
        return await client.SendAsync(request);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        var raw = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(raw, JsonOptions)
               ?? throw new InvalidOperationException(
                   $"Response did not contain {typeof(T).Name}: {raw}");
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private sealed class CustomerContext(Guid customerId) : ICurrentUserContext
    {
        public string CorrelationId => "postgres-order-test";

        public Guid GetRequiredCustomerId() => customerId;

        public Guid GetRequiredEmployeeId() => throw new NotSupportedException();
    }
}
