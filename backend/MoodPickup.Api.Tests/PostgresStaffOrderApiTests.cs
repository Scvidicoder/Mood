using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using MoodPickup.Api.DTOs.Menu;
using MoodPickup.Api.DTOs.Orders;
using MoodPickup.Api.Entities;
using MoodPickup.Api.Infrastructure;

namespace MoodPickup.Api.Tests;

public sealed class PostgresStaffOrderApiTests(PostgresMoodPickupApiFactory factory)
    : IClassFixture<PostgresMoodPickupApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private static readonly DateTimeOffset Now =
        new(2026, 8, 11, 5, 0, 0, TimeSpan.Zero);

    [PostgresFact]
    public async Task StaffOrders_RequireAnAuthorizedEmployeeRole()
    {
        await factory.ResetAsync();
        var customerToken = await factory.CreateCustomerTokenAsync();
        var order = await CreatePendingOrderAsync(customerToken);
        var menuManagerToken = await factory.CreateEmployeeTokenAsync(
            "menu-only",
            AuthenticationConstants.Roles.MenuManager);
        var cashierToken = await factory.CreateEmployeeTokenAsync(
            "cashier",
            AuthenticationConstants.Roles.Cashier);
        factory.TimeProvider.SetUtcNow(Now);
        using var client = factory.CreateSecureClient();

        using var anonymous = await client.GetAsync("/api/v1/staff/orders");
        using var customer = await SendAuthorizedAsync(
            client,
            HttpMethod.Get,
            "/api/v1/staff/orders",
            customerToken);
        using var menuManager = await SendAuthorizedAsync(
            client,
            HttpMethod.Get,
            "/api/v1/staff/orders",
            menuManagerToken);
        using var cashier = await SendAuthorizedAsync(
            client,
            HttpMethod.Get,
            "/api/v1/staff/orders",
            cashierToken);

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, customer.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, menuManager.StatusCode);
        Assert.Equal(HttpStatusCode.OK, cashier.StatusCode);
        var page = await ReadAsync<PagedResponse<StaffOrderSummaryDto>>(cashier);
        Assert.Equal(order.Id, Assert.Single(page.Items).Id);
    }

    [PostgresFact]
    public async Task Confirmation_IsAuditedCustomerVisibleKitchenVisibleAndConcurrent()
    {
        await factory.ResetAsync();
        var customerToken = await factory.CreateCustomerTokenAsync();
        var order = await CreatePendingOrderAsync(customerToken);
        var cashierToken = await factory.CreateEmployeeTokenAsync(
            "confirm-cashier",
            AuthenticationConstants.Roles.Cashier);
        var kitchenToken = await factory.CreateEmployeeTokenAsync(
            "kitchen",
            AuthenticationConstants.Roles.Kitchen);
        factory.TimeProvider.SetUtcNow(Now);
        using var client = factory.CreateSecureClient();
        var request = new ConfirmOrderRequest(Now.AddMinutes(45), order.RowVersion);

        using var response = await SendAuthorizedJsonAsync(
            client,
            HttpMethod.Post,
            $"/api/v1/staff/orders/{order.Id}/confirm",
            cashierToken,
            request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var confirmed = await ReadAsync<StaffOrderDetailDto>(response);
        Assert.Equal(OrderStatus.Confirmed, confirmed.Status);
        Assert.Equal(Now.AddMinutes(45), confirmed.EstimatedReadyAt);

        using var customerResponse = await SendAuthorizedAsync(
            client,
            HttpMethod.Get,
            $"/api/v1/orders/{order.Id}",
            customerToken);
        var customerOrder = await ReadAsync<OrderDetailDto>(customerResponse);
        Assert.Equal(OrderStatus.Confirmed, customerOrder.Status);
        Assert.Equal(Now.AddMinutes(45), customerOrder.EstimatedReadyAt);
        var customerJson = await customerResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain("confirmedByEmployee", customerJson, StringComparison.OrdinalIgnoreCase);

        using var kitchenResponse = await SendAuthorizedAsync(
            client,
            HttpMethod.Get,
            "/api/v1/staff/kitchen/orders",
            kitchenToken);
        Assert.Equal(HttpStatusCode.OK, kitchenResponse.StatusCode);
        var kitchenOrders = await ReadAsync<PagedResponse<StaffOrderSummaryDto>>(kitchenResponse);
        Assert.Equal(order.Id, Assert.Single(kitchenOrders.Items).Id);

        var stored = await factory.ReadDatabaseAsync(async db => new
        {
            Order = await db.Orders.SingleAsync(item => item.Id == order.Id),
            Audits = await db.EmployeeActionLogs
                .Where(item => item.EntityId == order.Id)
                .ToListAsync()
        });
        Assert.NotNull(stored.Order.ConfirmedByEmployeeId);
        Assert.Equal(Now, stored.Order.ConfirmedAt);
        Assert.Equal("OrderConfirmed", Assert.Single(stored.Audits).ActionType);

        using var doubleConfirmation = await SendAuthorizedJsonAsync(
            client,
            HttpMethod.Post,
            $"/api/v1/staff/orders/{order.Id}/confirm",
            cashierToken,
            request);
        Assert.Equal(HttpStatusCode.Conflict, doubleConfirmation.StatusCode);
        Assert.Equal("ORDER_VERSION_CONFLICT", await ReadProblemCodeAsync(doubleConfirmation));

        using var lateCancellation = await SendAuthorizedJsonAsync(
            client,
            HttpMethod.Post,
            $"/api/v1/orders/{order.Id}/cancel",
            customerToken,
            body: new { });
        Assert.Equal(HttpStatusCode.Conflict, lateCancellation.StatusCode);
        Assert.Equal("ORDER_CANNOT_BE_CANCELLED", await ReadProblemCodeAsync(lateCancellation));
    }

    [PostgresFact]
    public async Task Rejection_IsAuditedAndReturnedToTheCustomer()
    {
        await factory.ResetAsync();
        var customerToken = await factory.CreateCustomerTokenAsync();
        var order = await CreatePendingOrderAsync(customerToken);
        var managerToken = await factory.CreateEmployeeTokenAsync(
            "manager",
            AuthenticationConstants.Roles.Manager);
        factory.TimeProvider.SetUtcNow(Now);
        using var client = factory.CreateSecureClient();

        using var response = await SendAuthorizedJsonAsync(
            client,
            HttpMethod.Post,
            $"/api/v1/staff/orders/{order.Id}/reject",
            managerToken,
            new RejectOrderRequest("Kitchen capacity is full.", order.RowVersion));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var rejected = await ReadAsync<StaffOrderDetailDto>(response);
        Assert.Equal(OrderStatus.Rejected, rejected.Status);
        Assert.Equal("Kitchen capacity is full.", rejected.RejectReason);

        using var customerResponse = await SendAuthorizedAsync(
            client,
            HttpMethod.Get,
            $"/api/v1/orders/{order.Id}",
            customerToken);
        var customerOrder = await ReadAsync<OrderDetailDto>(customerResponse);
        Assert.Equal(OrderStatus.Rejected, customerOrder.Status);
        Assert.Equal("Kitchen capacity is full.", customerOrder.RejectReason);

        var audit = await factory.ReadDatabaseAsync(db => db.EmployeeActionLogs
            .SingleAsync(item => item.EntityId == order.Id));
        Assert.Equal("OrderRejected", audit.ActionType);
        Assert.Contains("Kitchen capacity is full.", audit.NewValuesJson, StringComparison.Ordinal);
    }

    private async Task<Order> CreatePendingOrderAsync(string customerToken)
    {
        var customerId = Guid.Parse(new JwtSecurityTokenHandler()
            .ReadJwtToken(customerToken)
            .Claims.Single(claim => claim.Type == JwtRegisteredClaimNames.Sub)
            .Value);
        return await factory.ReadDatabaseAsync(async db =>
        {
            var customer = await db.Customers.SingleAsync(item => item.Id == customerId);
            var order = new Order
            {
                Id = Guid.NewGuid(),
                CustomerId = customer.Id,
                OrderNumber = $"MP-20260811-{Random.Shared.Next(1, 99999):D5}",
                Status = OrderStatus.PendingConfirmation,
                PaymentMethod = PaymentMethod.PayOnPickup,
                PickupMode = PickupMode.AsSoonAsPossible,
                CustomerName = customer.Name,
                CustomerPhoneNumber = customer.PhoneNumber,
                Comment = "Integration order",
                Subtotal = 20m,
                Total = 20m,
                Currency = "TJS"
            };
            db.Orders.Add(order);
            await db.SaveChangesAsync();
            return order;
        });
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

    private static async Task<string?> ReadProblemCodeAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
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
