using MoodPickup.Api.DTOs.Payments;
using MoodPickup.Api.Entities;

namespace MoodPickup.Api.DTOs.Orders;

public sealed record CreateOrderRequest(
    IReadOnlyList<CreateOrderItemRequest> Items,
    string? Comment,
    PaymentMethod PaymentMethod,
    PickupMode PickupMode,
    DateTimeOffset? RequestedPickupTime);

public sealed record CreateOrderItemRequest(
    Guid ProductId,
    IReadOnlyList<Guid> OptionValueIds,
    int Quantity,
    string? Comment);

public sealed record PickupSlotsDto(
    bool SupportsAsap,
    DateOnly Date,
    int IntervalMinutes,
    IReadOnlyList<PickupSlotDto> Slots);

public sealed record PickupSlotDto(
    string Label,
    DateTimeOffset StartsAt);

public sealed class OrderListQuery : Menu.PaginationQuery
{
    public CustomerOrderFilter Filter { get; init; } = CustomerOrderFilter.All;

    public string? Search { get; init; }
}

public enum CustomerOrderFilter
{
    All,
    Active,
    Completed,
    Cancelled,
    Rejected
}

public sealed record OrderSummaryDto(
    Guid Id,
    string OrderNumber,
    OrderStatus Status,
    PaymentMethod PaymentMethod,
    PickupMode PickupMode,
    DateTimeOffset? RequestedPickupTime,
    decimal Total,
    string Currency,
    int ItemQuantity,
    DateTimeOffset CreatedAt,
    DateTimeOffset? EstimatedReadyAt,
    string? RejectReason,
    DateTimeOffset? PreparationStartedAt,
    DateTimeOffset? ReadyAt,
    DateTimeOffset? CompletedAt,
    bool PaymentReceived,
    PaymentMethodUsed? PaymentMethodUsed,
    PaymentStatus? OnlinePaymentStatus = null,
    DateTimeOffset? PaidAt = null,
    DateTimeOffset? RefundedAt = null);

public sealed record OrderDetailDto(
    Guid Id,
    string OrderNumber,
    OrderStatus Status,
    PaymentMethod PaymentMethod,
    PickupMode PickupMode,
    DateTimeOffset? RequestedPickupTime,
    string? Comment,
    decimal Subtotal,
    decimal DiscountTotal,
    decimal Total,
    string Currency,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ConfirmedAt,
    DateTimeOffset? RejectedAt,
    DateTimeOffset? EstimatedReadyAt,
    string? RejectReason,
    DateTimeOffset? PreparationStartedAt,
    DateTimeOffset? ReadyAt,
    DateTimeOffset? CompletedAt,
    bool PaymentReceived,
    PaymentMethodUsed? PaymentMethodUsed,
    DateTimeOffset? PaymentReceivedAt,
    IReadOnlyList<OrderStatusHistoryDto> StatusHistory,
    IReadOnlyList<OrderItemDto> Items,
    CustomerPaymentDto? Payment = null,
    PaymentLaunchResponse? PaymentLaunch = null);

public sealed record OrderRealtimeEventDto(
    Guid EventId,
    DateTimeOffset Timestamp,
    Guid EntityId,
    string OrderNumber,
    OrderStatus Status,
    DateTimeOffset? EstimatedReadyAt,
    string? RejectReason,
    DateTimeOffset? PreparationStartedAt,
    DateTimeOffset? ReadyAt,
    DateTimeOffset? CompletedAt,
    bool PaymentReceived,
    PaymentMethodUsed? PaymentMethodUsed,
    Guid? PaymentId = null,
    PaymentStatus? PaymentStatus = null,
    DateTimeOffset? PaidAt = null,
    DateTimeOffset? RefundedAt = null);

public sealed record OrderStatusHistoryDto(
    OrderStatus? OldStatus,
    OrderStatus NewStatus,
    DateTimeOffset Timestamp,
    string? Reason);

public sealed record OrderItemDto(
    Guid ProductId,
    string ProductName,
    bool IsAvailableAtPurchase,
    decimal BasePrice,
    decimal FinalPrice,
    int? Calories,
    int? VolumeMilliliters,
    int? WeightGrams,
    int Quantity,
    string? Comment,
    IReadOnlyList<OrderItemOptionDto> Options);

public sealed record OrderItemOptionDto(
    string OptionGroupName,
    string OptionValueName,
    decimal PriceModifier,
    int? CaloriesModifier,
    int? VolumeModifier,
    int DisplayOrder);

public sealed record RepeatOrderResultDto(
    string SourceOrderNumber,
    IReadOnlyList<RepeatOrderItemDto> AvailableItems,
    IReadOnlyList<RepeatOrderIssueDto> UnavailableItems);

public sealed record RepeatOrderItemDto(
    Guid ProductId,
    string ProductName,
    decimal BasePrice,
    decimal UnitPrice,
    string Currency,
    int Quantity,
    IReadOnlyList<RepeatOrderOptionDto> Options);

public sealed record RepeatOrderOptionDto(
    Guid ProductOptionGroupId,
    string OptionGroupName,
    Guid OptionValueId,
    string OptionValueName,
    decimal PriceModifier,
    int? VolumeMilliliters,
    int? Calories);

public sealed record RepeatOrderIssueDto(
    string ProductName,
    int Quantity,
    IReadOnlyList<string> Reasons);
