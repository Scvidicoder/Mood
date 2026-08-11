
# Notifications and SignalR

Version: 1.2 (Sprint 3.9 customer account consumers)

## Purpose

Define all real-time events and notification channels.

## Channels

- SignalR (real-time UI)
- Telegram
- Browser Notifications
- In-app notifications
- Sound (employees)

## Customer Events

| Event | SignalR | Telegram | Browser |
|---|---|---|---|
| Order confirmed | ✓ | ✓ | ✓ |
| Estimated time set | ✓ | ✓ | ✓ |
| Estimated time changed | ✓ | ✓ | ✓ |
| Preparing | ✓ | ✓ | ✓ |
| Ready for pickup | ✓ | ✓ | ✓ |
| Order rejected | ✓ | ✓ | ✓ |
| Refund started | ✓ | ✓ | ✓ |
| Refund completed | ✓ | ✓ | ✓ |

Ready reminder:
- Sent once after 15 minutes if order is still waiting.

## Employee Events

Reception:
- New order

Kitchen:
- Order confirmed

Pickup:
- Order ready

One sound per event. No repeating alarm.

## SignalR Hub

Endpoint:

/hubs/notifications

Groups:

customer:{customerId}
role:OrderReception
role:Kitchen
role:Pickup
staff:all

## Main Events

OrderCreated
OrderConfirmed
OrderPreparing
EstimatedReadyTimeChanged
OrderReady
OrderCompleted
OrderRejected
PaymentStatusChanged
RefundStatusChanged
DashboardCountersChanged
NotificationCreated

## Connection Rules

- Automatic reconnect
- Refresh stale data after reconnect
- Show connection status
- Never require manual page refresh

## Duplicate Prevention

Events contain:
- EventId
- Timestamp
- EntityId

Clients ignore duplicate EventIds.

## Notification Storage

Notifications are stored for later viewing.

Fields:

- Id
- UserId
- EmployeeId
- Type
- Title
- Message
- IsRead
- CreatedAt
- ReadAt

## Browser Notifications

Permission requested only after successful employee login.

## Telegram Messages

Customer messages are concise.

Example:

Order #152 is ready for pickup.

Estimated time updates always include both previous and new values.

## Security

Clients receive only events for authorized groups.

SignalR payloads never contain confidential data unrelated to the recipient.

## Sprint 3.8 implementation boundary

The implemented order events are `OrderConfirmed`, `OrderRejected`,
`OrderPreparing`, `EstimatedReadyTimeChanged`, `OrderReady`,
`PaymentStatusChanged`, and `OrderCompleted`. Each is sent to the authenticated
`customer:{customerId}` group and `staff:all`. Payloads contain `EventId`,
`Timestamp`, `EntityId`, order number/status, ETA, workflow timestamps,
rejection reason, and payment state. They contain no employee identity.

Customer and staff clients reconnect automatically, invalidate affected query
caches after reconnect, and ignore duplicate event IDs. Their HTTP polling is
disabled while SignalR is connected and runs only as a disconnected fallback.
Persisted notification inboxes, Telegram order messages, browser notifications,
and guaranteed outbox delivery remain future work.

## Sprint 3.9 customer account behavior

`/profile`, `/profile/orders`, and `/profile/orders/{id}` reuse the existing
authenticated customer group and order events; Sprint 3.9 adds no notification
channel or event type. Each accepted event invalidates the affected detail,
the filtered/paginated customer history queries, and profile order counts.
Polling remains disabled while connected and runs every 15 seconds only as a
fallback. Repeat-order validation is an explicit HTTP request and emits no
SignalR event.
