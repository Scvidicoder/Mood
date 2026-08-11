using MoodPickup.Api.DTOs.Orders;

namespace MoodPickup.Api.Interfaces;

public interface IOrderRealtimeNotifier
{
    Task OrderConfirmedAsync(
        Guid customerId,
        OrderRealtimeEventDto notification,
        CancellationToken cancellationToken);

    Task OrderRejectedAsync(
        Guid customerId,
        OrderRealtimeEventDto notification,
        CancellationToken cancellationToken);

    Task EstimatedReadyTimeChangedAsync(
        Guid customerId,
        OrderRealtimeEventDto notification,
        CancellationToken cancellationToken);
}
