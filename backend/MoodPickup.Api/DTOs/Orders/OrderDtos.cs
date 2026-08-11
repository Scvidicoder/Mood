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

public sealed class OrderListQuery : Menu.PaginationQuery;

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
    PaymentMethodUsed? PaymentMethodUsed);

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
    DateTimeOffset? EstimatedReadyAt,
    string? RejectReason,
    DateTimeOffset? PreparationStartedAt,
    DateTimeOffset? ReadyAt,
    DateTimeOffset? CompletedAt,
    bool PaymentReceived,
    PaymentMethodUsed? PaymentMethodUsed,
    IReadOnlyList<OrderStatusHistoryDto> StatusHistory,
    IReadOnlyList<OrderItemDto> Items);

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
    PaymentMethodUsed? PaymentMethodUsed);

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
