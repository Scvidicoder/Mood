using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MoodPickup.Api.Data;
using MoodPickup.Api.DTOs.Orders;
using MoodPickup.Api.DTOs.Payments;
using MoodPickup.Api.Entities;
using MoodPickup.Api.Infrastructure;
using MoodPickup.Api.Interfaces;
using MoodPickup.Api.Options;
using MoodPickup.Api.Services;

namespace MoodPickup.Api.Tests;

public sealed class PaymentServiceTests
{
    private const string Key = "44444444";
    private const string Password = "cztef62wrwcysyubbbdnhlk1rs2cztfsqgwww7j0";
    private static readonly DateTimeOffset Now =
        new(2026, 8, 11, 7, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ValidPaidCallback_IsAtomicAuditedNotifiedAndIdempotent()
    {
        await using var fixture = await PaymentFixture.CreateAsync();

        var first = await fixture.Service.HandleAlifCallbackAsync(
            Callback("ok", 10m),
            CancellationToken.None);
        var duplicate = await fixture.Service.HandleAlifCallbackAsync(
            Callback("ok", 10m),
            CancellationToken.None);

        Assert.True(first.Processed);
        Assert.False(first.Duplicate);
        Assert.False(duplicate.Processed);
        Assert.True(duplicate.Duplicate);
        var payment = await fixture.DbContext.Payments
            .Include(item => item.Order)
            .SingleAsync();
        Assert.Equal(PaymentStatus.Paid, payment.Status);
        Assert.Equal("92938922", payment.ProviderTransactionId);
        Assert.Equal(Now, payment.PaidAt);
        Assert.True(payment.Order.PaymentReceived);
        Assert.Equal(Now, payment.Order.PaymentReceivedAt);
        Assert.Equal(1, await fixture.DbContext.PaymentWebhookEvents.CountAsync());
        Assert.Equal(
            ["PaymentCallbackProcessed", "PaymentCompleted"],
            await fixture.DbContext.EmployeeActionLogs
                .OrderBy(item => item.ActionType)
                .Select(item => item.ActionType)
                .ToArrayAsync());
        var notification = Assert.Single(fixture.Notifier.Notifications);
        Assert.Equal("PaymentStatusChanged", notification.EventName);
        Assert.Equal(PaymentStatus.Paid, notification.Event.PaymentStatus);
    }

    [Fact]
    public async Task Callback_RejectsInvalidSignatureOrAmountWithoutChangingPayment()
    {
        await using var fixture = await PaymentFixture.CreateAsync();
        var invalidSignature = Callback("ok", 10m);
        invalidSignature = new AlifCallbackRequest
        {
            OrderId = invalidSignature.OrderId,
            TransactionId = invalidSignature.TransactionId,
            Status = invalidSignature.Status,
            Token = "00",
            Amount = invalidSignature.Amount,
            Account = invalidSignature.Account
        };

        var signatureException = await Assert.ThrowsAsync<ApiProblemException>(() =>
            fixture.Service.HandleAlifCallbackAsync(
                invalidSignature,
                CancellationToken.None));
        var amountException = await Assert.ThrowsAsync<ApiProblemException>(() =>
            fixture.Service.HandleAlifCallbackAsync(
                Callback("ok", 11m),
                CancellationToken.None));

        Assert.Equal("INVALID_PAYMENT_CALLBACK", signatureException.Code);
        Assert.Equal("INVALID_PAYMENT_CALLBACK", amountException.Code);
        var payment = await fixture.DbContext.Payments.SingleAsync();
        Assert.Equal(PaymentStatus.Pending, payment.Status);
        Assert.False(fixture.Order.PaymentReceived);
        Assert.Empty(await fixture.DbContext.PaymentWebhookEvents.ToListAsync());
        Assert.Empty(fixture.Notifier.Notifications);
    }

    [Fact]
    public async Task PartialCancellation_RequiresReconciliationAndNeverClaimsARefund()
    {
        await using var fixture = await PaymentFixture.CreateAsync();

        await fixture.Service.HandleAlifCallbackAsync(
            Callback("partially_canceled", 10m),
            CancellationToken.None);

        var payment = await fixture.DbContext.Payments.SingleAsync();
        Assert.Equal(PaymentStatus.ReconciliationRequired, payment.Status);
        Assert.Null(payment.RefundedAt);
        Assert.Contains("partial cancellation", payment.FailureReason);
        Assert.False(payment.Order.PaymentReceived);
    }

    [Fact]
    public async Task StatusVerification_AppliesOnlyAValidatedMatchingProviderResult()
    {
        await using var fixture = await PaymentFixture.CreateAsync();
        fixture.Provider.CheckResult = new PaymentProviderStatusResult(
            "12345678",
            "92938922",
            "ok",
            10m,
            PaymentStatus.Paid,
            null);

        var result = await fixture.Service.VerifyOwnedAsync(
            fixture.Payment.Id,
            CancellationToken.None);

        Assert.Equal(PaymentStatus.Paid, result.Status);
        Assert.Equal(Now, result.PaidAt);
        Assert.Equal(Now, fixture.Payment.LastVerifiedAt);
        Assert.Equal(1, fixture.Provider.CheckCalls);
        Assert.Equal("PaymentStatusChanged", Assert.Single(fixture.Notifier.Notifications).EventName);
    }

    [Fact]
    public async Task PaidRejectedPayment_StaysRefundRequiredWithoutAProviderRefundRequest()
    {
        await using var fixture = await PaymentFixture.CreateAsync(PaymentStatus.Paid);

        var changed = await fixture.Service.MarkRefundRequiredForRejectedOrderAsync(
            fixture.Order,
            CancellationToken.None);
        await fixture.DbContext.SaveChangesAsync();

        Assert.True(changed);
        Assert.Equal(PaymentStatus.RefundRequired, fixture.Payment.Status);
        Assert.Null(fixture.Payment.RefundedAt);
        Assert.False(fixture.Order.PaymentReceived);
        Assert.Contains("No provider refund was sent", fixture.Payment.FailureReason);
        Assert.Equal(0, fixture.Provider.RefundCalls);
        Assert.Equal(
            "RefundRequired",
            Assert.Single(await fixture.DbContext.EmployeeActionLogs.ToListAsync()).ActionType);

        fixture.Provider.CheckResult = new PaymentProviderStatusResult(
            "12345678",
            "92938922",
            "ok",
            10m,
            PaymentStatus.Paid,
            null);
        var verified = await fixture.Service.VerifyOwnedAsync(
            fixture.Payment.Id,
            CancellationToken.None);

        Assert.Equal(PaymentStatus.RefundRequired, verified.Status);
        Assert.Equal(0, fixture.Provider.RefundCalls);
    }

    private static AlifCallbackRequest Callback(string status, decimal amount) =>
        new()
        {
            OrderId = "12345678",
            TransactionId = "92938922",
            Status = status,
            Token = ResponseToken("12345678", status, "92938922"),
            Amount = amount,
            Account = "5058***ALF**0104",
            TransactionType = "korti_milli"
        };

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

    private sealed class PaymentFixture : IAsyncDisposable
    {
        private readonly DbContextOptions<MoodPickupDbContext> _options;

        private PaymentFixture(
            DbContextOptions<MoodPickupDbContext> options,
            MoodPickupDbContext dbContext,
            Order order,
            Payment payment,
            FakePaymentProvider provider,
            RecordingNotifier notifier,
            PaymentService service)
        {
            _options = options;
            DbContext = dbContext;
            Order = order;
            Payment = payment;
            Provider = provider;
            Notifier = notifier;
            Service = service;
        }

        public MoodPickupDbContext DbContext { get; }

        public Order Order { get; }

        public Payment Payment { get; }

        public FakePaymentProvider Provider { get; }

        public RecordingNotifier Notifier { get; }

        public PaymentService Service { get; }

        public static async Task<PaymentFixture> CreateAsync(
            PaymentStatus status = PaymentStatus.Pending)
        {
            var options = new DbContextOptionsBuilder<MoodPickupDbContext>()
                .UseInMemoryDatabase($"payment-service-{Guid.NewGuid():N}")
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
                Username = "administrator",
                FullName = "Administrator",
                PasswordHash = "not-used"
            };
            var order = new Order
            {
                Id = Guid.NewGuid(),
                CustomerId = customer.Id,
                Customer = customer,
                OrderNumber = "MP-20260811-00001",
                Status = OrderStatus.PendingConfirmation,
                PaymentMethod = PaymentMethod.Online,
                PickupMode = PickupMode.AsSoonAsPossible,
                CustomerName = customer.Name,
                CustomerPhoneNumber = customer.PhoneNumber,
                Subtotal = 10m,
                Total = 10m,
                Currency = "TJS",
                PaymentReceived = status == PaymentStatus.Paid,
                PaymentReceivedAt = status == PaymentStatus.Paid ? Now : null
            };
            var payment = new Payment
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                Order = order,
                Provider = PaymentProvider.Alif,
                ProviderOrderId = "12345678",
                ProviderTransactionId = status == PaymentStatus.Paid ? "92938922" : null,
                Status = status,
                Amount = 10m,
                Currency = "TJS",
                PaidAt = status == PaymentStatus.Paid ? Now : null
            };
            payment.Attempts.Add(new PaymentAttempt
            {
                Id = Guid.NewGuid(),
                PaymentId = payment.Id,
                Payment = payment,
                AttemptNumber = 1,
                ProviderReference = payment.ProviderOrderId,
                ProviderStatus = "launch_created",
                RequestSnapshot = "{}"
            });
            order.Payment = payment;
            dbContext.AddRange(customer, employee, order, payment);
            await dbContext.SaveChangesAsync();

            var currentUser = new PaymentCurrentUser(customer.Id, employee.Id);
            var provider = new FakePaymentProvider();
            var notifier = new RecordingNotifier();
            var optionMonitor = new StaticOptionsMonitor<AlifOptions>(new AlifOptions
            {
                Enabled = true,
                Key = Key,
                Password = Password,
                CallbackUrl = "https://merchant.test/callback",
                ReturnUrl = "https://merchant.test/payment/result"
            });
            var service = new PaymentService(
                dbContext,
                currentUser,
                [provider],
                new AlifSignatureService(optionMonitor),
                new SystemAuditService(dbContext, new HttpContextAccessor()),
                new EmployeeAuditService(dbContext, currentUser),
                notifier,
                new StaticOptionsMonitor<PaymentOptions>(new PaymentOptions
                {
                    Provider = PaymentProvider.Alif
                }),
                new FixedTimeProvider(Now),
                NullLogger<PaymentService>.Instance);
            return new PaymentFixture(
                options,
                dbContext,
                order,
                payment,
                provider,
                notifier,
                service);
        }

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await using var cleanup = new MoodPickupDbContext(_options);
            await cleanup.Database.EnsureDeletedAsync();
        }
    }

    private sealed class FakePaymentProvider : IPaymentProvider
    {
        public PaymentProvider Provider => PaymentProvider.Alif;

        public PaymentProviderStatusResult CheckResult { get; set; } =
            new("12345678", "pending", "pending", 10m, PaymentStatus.Pending, null);

        public int CheckCalls { get; private set; }

        public int RefundCalls { get; private set; }

        public Task<PaymentLaunchResponse> CreatePaymentLaunchAsync(
            PaymentProviderLaunchRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<PaymentProviderStatusResult> CheckPaymentStatusAsync(
            string providerOrderId,
            CancellationToken cancellationToken)
        {
            CheckCalls++;
            return Task.FromResult(CheckResult);
        }

        public Task<PaymentProviderRefundResult> RefundAsync(
            PaymentProviderRefundRequest request,
            CancellationToken cancellationToken)
        {
            RefundCalls++;
            return Task.FromResult(new PaymentProviderRefundResult(true, "ok", "refund"));
        }
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

    private sealed class PaymentCurrentUser(Guid customerId, Guid employeeId)
        : ICurrentUserContext
    {
        public string CorrelationId => "payment-service-test";

        public Guid GetRequiredCustomerId() => customerId;

        public Guid GetRequiredEmployeeId() => employeeId;
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }

    private sealed class StaticOptionsMonitor<T>(T value) : IOptionsMonitor<T>
        where T : class
    {
        public T CurrentValue => value;

        public T Get(string? name) => value;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
