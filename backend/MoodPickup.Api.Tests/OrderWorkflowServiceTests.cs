using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MoodPickup.Api.Data;
using MoodPickup.Api.DTOs.Orders;
using MoodPickup.Api.Entities;
using MoodPickup.Api.Infrastructure;
using MoodPickup.Api.Interfaces;
using MoodPickup.Api.Options;
using MoodPickup.Api.Services;

namespace MoodPickup.Api.Tests;

public sealed class OrderWorkflowServiceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 11, 5, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task PayOnPickupOrder_FollowsWorkflowWithHistoryAuditAndNotifications()
    {
        await using var fixture = await WorkflowFixture.CreateAsync();
        var service = fixture.CreateService();

        var started = await service.StartPreparationAsync(
            fixture.Order.Id,
            new OrderVersionRequest(fixture.Order.RowVersion),
            CancellationToken.None);
        fixture.TimeProvider.SetUtcNow(Now.AddMinutes(1));
        var ready = await service.MarkReadyAsync(
            fixture.Order.Id,
            new OrderVersionRequest(started.RowVersion),
            CancellationToken.None);

        var paymentRequired = await Assert.ThrowsAsync<ApiProblemException>(() =>
            service.CompleteAsync(
                fixture.Order.Id,
                new OrderVersionRequest(ready.RowVersion),
                CancellationToken.None));
        Assert.Equal(StatusCodes.Status409Conflict, paymentRequired.Status);
        Assert.Equal("ORDER_PAYMENT_REQUIRED", paymentRequired.Code);

        fixture.TimeProvider.SetUtcNow(Now.AddMinutes(2));
        var paid = await service.RecordPaymentAsync(
            fixture.Order.Id,
            new RecordPaymentRequest(PaymentMethodUsed.Card, ready.RowVersion),
            CancellationToken.None);
        fixture.TimeProvider.SetUtcNow(Now.AddMinutes(3));
        var completed = await service.CompleteAsync(
            fixture.Order.Id,
            new OrderVersionRequest(paid.RowVersion),
            CancellationToken.None);

        Assert.Equal(OrderStatus.Completed, completed.Status);
        Assert.True(completed.PaymentReceived);
        Assert.Equal(PaymentMethodUsed.Card, completed.PaymentMethodUsed);
        Assert.Equal(Now.AddMinutes(3), completed.CompletedAt);
        Assert.Equal(
            [
                OrderStatus.Confirmed,
                OrderStatus.Preparing,
                OrderStatus.ReadyForPickup,
                OrderStatus.Completed
            ],
            completed.StatusHistory.Select(entry => entry.NewStatus).ToArray());

        var persisted = await fixture.DbContext.Orders
            .Include(order => order.StatusHistory)
            .SingleAsync();
        Assert.Equal(fixture.Employee.Id, persisted.PreparationStartedByEmployeeId);
        Assert.Equal(fixture.Employee.Id, persisted.ReadyByEmployeeId);
        Assert.Equal(fixture.Employee.Id, persisted.PaymentReceivedByEmployeeId);
        Assert.Equal(fixture.Employee.Id, persisted.CompletedByEmployeeId);
        Assert.All(
            persisted.StatusHistory.Skip(1),
            history => Assert.Equal("kitchen-workflow-test", history.CorrelationId));
        Assert.Equal(
            [
                "OrderPreparationStarted",
                "OrderReadyForPickup",
                "OrderPaymentReceived",
                "OrderCompleted"
            ],
            await fixture.DbContext.EmployeeActionLogs
                .OrderBy(entry => entry.CreatedAt)
                .Select(entry => entry.ActionType)
                .ToArrayAsync());
        Assert.Equal(
            ["OrderPreparing", "OrderReady", "PaymentStatusChanged", "OrderCompleted"],
            fixture.Notifier.Notifications.Select(entry => entry.EventName).ToArray());
    }

    [Fact]
    public async Task Workflow_RejectsSkippedRepeatedAndStaleTransitions()
    {
        await using var fixture = await WorkflowFixture.CreateAsync();
        var service = fixture.CreateService();

        var skipped = await Assert.ThrowsAsync<ApiProblemException>(() =>
            service.MarkReadyAsync(
                fixture.Order.Id,
                new OrderVersionRequest(fixture.Order.RowVersion),
                CancellationToken.None));
        Assert.Equal("ORDER_CANNOT_BE_MARKED_READY", skipped.Code);

        var stale = await Assert.ThrowsAsync<ApiProblemException>(() =>
            service.StartPreparationAsync(
                fixture.Order.Id,
                new OrderVersionRequest(Guid.NewGuid()),
                CancellationToken.None));
        Assert.Equal("ORDER_VERSION_CONFLICT", stale.Code);

        var started = await service.StartPreparationAsync(
            fixture.Order.Id,
            new OrderVersionRequest(fixture.Order.RowVersion),
            CancellationToken.None);
        var repeated = await Assert.ThrowsAsync<ApiProblemException>(() =>
            service.StartPreparationAsync(
                fixture.Order.Id,
                new OrderVersionRequest(started.RowVersion),
                CancellationToken.None));
        Assert.Equal("ORDER_ALREADY_PREPARING", repeated.Code);
    }

    [Fact]
    public async Task KitchenDashboard_FiltersActiveOrdersAndEtaChangeIsAudited()
    {
        await using var fixture = await WorkflowFixture.CreateAsync();
        var service = fixture.CreateService();
        var newEta = Now.AddMinutes(45);

        var page = await service.GetKitchenOrdersAsync(
            new KitchenOrderListQuery
            {
                Status = OrderStatus.Confirmed,
                Page = 1,
                PageSize = 20
            },
            CancellationToken.None);
        var dashboardOrder = Assert.Single(page.Items);
        Assert.Equal("Cappuccino", Assert.Single(dashboardOrder.Items).ProductName);
        Assert.False(dashboardOrder.PaymentReceived);

        var updated = await service.UpdateKitchenEtaAsync(
            fixture.Order.Id,
            new UpdateEstimatedReadyTimeRequest(newEta, dashboardOrder.RowVersion),
            CancellationToken.None);
        Assert.Equal(newEta, updated.EstimatedReadyAt);
        Assert.Equal(
            "EstimatedReadyTimeChanged",
            Assert.Single(await fixture.DbContext.EmployeeActionLogs.ToListAsync()).ActionType);
        Assert.Equal(
            "EstimatedReadyTimeChanged",
            Assert.Single(fixture.Notifier.Notifications).EventName);
    }

    private sealed class WorkflowFixture : IAsyncDisposable
    {
        private readonly DbContextOptions<MoodPickupDbContext> _options;

        private WorkflowFixture(
            DbContextOptions<MoodPickupDbContext> options,
            MoodPickupDbContext dbContext,
            Customer customer,
            Employee employee,
            Order order)
        {
            _options = options;
            DbContext = dbContext;
            Customer = customer;
            Employee = employee;
            Order = order;
        }

        public MoodPickupDbContext DbContext { get; }

        public Customer Customer { get; }

        public Employee Employee { get; }

        public Order Order { get; }

        public RecordingNotifier Notifier { get; } = new();

        public MutableTimeProvider TimeProvider { get; } = new(Now);

        public static async Task<WorkflowFixture> CreateAsync()
        {
            var options = new DbContextOptionsBuilder<MoodPickupDbContext>()
                .UseInMemoryDatabase($"kitchen-workflow-{Guid.NewGuid():N}")
                .Options;
            var dbContext = new MoodPickupDbContext(options);
            var customer = new Customer
            {
                Id = Guid.NewGuid(),
                Name = "Amina",
                PhoneNumber = "+992900000001"
            };
            var employee = new Employee
            {
                Id = Guid.NewGuid(),
                Username = "kitchen",
                FullName = "Kitchen Test",
                PasswordHash = "not-used"
            };
            var order = new Order
            {
                Id = Guid.NewGuid(),
                CustomerId = customer.Id,
                Customer = customer,
                OrderNumber = "MP-20260811-00001",
                Status = OrderStatus.Confirmed,
                PaymentMethod = PaymentMethod.PayOnPickup,
                PaymentReceived = false,
                PickupMode = PickupMode.AsSoonAsPossible,
                CustomerName = customer.Name,
                CustomerPhoneNumber = customer.PhoneNumber,
                Subtotal = 22m,
                Total = 22m,
                Currency = "TJS",
                EstimatedReadyAt = Now.AddMinutes(30),
                ConfirmedAt = Now,
                ConfirmedByEmployeeId = employee.Id
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
                Quantity = 1
            });
            order.StatusHistory.Add(new OrderStatusHistory
            {
                Id = Guid.NewGuid(),
                Order = order,
                OldStatus = OrderStatus.PendingConfirmation,
                NewStatus = OrderStatus.Confirmed,
                Timestamp = Now.AddMinutes(-1),
                EmployeeId = employee.Id,
                CorrelationId = "confirmation-test"
            });
            dbContext.AddRange(customer, employee, order);
            await dbContext.SaveChangesAsync();
            return new WorkflowFixture(options, dbContext, customer, employee, order);
        }

        public OrderWorkflowService CreateService()
        {
            var context = new WorkflowCurrentUserContext(Customer.Id, Employee.Id);
            return new OrderWorkflowService(
                DbContext,
                context,
                new EmployeeAuditService(DbContext, context),
                Notifier,
                new StaticOptionsMonitor<CheckoutOptions>(new CheckoutOptions()),
                TimeProvider,
                NullLogger<OrderWorkflowService>.Instance);
        }

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await using var cleanup = new MoodPickupDbContext(_options);
            await cleanup.Database.EnsureDeletedAsync();
        }
    }

    private sealed class WorkflowCurrentUserContext(Guid customerId, Guid employeeId)
        : ICurrentUserContext
    {
        public string CorrelationId => "kitchen-workflow-test";

        public Guid GetRequiredCustomerId() => customerId;

        public Guid GetRequiredEmployeeId() => employeeId;
    }

    private sealed class MutableTimeProvider(DateTimeOffset value) : TimeProvider
    {
        private DateTimeOffset _value = value;

        public override DateTimeOffset GetUtcNow() => _value;

        public void SetUtcNow(DateTimeOffset value) => _value = value;
    }

    private sealed class RecordingNotifier : IOrderRealtimeNotifier
    {
        public List<RecordedNotification> Notifications { get; } = [];

        public Task OrderConfirmedAsync(Guid customerId, OrderRealtimeEventDto notification, CancellationToken cancellationToken) =>
            Record("OrderConfirmed", customerId, notification);

        public Task OrderRejectedAsync(Guid customerId, OrderRealtimeEventDto notification, CancellationToken cancellationToken) =>
            Record("OrderRejected", customerId, notification);

        public Task EstimatedReadyTimeChangedAsync(Guid customerId, OrderRealtimeEventDto notification, CancellationToken cancellationToken) =>
            Record("EstimatedReadyTimeChanged", customerId, notification);

        public Task OrderPreparingAsync(Guid customerId, OrderRealtimeEventDto notification, CancellationToken cancellationToken) =>
            Record("OrderPreparing", customerId, notification);

        public Task OrderReadyAsync(Guid customerId, OrderRealtimeEventDto notification, CancellationToken cancellationToken) =>
            Record("OrderReady", customerId, notification);

        public Task PaymentStatusChangedAsync(Guid customerId, OrderRealtimeEventDto notification, CancellationToken cancellationToken) =>
            Record("PaymentStatusChanged", customerId, notification);

        public Task RefundStatusChangedAsync(Guid customerId, OrderRealtimeEventDto notification, CancellationToken cancellationToken) =>
            Record("RefundStatusChanged", customerId, notification);

        public Task OrderCompletedAsync(Guid customerId, OrderRealtimeEventDto notification, CancellationToken cancellationToken) =>
            Record("OrderCompleted", customerId, notification);

        private Task Record(
            string eventName,
            Guid customerId,
            OrderRealtimeEventDto notification)
        {
            Notifications.Add(new RecordedNotification(eventName, customerId, notification));
            return Task.CompletedTask;
        }
    }

    private sealed record RecordedNotification(
        string EventName,
        Guid CustomerId,
        OrderRealtimeEventDto Event);

    private sealed class StaticOptionsMonitor<T>(T value) : IOptionsMonitor<T>
        where T : class
    {
        public T CurrentValue => value;

        public T Get(string? name) => value;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
