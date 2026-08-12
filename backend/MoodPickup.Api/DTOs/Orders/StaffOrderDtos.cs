using MoodPickup.Api.DTOs.Payments;
using MoodPickup.Api.Entities;

namespace MoodPickup.Api.DTOs.Orders;

public sealed class StaffOrderListQuery : Menu.PaginationQuery
{
    public OrderStatus? Status { get; init; }
}

public sealed record StaffOrderSummaryDto(
    Guid Id,
    string OrderNumber,
    string CustomerName,
    string CustomerPhoneNumber,
    DateTimeOffset CreatedAt,
    PickupMode PickupMode,
    DateTimeOffset? RequestedPickupTime,
    PaymentMethod PaymentMethod,
    decimal Total,
    string Currency,
    string? Comment,
    OrderStatus Status,
    DateTimeOffset? EstimatedReadyAt,
    DateTimeOffset? PreparationStartedAt,
    DateTimeOffset? ReadyAt,
    DateTimeOffset? CompletedAt,
    bool PaymentReceived,
    PaymentMethodUsed? PaymentMethodUsed,
    int ItemQuantity,
    Guid RowVersion,
    PaymentStatus? OnlinePaymentStatus = null);

public sealed record StaffOrderDetailDto(
    Guid Id,
    string OrderNumber,
    string CustomerName,
    string CustomerPhoneNumber,
    OrderStatus Status,
    PaymentMethod PaymentMethod,
    PickupMode PickupMode,
    DateTimeOffset? RequestedPickupTime,
    DateTimeOffset? EstimatedReadyAt,
    string? Comment,
    string? RejectReason,
    decimal Subtotal,
    decimal DiscountTotal,
    decimal Total,
    string Currency,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ConfirmedAt,
    DateTimeOffset? RejectedAt,
    DateTimeOffset? PreparationStartedAt,
    DateTimeOffset? ReadyAt,
    DateTimeOffset? CompletedAt,
    bool PaymentReceived,
    PaymentMethodUsed? PaymentMethodUsed,
    Guid RowVersion,
    IReadOnlyList<OrderStatusHistoryDto> StatusHistory,
    IReadOnlyList<OrderItemDto> Items,
    StaffPaymentDto? Payment = null);

public sealed record ConfirmOrderRequest(
    DateTimeOffset EstimatedReadyTime,
    Guid RowVersion);

public sealed record RejectOrderRequest(
    string Reason,
    Guid RowVersion);

public sealed record UpdateEstimatedReadyTimeRequest(
    DateTimeOffset EstimatedReadyTime,
    Guid RowVersion);

public sealed class KitchenOrderListQuery : Menu.PaginationQuery
{
    public OrderStatus? Status { get; init; }

    public DateTimeOffset? CreatedFrom { get; init; }

    public DateTimeOffset? CreatedTo { get; init; }

    public DateTimeOffset? PickupFrom { get; init; }

    public DateTimeOffset? PickupTo { get; init; }

    public string? OrderNumber { get; init; }
}

public sealed record KitchenOrderDto(
    Guid Id,
    string OrderNumber,
    string CustomerName,
    string CustomerPhoneNumber,
    DateTimeOffset CreatedAt,
    PickupMode PickupMode,
    DateTimeOffset? RequestedPickupTime,
    DateTimeOffset? EstimatedReadyAt,
    DateTimeOffset? PreparationStartedAt,
    DateTimeOffset? ReadyAt,
    OrderStatus Status,
    PaymentMethod PaymentMethod,
    bool PaymentReceived,
    PaymentMethodUsed? PaymentMethodUsed,
    decimal Total,
    string Currency,
    string? Comment,
    int ItemQuantity,
    Guid RowVersion,
    IReadOnlyList<OrderItemDto> Items);

public sealed record OrderVersionRequest(Guid RowVersion);

public sealed record RecordPaymentRequest(
    PaymentMethodUsed? PaymentMethodUsed,
    Guid RowVersion);
