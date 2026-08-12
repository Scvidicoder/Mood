using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using MoodPickup.Api.Data;
using MoodPickup.Api.DTOs.Orders;
using MoodPickup.Api.DTOs.Payments;
using MoodPickup.Api.Entities;
using MoodPickup.Api.Infrastructure;
using MoodPickup.Api.Interfaces;
using MoodPickup.Api.Options;
using Npgsql;

namespace MoodPickup.Api.Services;

public sealed class PaymentService(
    MoodPickupDbContext dbContext,
    ICurrentUserContext currentUser,
    IEnumerable<IPaymentProvider> providers,
    AlifSignatureService signatureService,
    ISystemAuditService systemAudit,
    IEmployeeAuditService employeeAudit,
    IOrderRealtimeNotifier realtimeNotifier,
    IOptionsMonitor<PaymentOptions> paymentOptions,
    TimeProvider timeProvider,
    ILogger<PaymentService> logger) : IPaymentService
{
    private static readonly JsonSerializerOptions SnapshotJsonOptions =
        new(JsonSerializerDefaults.Web);

    public async Task<PaymentLaunchResponse> CreateForOrderAsync(
        Order order,
        CancellationToken cancellationToken)
    {
        if (order.PaymentMethod != PaymentMethod.Online)
        {
            throw StatusConflict("Only online orders can create a provider payment.");
        }

        if (order.Payment is not null)
        {
            throw new ApiProblemException(
                StatusCodes.Status409Conflict,
                "payment_already_completed",
                "A payment already exists for this order",
                "PAYMENT_ALREADY_COMPLETED");
        }

        var paymentId = Guid.NewGuid();
        var providerOrderId = $"MP{paymentId:N}";
        var selectedProvider = paymentOptions.CurrentValue.Provider;
        var provider = GetProvider(selectedProvider);
        var launch = await provider.CreatePaymentLaunchAsync(
            new PaymentProviderLaunchRequest(
                paymentId,
                providerOrderId,
                order.Total,
                order.Currency,
                order.CustomerPhoneNumber,
                $"Mood Pickup order {order.OrderNumber}"),
            cancellationToken);

        var payment = new Payment
        {
            Id = paymentId,
            OrderId = order.Id,
            Order = order,
            Provider = selectedProvider,
            ProviderOrderId = providerOrderId,
            Status = PaymentStatus.Pending,
            Amount = order.Total,
            Currency = order.Currency
        };
        payment.Attempts.Add(new PaymentAttempt
        {
            Id = Guid.NewGuid(),
            PaymentId = payment.Id,
            Payment = payment,
            AttemptNumber = 1,
            ProviderReference = providerOrderId,
            ProviderStatus = "launch_created",
            RequestSnapshot = JsonSerializer.Serialize(
                new
                {
                    provider = payment.Provider,
                    providerOrderId,
                    amount = payment.Amount,
                    currency = payment.Currency,
                    method = launch.Method,
                    actionUrl = launch.ActionUrl
                },
                SnapshotJsonOptions)
        });
        order.Payment = payment;
        dbContext.Payments.Add(payment);
        await systemAudit.RecordAsync(
            "PaymentCreated",
            "Payment",
            payment.Id,
            $"Created online payment for order '{order.OrderNumber}'.",
            null,
            new
            {
                orderId = order.Id,
                provider = payment.Provider,
                status = payment.Status,
                amount = payment.Amount,
                currency = payment.Currency
            },
            cancellationToken);

        return launch;
    }

    public async Task<CustomerPaymentDto> GetOwnedAsync(
        Guid paymentId,
        CancellationToken cancellationToken)
    {
        var customerId = currentUser.GetRequiredCustomerId();
        var payment = await dbContext.Payments
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.Id == paymentId && item.Order.CustomerId == customerId,
                cancellationToken)
            ?? throw NotFound();
        return ToCustomerDto(payment);
    }

    public async Task<CustomerPaymentDto> VerifyOwnedAsync(
        Guid paymentId,
        CancellationToken cancellationToken)
    {
        var customerId = currentUser.GetRequiredCustomerId();
        var payment = await dbContext.Payments
            .Include(item => item.Order)
            .Include(item => item.Attempts)
            .SingleOrDefaultAsync(
                item => item.Id == paymentId && item.Order.CustomerId == customerId,
                cancellationToken)
            ?? throw NotFound();

        if (payment.Provider == PaymentProvider.Development ||
            payment.Status is PaymentStatus.Refunded or PaymentStatus.RefundPending)
        {
            return ToCustomerDto(payment);
        }

        var providerResult = await GetProvider(payment.Provider)
            .CheckPaymentStatusAsync(payment.ProviderOrderId, cancellationToken);
        ValidateProviderResult(payment, providerResult);
        var oldStatus = payment.Status;
        ApplyProviderStatus(
            payment,
            providerResult.TransactionId,
            providerResult.Status,
            providerResult.FailureReason);
        payment.LastVerifiedAt = timeProvider.GetUtcNow();
        UpdateAttempt(payment, providerResult.ProviderStatus, providerResult);
        await systemAudit.RecordAsync(
            "PaymentStatusVerified",
            "Payment",
            payment.Id,
            $"Verified payment status for order '{payment.Order.OrderNumber}'.",
            new { status = oldStatus },
            new
            {
                status = payment.Status,
                providerStatus = providerResult.ProviderStatus,
                verifiedAt = payment.LastVerifiedAt
            },
            cancellationToken);
        if (oldStatus != payment.Status)
        {
            await RecordStatusAuditAsync(payment, oldStatus, cancellationToken);
        }

        await SaveChangesAsync(cancellationToken);
        if (oldStatus != payment.Status)
        {
            await NotifyStatusChangedSafelyAsync(payment, cancellationToken);
        }

        return ToCustomerDto(payment);
    }

    public async Task<PaymentCallbackResult> HandleAlifCallbackAsync(
        AlifCallbackRequest request,
        CancellationToken cancellationToken)
    {
        if (!signatureService.VerifyProviderResponseToken(
                request.OrderId,
                request.Status,
                request.TransactionId,
                request.Token))
        {
            throw InvalidCallback("The callback signature is invalid.");
        }

        if (!AlifStatusMapper.TryMap(
                request.Status,
                out var mappedStatus,
                out var failureReason))
        {
            throw InvalidCallback("The callback contains an unknown payment status.");
        }

        var eventIdentifier = HashHex(string.Join(
            '|',
            PaymentProvider.Alif,
            request.OrderId,
            request.TransactionId,
            request.Status.Trim().ToLowerInvariant(),
            AlifSignatureService.FormatAmount(request.Amount)));
        var payloadHash = HashHex(string.Join(
            '|',
            request.OrderId,
            request.TransactionId,
            request.Status,
            AlifSignatureService.FormatAmount(request.Amount),
            request.Account,
            request.TransactionType,
            request.Token));

        IDbContextTransaction? transaction = null;
        if (dbContext.Database.IsRelational())
        {
            transaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);
        }

        try
        {
            if (await dbContext.PaymentWebhookEvents.AnyAsync(
                    item => item.Provider == PaymentProvider.Alif &&
                            item.EventIdentifier == eventIdentifier,
                    cancellationToken))
            {
                if (transaction is not null)
                {
                    await transaction.CommitAsync(cancellationToken);
                }

                return new PaymentCallbackResult(false, true);
            }

            var payment = await dbContext.Payments
                .Include(item => item.Order)
                .Include(item => item.Attempts)
                .SingleOrDefaultAsync(
                    item => item.Provider == PaymentProvider.Alif &&
                            item.ProviderOrderId == request.OrderId,
                    cancellationToken)
                ?? throw InvalidCallback("The callback order identifier is unknown.");
            if (payment.Amount != request.Amount ||
                !string.Equals(payment.Currency, "TJS", StringComparison.Ordinal))
            {
                throw InvalidCallback("The callback amount does not match the payment.");
            }

            EnsureTransactionIsCompatible(payment, request.TransactionId);
            var webhook = new PaymentWebhookEvent
            {
                Id = Guid.NewGuid(),
                Provider = PaymentProvider.Alif,
                EventIdentifier = eventIdentifier,
                PayloadHash = payloadHash,
                ReceivedAt = timeProvider.GetUtcNow(),
                ProcessingResult = "Received"
            };
            dbContext.PaymentWebhookEvents.Add(webhook);

            var oldStatus = payment.Status;
            ApplyProviderStatus(
                payment,
                request.TransactionId,
                mappedStatus,
                failureReason);
            UpdateAttempt(
                payment,
                request.Status,
                new
                {
                    providerStatus = request.Status,
                    transactionId = request.TransactionId,
                    amount = request.Amount
                });
            webhook.ProcessedAt = timeProvider.GetUtcNow();
            webhook.ProcessingResult = oldStatus == payment.Status
                ? "ProcessedNoStatusChange"
                : $"Processed:{oldStatus}->{payment.Status}";
            await systemAudit.RecordAsync(
                "PaymentCallbackProcessed",
                "Payment",
                payment.Id,
                $"Processed Alif callback for order '{payment.Order.OrderNumber}'.",
                new { status = oldStatus },
                new
                {
                    status = payment.Status,
                    providerStatus = request.Status,
                    callbackEventId = webhook.Id
                },
                cancellationToken);
            if (oldStatus != payment.Status)
            {
                await RecordStatusAuditAsync(payment, oldStatus, cancellationToken);
            }

            await SaveChangesAsync(cancellationToken);
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            if (oldStatus != payment.Status)
            {
                await NotifyStatusChangedSafelyAsync(payment, cancellationToken);
            }

            return new PaymentCallbackResult(true, false);
        }
        catch (DbUpdateException exception) when (IsWebhookDuplicate(exception))
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }

            dbContext.ChangeTracker.Clear();
            return new PaymentCallbackResult(false, true);
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }
    }

    public async Task<CustomerPaymentDto> SimulateDevelopmentStatusAsync(
        Guid paymentId,
        PaymentStatus status,
        CancellationToken cancellationToken)
    {
        if (status is not PaymentStatus.Paid and
            not PaymentStatus.Failed and
            not PaymentStatus.Cancelled and
            not PaymentStatus.Pending)
        {
            throw StatusConflict(
                "The requested Development payment status is not supported.");
        }

        var customerId = currentUser.GetRequiredCustomerId();
        var payment = await dbContext.Payments
            .Include(item => item.Order)
            .Include(item => item.Attempts)
            .SingleOrDefaultAsync(
                item => item.Id == paymentId &&
                        item.Provider == PaymentProvider.Development &&
                        item.Order.CustomerId == customerId,
                cancellationToken)
            ?? throw NotFound();

        var oldStatus = payment.Status;
        var providerStatus = status switch
        {
            PaymentStatus.Paid => "success",
            PaymentStatus.Failed => "failed",
            PaymentStatus.Cancelled => "cancelled",
            _ => "pending"
        };
        var failureReason = status switch
        {
            PaymentStatus.Failed => "Simulated payment failure.",
            PaymentStatus.Cancelled => "Simulated payment cancellation.",
            _ => null
        };
        var transactionId = $"DEV-{payment.Id:N}";

        ApplyProviderStatus(payment, transactionId, status, failureReason);
        UpdateAttempt(
            payment,
            providerStatus,
            new
            {
                provider = PaymentProvider.Development,
                providerStatus,
                transactionId,
                amount = payment.Amount
            });
        await systemAudit.RecordAsync(
            "DevelopmentPaymentSimulated",
            "Payment",
            payment.Id,
            $"Simulated payment status for order '{payment.Order.OrderNumber}'.",
            new { status = oldStatus },
            new { status = payment.Status, providerStatus },
            cancellationToken);
        if (oldStatus != payment.Status)
        {
            await RecordStatusAuditAsync(payment, oldStatus, cancellationToken);
        }

        await SaveChangesAsync(cancellationToken);
        await NotifyStatusChangedSafelyAsync(payment, cancellationToken);
        return ToCustomerDto(payment);
    }

    public async Task<bool> MarkRefundRequiredForRejectedOrderAsync(
        Order order,
        CancellationToken cancellationToken)
    {
        if (order.PaymentMethod != PaymentMethod.Online)
        {
            return false;
        }

        var payment = order.Payment ?? await dbContext.Payments
            .SingleOrDefaultAsync(item => item.OrderId == order.Id, cancellationToken);
        if (payment?.Status != PaymentStatus.Paid)
        {
            return false;
        }

        var oldStatus = payment.Status;
        payment.Status = PaymentStatus.RefundRequired;
        payment.FailureReason = payment.Provider == PaymentProvider.Development
            ? "A full refund is required. The Development payment simulator does not process refunds."
            : "A full refund is required, but the official Alif cancellation contract is not yet available. No provider refund was sent.";
        SynchronizeOrderPaymentCompatibility(payment);
        await employeeAudit.RecordAsync(
            "RefundRequired",
            "Payment",
            payment.Id,
            $"Recorded required refund for rejected order '{order.OrderNumber}'.",
            new { status = oldStatus },
            new
            {
                status = payment.Status,
                providerRequestSent = false,
                reason = payment.Provider == PaymentProvider.Development
                    ? "Development payment simulator does not process refunds"
                    : "Official Alif cancellation contract unavailable"
            },
            cancellationToken);
        return true;
    }

    private IPaymentProvider GetProvider(PaymentProvider provider)
    {
        return providers.SingleOrDefault(item => item.Provider == provider)
            ?? throw new ApiProblemException(
                StatusCodes.Status503ServiceUnavailable,
                "payment_provider_unavailable",
                "The payment provider is unavailable",
                "PAYMENT_PROVIDER_UNAVAILABLE");
    }

    private static void ValidateProviderResult(
        Payment payment,
        PaymentProviderStatusResult result)
    {
        if (!string.Equals(
                payment.ProviderOrderId,
                result.ProviderOrderId,
                StringComparison.Ordinal) ||
            payment.Amount != result.Amount)
        {
            throw StatusConflict("The provider status does not match the payment.");
        }

        EnsureTransactionIsCompatible(payment, result.TransactionId);
    }

    private static void EnsureTransactionIsCompatible(
        Payment payment,
        string transactionId)
    {
        if (payment.ProviderTransactionId is not null &&
            !string.Equals(
                payment.ProviderTransactionId,
                transactionId,
                StringComparison.Ordinal))
        {
            throw StatusConflict(
                "The payment is already associated with another provider transaction.");
        }
    }

    private void ApplyProviderStatus(
        Payment payment,
        string transactionId,
        PaymentStatus providerStatus,
        string? failureReason)
    {
        payment.ProviderTransactionId ??= transactionId;
        if (payment.Status is PaymentStatus.Refunded or
            PaymentStatus.RefundPending or PaymentStatus.RefundRequired)
        {
            return;
        }

        if (payment.Status == PaymentStatus.Paid &&
            providerStatus is PaymentStatus.Pending or PaymentStatus.Failed or
                PaymentStatus.Cancelled)
        {
            throw StatusConflict(
                "A completed payment cannot move to an unpaid provider status.");
        }

        payment.Status = providerStatus;
        payment.FailureReason = failureReason;
        if (providerStatus == PaymentStatus.Paid)
        {
            payment.PaidAt ??= timeProvider.GetUtcNow();
        }

        SynchronizeOrderPaymentCompatibility(payment);
    }

    private static void SynchronizeOrderPaymentCompatibility(Payment payment)
    {
        var isPaid = payment.Status == PaymentStatus.Paid;
        payment.Order.PaymentReceived = isPaid;
        payment.Order.PaymentReceivedAt = isPaid ? payment.PaidAt : null;
    }

    private static void UpdateAttempt(
        Payment payment,
        string providerStatus,
        object response)
    {
        var attempt = payment.Attempts
            .OrderByDescending(item => item.AttemptNumber)
            .FirstOrDefault();
        if (attempt is null)
        {
            return;
        }

        attempt.ProviderStatus = providerStatus;
        attempt.ResponseSnapshot = JsonSerializer.Serialize(
            response,
            SnapshotJsonOptions);
    }

    private async Task RecordStatusAuditAsync(
        Payment payment,
        PaymentStatus oldStatus,
        CancellationToken cancellationToken)
    {
        var action = payment.Status switch
        {
            PaymentStatus.Paid => "PaymentCompleted",
            PaymentStatus.Failed => "PaymentFailed",
            PaymentStatus.Cancelled => "PaymentCancelled",
            PaymentStatus.ReconciliationRequired => "PaymentReconciliationRequired",
            _ => "PaymentStatusChanged"
        };
        await systemAudit.RecordAsync(
            action,
            "Payment",
            payment.Id,
            $"Changed payment status for order '{payment.Order.OrderNumber}'.",
            new { status = oldStatus },
            new
            {
                status = payment.Status,
                paidAt = payment.PaidAt,
                failureReason = payment.FailureReason
            },
            cancellationToken);
    }

    private async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw StatusConflict(
                "The payment changed while the request was being processed.");
        }
    }

    private async Task NotifyStatusChangedSafelyAsync(
        Payment payment,
        CancellationToken cancellationToken)
    {
        try
        {
            await realtimeNotifier.PaymentStatusChangedAsync(
                payment.Order.CustomerId,
                ToRealtimeEvent(payment),
                cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(
                exception,
                "Failed to publish payment status update for {PaymentId}.",
                payment.Id);
        }
    }

    private OrderRealtimeEventDto ToRealtimeEvent(Payment payment)
    {
        var order = payment.Order;
        return new OrderRealtimeEventDto(
            Guid.NewGuid(),
            timeProvider.GetUtcNow(),
            order.Id,
            order.OrderNumber,
            order.Status,
            order.EstimatedReadyAt,
            order.RejectReason,
            order.PreparationStartedAt,
            order.ReadyAt,
            order.CompletedAt,
            order.PaymentReceived,
            order.PaymentMethodUsed,
            payment.Id,
            payment.Status,
            payment.PaidAt,
            payment.RefundedAt);
    }

    private static CustomerPaymentDto ToCustomerDto(Payment payment)
    {
        return new CustomerPaymentDto(
            payment.Id,
            payment.OrderId,
            payment.Status,
            payment.Amount,
            payment.Currency,
            payment.CreatedAt,
            payment.PaidAt,
            payment.RefundedAt,
            payment.FailureReason);
    }

    private static string HashHex(string value)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))
            .ToLowerInvariant();
    }

    private static bool IsWebhookDuplicate(DbUpdateException exception)
    {
        return exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "IX_PaymentWebhookEvents_Provider_EventIdentifier"
        };
    }

    private static ApiProblemException NotFound()
    {
        return new ApiProblemException(
            StatusCodes.Status404NotFound,
            "not_found",
            "Payment not found",
            "PAYMENT_NOT_FOUND");
    }

    private static ApiProblemException InvalidCallback(string detail)
    {
        return new ApiProblemException(
            StatusCodes.Status400BadRequest,
            "invalid_payment_callback",
            "Invalid payment callback",
            "INVALID_PAYMENT_CALLBACK",
            detail);
    }

    private static ApiProblemException StatusConflict(string detail)
    {
        return new ApiProblemException(
            StatusCodes.Status409Conflict,
            "payment_status_conflict",
            "Payment status conflict",
            "PAYMENT_STATUS_CONFLICT",
            detail);
    }
}
