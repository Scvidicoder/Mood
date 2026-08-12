using MoodPickup.Api.Entities;

namespace MoodPickup.Api.Services;

internal static class AlifStatusMapper
{
    public static bool TryMap(
        string providerStatus,
        out PaymentStatus status,
        out string? failureReason)
    {
        failureReason = null;
        switch (providerStatus.Trim().ToLowerInvariant())
        {
            case "ok":
                status = PaymentStatus.Paid;
                return true;
            case "failed":
                status = PaymentStatus.Failed;
                failureReason = "The payment provider reported a failed payment.";
                return true;
            case "pending":
                status = PaymentStatus.Pending;
                return true;
            case "canceled":
                status = PaymentStatus.Cancelled;
                failureReason = "The payment provider reported a cancelled payment.";
                return true;
            case "partially_canceled":
                status = PaymentStatus.ReconciliationRequired;
                failureReason =
                    "Alif reported a partial cancellation. MoodPickup does not treat it as a full refund and administrator reconciliation is required.";
                return true;
            default:
                status = PaymentStatus.ReconciliationRequired;
                failureReason =
                    "Alif returned an unknown payment status. Administrator reconciliation is required.";
                return false;
        }
    }
}
