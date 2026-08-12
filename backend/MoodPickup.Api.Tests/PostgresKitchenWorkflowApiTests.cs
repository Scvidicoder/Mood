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

public sealed class PostgresKitchenWorkflowApiTests(PostgresMoodPickupApiFactory factory)
    : IClassFixture<PostgresMoodPickupApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private static readonly DateTimeOffset Now =
        new(2026, 8, 11, 5, 0, 0, TimeSpan.Zero);

    [PostgresFact]
    public async Task KitchenDashboard_EnforcesViewAndActionRoles()
    {
        await factory.ResetAsync();
        var customerToken = await factory.CreateCustomerTokenAsync();
        var order = await CreateConfirmedOrderAsync(customerToken);
        var menuManagerToken = await factory.CreateEmployeeTokenAsync(
            "kitchen-menu-manager",
            AuthenticationConstants.Roles.MenuManager);
        var cashierToken = await factory.CreateEmployeeTokenAsync(
            "kitchen-cashier",
            AuthenticationConstants.Roles.Cashier);
        var managerToken = await factory.CreateEmployeeTokenAsync(
            "kitchen-manager",
            AuthenticationConstants.Roles.Manager);
        var kitchenToken = await factory.CreateEmployeeTokenAsync(
            "kitchen-worker",
            AuthenticationConstants.Roles.Kitchen);
        factory.TimeProvider.SetUtcNow(Now);
        using var client = factory.CreateSecureClient();

        using var anonymous = await client.GetAsync("/api/v1/staff/kitchen/orders");
        using var customer = await SendAuthorizedAsync(
            client,
            HttpMethod.Get,
            "/api/v1/staff/kitchen/orders",
            customerToken);
        using var menuManager = await SendAuthorizedAsync(
            client,
            HttpMethod.Get,
            "/api/v1/staff/kitchen/orders",
            menuManagerToken);
        using var cashier = await SendAuthorizedAsync(
            client,
            HttpMethod.Get,
            "/api/v1/staff/kitchen/orders",
            cashierToken);
        using var manager = await SendAuthorizedAsync(
            client,
            HttpMethod.Get,
            "/api/v1/staff/kitchen/orders",
            managerToken);
        using var cashierStart = await SendAuthorizedJsonAsync(
            client,
            HttpMethod.Post,
            $"/api/v1/staff/kitchen/{order.Id}/start",
            cashierToken,
            new OrderVersionRequest(order.RowVersion));
        using var kitchenStart = await SendAuthorizedJsonAsync(
            client,
            HttpMethod.Post,
            $"/api/v1/staff/kitchen/{order.Id}/start",
            kitchenToken,
            new OrderVersionRequest(order.RowVersion));

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, customer.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, menuManager.StatusCode);
        Assert.Equal(HttpStatusCode.OK, cashier.StatusCode);
        Assert.Equal(HttpStatusCode.OK, manager.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, cashierStart.StatusCode);
        Assert.Equal(HttpStatusCode.OK, kitchenStart.StatusCode);
        var page = await ReadAsync<PagedResponse<KitchenOrderDto>>(cashier);
        Assert.Equal(order.Id, Assert.Single(page.Items).Id);
    }

    [PostgresFact]
    public async Task PayOnPickupOrder_CompletesOnlyAfterForwardWorkflowAndPayment()
    {
        await factory.ResetAsync();
        var customerToken = await factory.CreateCustomerTokenAsync();
        var order = await CreateConfirmedOrderAsync(customerToken);
        var kitchenToken = await factory.CreateEmployeeTokenAsync(
            "workflow-kitchen",
            AuthenticationConstants.Roles.Kitchen);
        var cashierToken = await factory.CreateEmployeeTokenAsync(
            "workflow-cashier",
            AuthenticationConstants.Roles.Cashier);
        var managerToken = await factory.CreateEmployeeTokenAsync(
            "workflow-manager",
            AuthenticationConstants.Roles.Manager);
        factory.TimeProvider.SetUtcNow(Now);
        using var client = factory.CreateSecureClient();

        using var startedResponse = await SendAuthorizedJsonAsync(
            client,
            HttpMethod.Post,
            $"/api/v1/staff/kitchen/{order.Id}/start",
            kitchenToken,
            new OrderVersionRequest(order.RowVersion));
        Assert.Equal(HttpStatusCode.OK, startedResponse.StatusCode);
        var started = await ReadAsync<KitchenOrderDto>(startedResponse);
        Assert.Equal(OrderStatus.Preparing, started.Status);

        using var staleReady = await SendAuthorizedJsonAsync(
            client,
            HttpMethod.Post,
            $"/api/v1/staff/kitchen/{order.Id}/ready",
            kitchenToken,
            new OrderVersionRequest(order.RowVersion));
        Assert.Equal(HttpStatusCode.Conflict, staleReady.StatusCode);
        Assert.Equal("ORDER_VERSION_CONFLICT", await ReadProblemCodeAsync(staleReady));

        factory.TimeProvider.SetUtcNow(Now.AddMinutes(5));
        using var etaResponse = await SendAuthorizedJsonAsync(
            client,
            HttpMethod.Patch,
            $"/api/v1/staff/kitchen/{order.Id}/eta",
            kitchenToken,
            new UpdateEstimatedReadyTimeRequest(Now.AddMinutes(50), started.RowVersion));
        Assert.Equal(HttpStatusCode.OK, etaResponse.StatusCode);
        var etaUpdated = await ReadAsync<KitchenOrderDto>(etaResponse);

        factory.TimeProvider.SetUtcNow(Now.AddMinutes(30));
        using var readyResponse = await SendAuthorizedJsonAsync(
            client,
            HttpMethod.Post,
            $"/api/v1/staff/kitchen/{order.Id}/ready",
            kitchenToken,
            new OrderVersionRequest(etaUpdated.RowVersion));
        Assert.Equal(HttpStatusCode.OK, readyResponse.StatusCode);
        var ready = await ReadAsync<KitchenOrderDto>(readyResponse);
        Assert.Equal(OrderStatus.ReadyForPickup, ready.Status);

        using var kitchenComplete = await SendAuthorizedJsonAsync(
            client,
            HttpMethod.Post,
            $"/api/v1/staff/orders/{order.Id}/complete",
            kitchenToken,
            new OrderVersionRequest(ready.RowVersion));
        using var managerComplete = await SendAuthorizedJsonAsync(
            client,
            HttpMethod.Post,
            $"/api/v1/staff/orders/{order.Id}/complete",
            managerToken,
            new OrderVersionRequest(ready.RowVersion));
        Assert.Equal(HttpStatusCode.Forbidden, kitchenComplete.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, managerComplete.StatusCode);

        using var unpaidComplete = await SendAuthorizedJsonAsync(
            client,
            HttpMethod.Post,
            $"/api/v1/staff/orders/{order.Id}/complete",
            cashierToken,
            new OrderVersionRequest(ready.RowVersion));
        Assert.Equal(HttpStatusCode.Conflict, unpaidComplete.StatusCode);
        Assert.Equal("ORDER_PAYMENT_REQUIRED", await ReadProblemCodeAsync(unpaidComplete));

        using var missingPaymentMethod = await SendAuthorizedJsonAsync(
            client,
            HttpMethod.Post,
            $"/api/v1/staff/orders/{order.Id}/record-payment",
            cashierToken,
            new { rowVersion = ready.RowVersion });
        Assert.Equal(HttpStatusCode.BadRequest, missingPaymentMethod.StatusCode);

        factory.TimeProvider.SetUtcNow(Now.AddMinutes(31));
        using var paymentResponse = await SendAuthorizedJsonAsync(
            client,
            HttpMethod.Post,
            $"/api/v1/staff/orders/{order.Id}/record-payment",
            cashierToken,
            new RecordPaymentRequest(PaymentMethodUsed.Card, ready.RowVersion));
        Assert.Equal(HttpStatusCode.OK, paymentResponse.StatusCode);
        var paid = await ReadAsync<StaffOrderDetailDto>(paymentResponse);
        Assert.True(paid.PaymentReceived);
        Assert.Equal(PaymentMethodUsed.Card, paid.PaymentMethodUsed);

        factory.TimeProvider.SetUtcNow(Now.AddMinutes(32));
        using var completedResponse = await SendAuthorizedJsonAsync(
            client,
            HttpMethod.Post,
            $"/api/v1/staff/orders/{order.Id}/complete",
            cashierToken,
            new OrderVersionRequest(paid.RowVersion));
        Assert.Equal(HttpStatusCode.OK, completedResponse.StatusCode);
        var completed = await ReadAsync<StaffOrderDetailDto>(completedResponse);
        Assert.Equal(OrderStatus.Completed, completed.Status);

        using var customerResponse = await SendAuthorizedAsync(
            client,
            HttpMethod.Get,
            $"/api/v1/orders/{order.Id}",
            customerToken);
        var customerOrder = await ReadAsync<OrderDetailDto>(customerResponse);
        Assert.Equal(OrderStatus.Completed, customerOrder.Status);
        Assert.Equal(
            [
                OrderStatus.Confirmed,
                OrderStatus.Preparing,
                OrderStatus.ReadyForPickup,
                OrderStatus.Completed
            ],
            customerOrder.StatusHistory.Select(history => history.NewStatus).ToArray());
        var customerJson = await customerResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain("employeeId", customerJson, StringComparison.OrdinalIgnoreCase);

        using var activeKitchenResponse = await SendAuthorizedAsync(
            client,
            HttpMethod.Get,
            $"/api/v1/staff/kitchen/orders?orderNumber={order.OrderNumber}",
            kitchenToken);
        var activeKitchenOrders = await ReadAsync<PagedResponse<KitchenOrderDto>>(
            activeKitchenResponse);
        Assert.Empty(activeKitchenOrders.Items);

        var stored = await factory.ReadDatabaseAsync(async db => new
        {
            HistoryCount = await db.OrderStatusHistories.CountAsync(
                history => history.OrderId == order.Id),
            Audits = await db.EmployeeActionLogs
                .Where(log => log.EntityId == order.Id)
                .Select(log => log.ActionType)
                .ToListAsync(),
            Order = await db.Orders.SingleAsync(item => item.Id == order.Id)
        });
        Assert.Equal(4, stored.HistoryCount);
        Assert.Contains("OrderPreparationStarted", stored.Audits);
        Assert.Contains("EstimatedReadyTimeChanged", stored.Audits);
        Assert.Contains("OrderReadyForPickup", stored.Audits);
        Assert.Contains("OrderPaymentReceived", stored.Audits);
        Assert.Contains("OrderCompleted", stored.Audits);
        Assert.NotNull(stored.Order.PreparationStartedByEmployeeId);
        Assert.NotNull(stored.Order.ReadyByEmployeeId);
        Assert.NotNull(stored.Order.PaymentReceivedByEmployeeId);
        Assert.NotNull(stored.Order.CompletedByEmployeeId);
    }

    [PostgresFact]
    public async Task OnlineReadyOrder_CanBeCompletedByPickupRoleWithoutPaymentMutation()
    {
        await factory.ResetAsync();
        var customerToken = await factory.CreateCustomerTokenAsync();
        var order = await CreateConfirmedOrderAsync(
            customerToken,
            PaymentMethod.Online,
            OrderStatus.ReadyForPickup);
        var pickupToken = await factory.CreateEmployeeTokenAsync(
            "workflow-pickup",
            AuthenticationConstants.Roles.Pickup);
        factory.TimeProvider.SetUtcNow(Now);
        using var client = factory.CreateSecureClient();

        using var response = await SendAuthorizedJsonAsync(
            client,
            HttpMethod.Post,
            $"/api/v1/staff/orders/{order.Id}/complete",
            pickupToken,
            new OrderVersionRequest(order.RowVersion));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var completed = await ReadAsync<StaffOrderDetailDto>(response);
        Assert.Equal(OrderStatus.Completed, completed.Status);
        Assert.True(completed.PaymentReceived);
    }

    private async Task<Order> CreateConfirmedOrderAsync(
        string customerToken,
        PaymentMethod paymentMethod = PaymentMethod.PayOnPickup,
        OrderStatus status = OrderStatus.Confirmed)
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
                Status = status,
                PaymentMethod = paymentMethod,
                PaymentReceived = paymentMethod == PaymentMethod.Online,
                PickupMode = PickupMode.AsSoonAsPossible,
                CustomerName = customer.Name,
                CustomerPhoneNumber = customer.PhoneNumber,
                Comment = "Kitchen integration order",
                Subtotal = 22m,
                Total = 22m,
                Currency = "TJS",
                ConfirmedAt = Now.AddMinutes(-1),
                EstimatedReadyAt = Now.AddMinutes(45),
                PreparationStartedAt = status == OrderStatus.ReadyForPickup
                    ? Now.AddMinutes(-1)
                    : null,
                ReadyAt = status == OrderStatus.ReadyForPickup ? Now : null
            };
            order.Items.Add(new OrderItem
            {
                Id = Guid.NewGuid(),
                Order = order,
                ProductId = Guid.NewGuid(),
                ProductName = "Cappuccino",
                IsAvailableAtPurchase = true,
                BasePrice = 22m,
                FinalPrice = 22m,
                Quantity = 1,
                Comment = "Extra hot"
            });
            order.StatusHistory.Add(new OrderStatusHistory
            {
                Id = Guid.NewGuid(),
                Order = order,
                OldStatus = OrderStatus.PendingConfirmation,
                NewStatus = status,
                Timestamp = status == OrderStatus.ReadyForPickup ? Now : Now.AddMinutes(-1),
                CorrelationId = "postgres-workflow-seed"
            });
            if (paymentMethod == PaymentMethod.Online)
            {
                order.Payment = new Payment
                {
                    Id = Guid.NewGuid(),
                    Order = order,
                    Provider = PaymentProvider.Legacy,
                    ProviderOrderId = $"LEGACY{order.Id:N}",
                    Status = PaymentStatus.Paid,
                    Amount = order.Total,
                    Currency = order.Currency,
                    PaidAt = Now
                };
            }
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
