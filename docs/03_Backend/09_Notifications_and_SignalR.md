
# Notifications and SignalR

Version: 1.1 (Sprint 3.7 customer order events)

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

## Sprint 3.7 implementation boundary

Sprint 3.7 publishes `OrderConfirmed`, `OrderRejected`, and
`EstimatedReadyTimeChanged` to the authenticated `customer:{customerId}` group.
Payloads contain `EventId`, `Timestamp`, `EntityId`, order number/status,
estimated ready time, and optional rejection reason. They contain no employee
identity. Kitchen SignalR delivery, persisted notifications, Telegram order
messages, browser notifications, and later order statuses remain future work.
