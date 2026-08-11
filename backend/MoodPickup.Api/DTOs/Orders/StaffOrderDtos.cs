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
    int ItemQuantity,
    Guid RowVersion);

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
    Guid RowVersion,
    IReadOnlyList<OrderItemDto> Items);

public sealed record ConfirmOrderRequest(
    DateTimeOffset EstimatedReadyTime,
    Guid RowVersion);

public sealed record RejectOrderRequest(
    string Reason,
    Guid RowVersion);

public sealed record UpdateEstimatedReadyTimeRequest(
    DateTimeOffset EstimatedReadyTime,
    Guid RowVersion);
