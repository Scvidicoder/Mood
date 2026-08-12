using MoodPickup.Api.DTOs.Orders;
using MoodPickup.Api.DTOs.Payments;
using MoodPickup.Api.Entities;

namespace MoodPickup.Api.Services;

internal static class OrderDtoMapper
{
    public static OrderDetailDto ToCustomerDetail(
        Order order,
        PaymentLaunchResponse? paymentLaunch = null)
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
            order.ConfirmedAt,
            order.RejectedAt,
            order.EstimatedReadyAt,
            order.RejectReason,
            order.PreparationStartedAt,
            order.ReadyAt,
            order.CompletedAt,
            order.PaymentReceived,
            order.PaymentMethodUsed,
            order.PaymentReceivedAt,
            ToStatusHistory(order),
            ToItems(order),
            order.Payment is null ? null : ToCustomerPayment(order.Payment),
            paymentLaunch);
    }

    public static CustomerPaymentDto ToCustomerPayment(Payment payment)
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

    public static StaffPaymentDto ToStaffPayment(Payment payment)
    {
        return new StaffPaymentDto(
            payment.Provider,
            payment.Status,
            payment.ProviderTransactionId,
            payment.Amount,
            payment.Currency,
            payment.PaidAt,
            payment.RefundedAt,
            payment.FailureReason);
    }

    public static IReadOnlyList<OrderStatusHistoryDto> ToStatusHistory(Order order)
    {
        return order.StatusHistory
            .OrderBy(history => history.Timestamp)
            .ThenBy(history => history.Id)
            .Select(history => new OrderStatusHistoryDto(
                history.OldStatus,
                history.NewStatus,
                history.Timestamp,
                history.Reason))
            .ToArray();
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
