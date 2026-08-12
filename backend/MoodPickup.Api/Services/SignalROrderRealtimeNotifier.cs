using Microsoft.AspNetCore.SignalR;
using MoodPickup.Api.DTOs.Orders;
using MoodPickup.Api.Hubs;
using MoodPickup.Api.Interfaces;

namespace MoodPickup.Api.Services;

public sealed class SignalROrderRealtimeNotifier(
    IHubContext<NotificationsHub> hubContext) : IOrderRealtimeNotifier
{
    public Task OrderConfirmedAsync(
        Guid customerId,
        OrderRealtimeEventDto notification,
        CancellationToken cancellationToken)
    {
        return SendAsync(
            customerId,
            "OrderConfirmed",
            notification,
            cancellationToken);
    }

    public Task OrderRejectedAsync(
        Guid customerId,
        OrderRealtimeEventDto notification,
        CancellationToken cancellationToken)
    {
        return SendAsync(
            customerId,
            "OrderRejected",
            notification,
            cancellationToken);
    }

    public Task EstimatedReadyTimeChangedAsync(
        Guid customerId,
        OrderRealtimeEventDto notification,
        CancellationToken cancellationToken)
    {
        return SendAsync(
            customerId,
            "EstimatedReadyTimeChanged",
            notification,
            cancellationToken);
    }

    public Task OrderPreparingAsync(
        Guid customerId,
        OrderRealtimeEventDto notification,
        CancellationToken cancellationToken)
    {
        return SendAsync(customerId, "OrderPreparing", notification, cancellationToken);
    }

    public Task OrderReadyAsync(
        Guid customerId,
        OrderRealtimeEventDto notification,
        CancellationToken cancellationToken)
    {
        return SendAsync(customerId, "OrderReady", notification, cancellationToken);
    }

    public Task PaymentStatusChangedAsync(
        Guid customerId,
        OrderRealtimeEventDto notification,
        CancellationToken cancellationToken)
    {
        return SendAsync(
            customerId,
            "PaymentStatusChanged",
            notification,
            cancellationToken);
    }

    public Task RefundStatusChangedAsync(
        Guid customerId,
        OrderRealtimeEventDto notification,
        CancellationToken cancellationToken)
    {
        return SendAsync(
            customerId,
            "RefundStatusChanged",
            notification,
            cancellationToken);
    }

    public Task OrderCompletedAsync(
        Guid customerId,
        OrderRealtimeEventDto notification,
        CancellationToken cancellationToken)
    {
        return SendAsync(customerId, "OrderCompleted", notification, cancellationToken);
    }

    private Task SendAsync(
        Guid customerId,
        string eventName,
        OrderRealtimeEventDto notification,
        CancellationToken cancellationToken)
    {
        return Task.WhenAll(
            hubContext.Clients
                .Group($"customer:{customerId}")
                .SendAsync(eventName, notification, cancellationToken),
            hubContext.Clients
                .Group("staff:all")
                .SendAsync(eventName, notification, cancellationToken));
    }
}
