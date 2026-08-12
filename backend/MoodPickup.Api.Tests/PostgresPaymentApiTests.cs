using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using MoodPickup.Api.DTOs.Orders;
using MoodPickup.Api.DTOs.Payments;
using MoodPickup.Api.Entities;

namespace MoodPickup.Api.Tests;

public sealed class PostgresPaymentApiTests(PostgresMoodPickupApiFactory factory)
    : IClassFixture<PostgresMoodPickupApiFactory>
{
    private const string Key = "44444444";
    private const string Password = "cztef62wrwcysyubbbdnhlk1rs2cztfsqgwww7j0";
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    [PostgresFact]
    public async Task OnlineCheckout_PersistsPendingPaymentWithoutTreatingItAsPaid()
    {
        await factory.ResetAsync();
        var token = await factory.CreateCustomerTokenAsync();
        var request = await CreateOnlineOrderRequestAsync();
        using var client = factory.CreateSecureClient();

        using var response = await SendAuthorizedJsonAsync(
            client,
            HttpMethod.Post,
            "/api/v1/orders",
            token,
            request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var order = await ReadAsync<OrderDetailDto>(response);
        Assert.False(order.PaymentReceived);
        Assert.NotNull(order.Payment);
        Assert.Equal(PaymentStatus.Pending, order.Payment.Status);
        Assert.NotNull(order.PaymentLaunch);
        Assert.Equal(order.Payment.Id, order.PaymentLaunch.PaymentId);
        Assert.Equal("POST", order.PaymentLaunch.Method);
        Assert.Equal("https://test-web.alif.tj/", order.PaymentLaunch.ActionUrl);
        Assert.DoesNotContain(
            order.PaymentLaunch.FormFields,
            item => item.Key.Contains("password", StringComparison.OrdinalIgnoreCase) ||
                    item.Value == Password);

        var stored = await factory.ReadDatabaseAsync(async db => new
        {
            Payment = await db.Payments.SingleAsync(item => item.OrderId == order.Id),
            Attempt = await db.PaymentAttempts.SingleAsync(),
            Audits = await db.EmployeeActionLogs
                .Where(item => item.EntityId == order.Payment.Id)
                .ToListAsync()
        });
        Assert.Equal(PaymentStatus.Pending, stored.Payment.Status);
        Assert.Equal(1, stored.Attempt.AttemptNumber);
        Assert.Null(stored.Attempt.ResponseSnapshot);
        Assert.Equal("PaymentCreated", Assert.Single(stored.Audits).ActionType);
        Assert.Null(Assert.Single(stored.Audits).EmployeeId);
    }

    [PostgresFact]
    public async Task Callback_IsAnonymousValidatedIdempotentAndOwnerScoped()
    {
        await factory.ResetAsync();
        var ownerToken = await factory.CreateCustomerTokenAsync();
        var otherToken = await factory.CreateCustomerTokenAsync();
        using var client = factory.CreateSecureClient();
        var order = await CreateOnlineOrderAsync(client, ownerToken);
        var payment = order.Payment!;
        var providerOrderId = order.PaymentLaunch!.FormFields["orderId"];
        var callback = new AlifCallbackRequest
        {
            OrderId = providerOrderId,
            TransactionId = "92938922",
            Status = "ok",
            Token = ResponseToken(providerOrderId, "ok", "92938922"),
            Amount = payment.Amount,
            Account = "5058***ALF**0104",
            TransactionType = "korti_milli"
        };

        using var first = await client.PostAsJsonAsync(
            "/api/v1/payments/alif/callback",
            callback,
            JsonOptions);
        using var duplicate = await client.PostAsJsonAsync(
            "/api/v1/payments/alif/callback",
            callback,
            JsonOptions);
        using var owner = await SendAuthorizedAsync(
            client,
            HttpMethod.Get,
            $"/api/v1/payments/{payment.Id}",
            ownerToken);
        using var other = await SendAuthorizedAsync(
            client,
            HttpMethod.Get,
            $"/api/v1/payments/{payment.Id}",
            otherToken);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(new PaymentCallbackResult(true, false), await ReadAsync<PaymentCallbackResult>(first));
        Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);
        Assert.Equal(new PaymentCallbackResult(false, true), await ReadAsync<PaymentCallbackResult>(duplicate));
        Assert.Equal(HttpStatusCode.OK, owner.StatusCode);
        Assert.Equal(PaymentStatus.Paid, (await ReadAsync<CustomerPaymentDto>(owner)).Status);
        Assert.Equal(HttpStatusCode.NotFound, other.StatusCode);

        var counts = await factory.ReadDatabaseAsync(async db => new
        {
            Webhooks = await db.PaymentWebhookEvents.CountAsync(),
            PaidPayments = await db.Payments.CountAsync(item => item.Status == PaymentStatus.Paid),
            SystemAudits = await db.EmployeeActionLogs.CountAsync(item =>
                item.EntityId == payment.Id && item.EmployeeId == null)
        });
        Assert.Equal(1, counts.Webhooks);
        Assert.Equal(1, counts.PaidPayments);
        Assert.Equal(3, counts.SystemAudits);
    }

    [PostgresFact]
    public async Task InvalidCallback_DoesNotMutatePayment()
    {
        await factory.ResetAsync();
        var token = await factory.CreateCustomerTokenAsync();
        using var client = factory.CreateSecureClient();
        var order = await CreateOnlineOrderAsync(client, token);
        var providerOrderId = order.PaymentLaunch!.FormFields["orderId"];

        using var response = await client.PostAsJsonAsync(
            "/api/v1/payments/alif/callback",
            new AlifCallbackRequest
            {
                OrderId = providerOrderId,
                TransactionId = "92938922",
                Status = "ok",
                Token = new string('0', 64),
                Amount = order.Total,
                Account = "account"
            },
            JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var stored = await factory.ReadDatabaseAsync(async db => new
        {
            Status = await db.Payments.Select(item => item.Status).SingleAsync(),
            Webhooks = await db.PaymentWebhookEvents.CountAsync()
        });
        Assert.Equal(PaymentStatus.Pending, stored.Status);
        Assert.Equal(0, stored.Webhooks);
    }

    private async Task<OrderDetailDto> CreateOnlineOrderAsync(
        HttpClient client,
        string token)
    {
        using var response = await SendAuthorizedJsonAsync(
            client,
            HttpMethod.Post,
            "/api/v1/orders",
            token,
            await CreateOnlineOrderRequestAsync());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadAsync<OrderDetailDto>(response);
    }

    private async Task<CreateOrderRequest> CreateOnlineOrderRequestAsync()
    {
        var item = await factory.ReadDatabaseAsync(async db =>
        {
            var product = await db.Products
                .Include(value => value.OptionGroups)
                    .ThenInclude(value => value.OptionGroup)
                .Include(value => value.OptionGroups)
                    .ThenInclude(value => value.Values)
                        .ThenInclude(value => value.OptionValue)
                .SingleAsync(value => value.Name == "Cappuccino");
            var option = product.OptionGroups
                .Single(value => value.OptionGroup.Name == "Size")
                .Values.Single(value => value.OptionValue.Name == "Small");
            return new { product.Id, option.OptionValueId };
        });
        return new CreateOrderRequest(
            [new CreateOrderItemRequest(item.Id, [item.OptionValueId], 1, null)],
            null,
            PaymentMethod.Online,
            PickupMode.AsSoonAsPossible,
            null);
    }

    private static string ResponseToken(
        string orderId,
        string status,
        string transactionId)
    {
        var derivedPassword = HmacHex(Key, Password);
        return HmacHex(derivedPassword, orderId + status + transactionId);
    }

    private static string HmacHex(string key, string data) =>
        Convert.ToHexString(HMACSHA256.HashData(
                Encoding.UTF8.GetBytes(key),
                Encoding.UTF8.GetBytes(data)))
            .ToLowerInvariant();

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
}
