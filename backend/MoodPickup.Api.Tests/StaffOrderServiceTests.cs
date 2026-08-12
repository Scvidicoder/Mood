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

public sealed class StaffOrderServiceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 11, 5, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Confirmation_PersistsEmployeeTimeAuditAndCustomerNotification()
    {
        await using var fixture = await StaffOrderFixture.CreateAsync();
        var service = fixture.CreateService();

        var confirmed = await service.ConfirmAsync(
            fixture.Order.Id,
            new ConfirmOrderRequest(Now.AddMinutes(30), fixture.Order.RowVersion),
            CancellationToken.None);

        Assert.Equal(OrderStatus.Confirmed, confirmed.Status);
        Assert.Equal(Now.AddMinutes(30), confirmed.EstimatedReadyAt);
        Assert.Equal(Now, confirmed.ConfirmedAt);
        var persisted = await fixture.DbContext.Orders.SingleAsync();
        Assert.Equal(fixture.Employee.Id, persisted.ConfirmedByEmployeeId);
        var audit = Assert.Single(await fixture.DbContext.EmployeeActionLogs.ToListAsync());
        Assert.Equal("OrderConfirmed", audit.ActionType);
        Assert.Equal("staff-order-test", audit.CorrelationId);
        var notification = Assert.Single(fixture.Notifier.Notifications);
        Assert.Equal("OrderConfirmed", notification.EventName);
        Assert.Equal(fixture.Customer.Id, notification.CustomerId);
        Assert.Equal(OrderStatus.Confirmed, notification.Event.Status);
    }

    [Fact]
    public async Task Rejection_RequiresPendingOrderAndStoresTrimmedReason()
    {
        await using var fixture = await StaffOrderFixture.CreateAsync();
        var service = fixture.CreateService();

        var rejected = await service.RejectAsync(
            fixture.Order.Id,
            new RejectOrderRequest("  Cafe is closing early.  ", fixture.Order.RowVersion),
            CancellationToken.None);

        Assert.Equal(OrderStatus.Rejected, rejected.Status);
        Assert.Equal("Cafe is closing early.", rejected.RejectReason);
        var persisted = await fixture.DbContext.Orders.SingleAsync();
        Assert.Equal(fixture.Employee.Id, persisted.RejectedByEmployeeId);
        Assert.Equal(Now, persisted.RejectedAt);
        Assert.Equal("OrderRejected", Assert.Single(fixture.Notifier.Notifications).EventName);
        Assert.Equal(
            "OrderRejected",
            Assert.Single(await fixture.DbContext.EmployeeActionLogs.ToListAsync()).ActionType);
    }

    [Fact]
    public async Task Confirmation_RejectsEstimatedReadyTimeOutsideWorkingHours()
    {
        await using var fixture = await StaffOrderFixture.CreateAsync();
        var service = fixture.CreateService();

        var exception = await Assert.ThrowsAsync<ApiValidationException>(() =>
            service.ConfirmAsync(
                fixture.Order.Id,
                new ConfirmOrderRequest(Now.AddDays(1), fixture.Order.RowVersion),
                CancellationToken.None));

        Assert.Contains("estimatedReadyTime", exception.Errors.Keys);
        Assert.Equal(OrderStatus.PendingConfirmation, fixture.Order.Status);
        Assert.Empty(fixture.Notifier.Notifications);
        Assert.Empty(await fixture.DbContext.EmployeeActionLogs.ToListAsync());
    }

    [Fact]
    public async Task StaffMutation_RejectsAStaleRowVersion()
    {
        await using var fixture = await StaffOrderFixture.CreateAsync();
        var service = fixture.CreateService();

        var exception = await Assert.ThrowsAsync<ApiProblemException>(() =>
            service.ConfirmAsync(
                fixture.Order.Id,
                new ConfirmOrderRequest(Now.AddMinutes(30), Guid.NewGuid()),
                CancellationToken.None));

        Assert.Equal(StatusCodes.Status409Conflict, exception.Status);
        Assert.Equal("ORDER_VERSION_CONFLICT", exception.Code);
        Assert.True(exception.Extensions.ContainsKey("currentResource"));
    }

    [Fact]
    public async Task ConfirmedOrder_CannotBeRejectedAndTimeChangeIsNotified()
    {
        await using var fixture = await StaffOrderFixture.CreateAsync();
        var service = fixture.CreateService();
        var confirmed = await service.ConfirmAsync(
            fixture.Order.Id,
            new ConfirmOrderRequest(Now.AddMinutes(30), fixture.Order.RowVersion),
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<ApiProblemException>(() =>
            service.RejectAsync(
                fixture.Order.Id,
                new RejectOrderRequest("Not allowed", confirmed.RowVersion),
                CancellationToken.None));
        Assert.Equal("CONFIRMED_ORDER_CANNOT_BE_REJECTED", exception.Code);

        var updated = await service.UpdateEstimatedReadyTimeAsync(
            fixture.Order.Id,
            new UpdateEstimatedReadyTimeRequest(Now.AddMinutes(45), confirmed.RowVersion),
            CancellationToken.None);

        Assert.Equal(Now.AddMinutes(45), updated.EstimatedReadyAt);
        Assert.Equal(
            ["OrderConfirmed", "EstimatedReadyTimeChanged"],
            fixture.Notifier.Notifications.Select(item => item.EventName).ToArray());
        var auditActions = await fixture.DbContext.EmployeeActionLogs
            .Select(item => item.ActionType)
            .ToArrayAsync();
        Assert.Contains("OrderConfirmed", auditActions);
        Assert.Contains("EstimatedReadyTimeChanged", auditActions);
    }

    [Fact]
    public async Task Rejection_OfPaidOnlineOrderFlagsRefundAndNotifiesCustomer()
    {
        await using var fixture = await StaffOrderFixture.CreateAsync();
        fixture.Order.PaymentMethod = PaymentMethod.Online;
        fixture.Order.PaymentReceived = true;
        fixture.Order.Payment = new Payment
        {
            Id = Guid.NewGuid(),
            OrderId = fixture.Order.Id,
            Order = fixture.Order,
            Provider = PaymentProvider.Alif,
            ProviderOrderId = "provider-order",
            ProviderTransactionId = "provider-transaction",
            Status = PaymentStatus.Paid,
            Amount = fixture.Order.Total,
            Currency = fixture.Order.Currency,
            PaidAt = Now
        };
        fixture.DbContext.Payments.Add(fixture.Order.Payment);
        await fixture.DbContext.SaveChangesAsync();
        var payments = new TestPaymentService { MarkRefundRequired = true };
        var service = fixture.CreateService(payments);

        await service.RejectAsync(
            fixture.Order.Id,
            new RejectOrderRequest("Kitchen capacity is full.", fixture.Order.RowVersion),
            CancellationToken.None);

        Assert.Equal(PaymentStatus.RefundRequired, fixture.Order.Payment.Status);
        Assert.Equal(1, payments.MarkRefundRequiredCalls);
        Assert.Equal(
            ["OrderRejected", "RefundStatusChanged"],
            fixture.Notifier.Notifications.Select(item => item.EventName).ToArray());
    }

    private sealed class StaffOrderFixture : IAsyncDisposable
    {
        private readonly DbContextOptions<MoodPickupDbContext> _options;
        private readonly FixedTimeProvider _timeProvider = new(Now);

        private StaffOrderFixture(
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

        public RecordingOrderNotifier Notifier { get; } = new();

        public static async Task<StaffOrderFixture> CreateAsync()
        {
            var options = new DbContextOptionsBuilder<MoodPickupDbContext>()
                .UseInMemoryDatabase($"staff-order-{Guid.NewGuid():N}")
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
                Username = "cashier",
                FullName = "Cashier Test",
                PasswordHash = "not-used"
            };
            var order = new Order
            {
                Id = Guid.NewGuid(),
                CustomerId = customer.Id,
                Customer = customer,
                OrderNumber = "MP-20260811-00001",
                Status = OrderStatus.PendingConfirmation,
                PaymentMethod = PaymentMethod.PayOnPickup,
                PickupMode = PickupMode.AsSoonAsPossible,
                CustomerName = customer.Name,
                CustomerPhoneNumber = customer.PhoneNumber,
                Subtotal = 22m,
                Total = 22m,
                Currency = "TJS"
            };
            dbContext.AddRange(customer, employee, order);
            await dbContext.SaveChangesAsync();
            return new StaffOrderFixture(options, dbContext, customer, employee, order);
        }

        public StaffOrderService CreateService(IPaymentService? paymentService = null)
        {
            var context = new StaffCurrentUserContext(Customer.Id, Employee.Id);
            return new StaffOrderService(
                DbContext,
                context,
                new EmployeeAuditService(DbContext, context),
                paymentService ?? new TestPaymentService(),
                Notifier,
                new StaticOptionsMonitor<CheckoutOptions>(new CheckoutOptions()),
                _timeProvider,
                NullLogger<StaffOrderService>.Instance);
        }

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await using var cleanup = new MoodPickupDbContext(_options);
            await cleanup.Database.EnsureDeletedAsync();
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }

    private sealed class StaffCurrentUserContext(Guid customerId, Guid employeeId)
        : ICurrentUserContext
    {
        public string CorrelationId => "staff-order-test";

        public Guid GetRequiredCustomerId() => customerId;

        public Guid GetRequiredEmployeeId() => employeeId;
    }

    private sealed class RecordingOrderNotifier : IOrderRealtimeNotifier
    {
        public List<RecordedNotification> Notifications { get; } = [];

        public Task OrderConfirmedAsync(
            Guid customerId,
            OrderRealtimeEventDto notification,
            CancellationToken cancellationToken)
        {
            Notifications.Add(new("OrderConfirmed", customerId, notification));
            return Task.CompletedTask;
        }

        public Task OrderRejectedAsync(
            Guid customerId,
            OrderRealtimeEventDto notification,
            CancellationToken cancellationToken)
        {
            Notifications.Add(new("OrderRejected", customerId, notification));
            return Task.CompletedTask;
        }

        public Task EstimatedReadyTimeChangedAsync(
            Guid customerId,
            OrderRealtimeEventDto notification,
            CancellationToken cancellationToken)
        {
            Notifications.Add(new("EstimatedReadyTimeChanged", customerId, notification));
            return Task.CompletedTask;
        }

        public Task OrderPreparingAsync(
            Guid customerId,
            OrderRealtimeEventDto notification,
            CancellationToken cancellationToken)
        {
            Notifications.Add(new("OrderPreparing", customerId, notification));
            return Task.CompletedTask;
        }

        public Task OrderReadyAsync(
            Guid customerId,
            OrderRealtimeEventDto notification,
            CancellationToken cancellationToken)
        {
            Notifications.Add(new("OrderReady", customerId, notification));
            return Task.CompletedTask;
        }

        public Task PaymentStatusChangedAsync(
            Guid customerId,
            OrderRealtimeEventDto notification,
            CancellationToken cancellationToken)
        {
            Notifications.Add(new("PaymentStatusChanged", customerId, notification));
            return Task.CompletedTask;
        }

        public Task RefundStatusChangedAsync(
            Guid customerId,
            OrderRealtimeEventDto notification,
            CancellationToken cancellationToken)
        {
            Notifications.Add(new("RefundStatusChanged", customerId, notification));
            return Task.CompletedTask;
        }

        public Task OrderCompletedAsync(
            Guid customerId,
            OrderRealtimeEventDto notification,
            CancellationToken cancellationToken)
        {
            Notifications.Add(new("OrderCompleted", customerId, notification));
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
