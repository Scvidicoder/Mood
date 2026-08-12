using System.Text.Json.Serialization;
using MoodPickup.Api.Entities;

namespace MoodPickup.Api.DTOs.Payments;

public sealed record PaymentLaunchResponse(
    Guid PaymentId,
    string ActionUrl,
    string Method,
    IReadOnlyDictionary<string, string> FormFields);

public sealed record CustomerPaymentDto(
    Guid Id,
    Guid OrderId,
    PaymentStatus Status,
    decimal Amount,
    string Currency,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PaidAt,
    DateTimeOffset? RefundedAt,
    string? FailureReason);

public sealed record StaffPaymentDto(
    PaymentProvider Provider,
    PaymentStatus Status,
    string? TransactionId,
    decimal Amount,
    string Currency,
    DateTimeOffset? PaidAt,
    DateTimeOffset? RefundedAt,
    string? FailureReason);

public sealed record PaymentCallbackResult(bool Processed, bool Duplicate);

public sealed class AlifCallbackRequest
{
    public string OrderId { get; init; } = string.Empty;

    public string TransactionId { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public string Token { get; init; } = string.Empty;

    public decimal Amount { get; init; }

    public string Account { get; init; } = string.Empty;

    [JsonPropertyName("transaction_type")]
    public string? TransactionType { get; init; }
}

internal sealed record AlifStatusCheckRequest(
    [property: JsonPropertyName("orderId")] string OrderId,
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("token")] string Token);

internal sealed class AlifStatusResponse
{
    public string OrderId { get; init; } = string.Empty;

    public string TransactionId { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public string Token { get; init; } = string.Empty;

    public decimal Amount { get; init; }

    public string? Phone { get; init; }

    public string? Account { get; init; }
}
