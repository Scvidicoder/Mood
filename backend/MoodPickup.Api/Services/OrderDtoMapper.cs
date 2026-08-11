using MoodPickup.Api.DTOs.Orders;
using MoodPickup.Api.Entities;

namespace MoodPickup.Api.Services;

internal static class OrderDtoMapper
{
    public static OrderDetailDto ToCustomerDetail(Order order)
    {
        return new OrderDetailDto(
            order.Id,
            order.OrderNumber,
            order.Status,
            order.PaymentMethod,
            order.PickupMode,
            order.RequestedPickupTime,
            order.Comment,
            order.Subtotal,
            order.DiscountTotal,
            order.Total,
            order.Currency,
            order.CreatedAt,
            order.EstimatedReadyAt,
            order.RejectReason,
            ToItems(order));
    }

    public static IReadOnlyList<OrderItemDto> ToItems(Order order)
    {
        return order.Items
            .OrderBy(item => item.Id)
            .Select(item => new OrderItemDto(
                item.ProductId,
                item.ProductName,
                item.IsAvailableAtPurchase,
                item.BasePrice,
                item.FinalPrice,
                item.Calories,
                item.VolumeMilliliters,
                item.WeightGrams,
                item.Quantity,
                item.Comment,
                item.Options
                    .OrderBy(option => option.DisplayOrder)
                    .ThenBy(option => option.OptionGroupName)
                    .Select(option => new OrderItemOptionDto(
                        option.OptionGroupName,
                        option.OptionValueName,
                        option.PriceModifier,
                        option.CaloriesModifier,
                        option.VolumeModifier,
                        option.DisplayOrder))
                    .ToArray()))
            .ToArray();
    }
}
