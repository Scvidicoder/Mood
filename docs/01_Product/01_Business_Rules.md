
# Business Rules

Version: 1.0 (Draft)

## Purpose

This document defines the business rules that govern the Mood Pickup System.
Every implementation decision (database, API, frontend and backend logic) must comply with these rules.

---

# Authentication

## BR-001 – Customer authentication
Customers authenticate using their phone number and a one-time verification code sent through the linked Telegram bot.

## BR-002 – No customer password
Customers do not have passwords. Every login requires a new verification code.

For a new or unlinked number, the customer must open the backend-provided
Telegram deep link and share their own contact through Telegram. Typed or
forwarded phone numbers are not proof of ownership. The shared normalized
number must match the website challenge. One Telegram identity cannot be
silently linked to different customer phone numbers.

## BR-003 – Employee authentication
Employees authenticate using username and password.

## BR-004 – First administrator
The first administrator account is created from seed data during application startup.

---

# Orders

## BR-005 – Order lifecycle
Orders must follow this lifecycle:

PendingConfirmation
→ Confirmed
→ Preparing
→ ReadyForPickup
→ Completed

## BR-006 – Statuses cannot be skipped
Orders must always move through every stage sequentially.

## BR-007 – No workflow rollback in Sprint 3.8

Sprint 3.8 supports forward transitions only. Preparation, ready, and
completion actions cannot be skipped, repeated, or rolled back.

## BR-008 – Customer cancellation
Sprint 3.7 customers may cancel an order only while it is
`PendingConfirmation`. Once confirmed, customer cancellation is forbidden.

## BR-009 – Cafe rejection
The café may reject an order only while it is `PendingConfirmation`.
Rejecting a confirmed order is forbidden. A rejection reason is mandatory.

## BR-010 – Estimated ready time
Estimated ready time is assigned at confirmation and may be changed by order
staff while Confirmed or by Kitchen/Administrator while Confirmed or Preparing.
It must be later than now, today in the café time zone, and inside working
hours. ETA cannot change after the order becomes ready.

---

# Payments

## BR-011 – Payment methods
Supported payment methods:

- Online
- Pay on Pickup

## BR-012 – Online payment
Sprint 3.8 assumes Online orders are already paid. No payment provider is
called and no gateway transaction is stored.

## BR-013 – Refund boundary
Refunds are outside Sprint 3.8. Cancellation or rejection does not call a
payment provider.

## BR-014 – Pickup payment
Orders with "Pay on Pickup" cannot be completed until payment is marked as received.

## BR-015 – On-site payment type
For pickup payments the employee must record:

- Cash
- Card

---

# Menu

## BR-016 – Product availability
Availability is managed independently for:

- Product
- Size
- Milk option
- Syrup
- Any other option

## BR-017 – Product options
Products may contain configurable option groups.

Examples:

- Size
- Milk
- Syrups

## BR-018 – Shared options
Option groups and option values are shared between products.
Each product selects only the values it supports.

## BR-019 – Product snapshots
Orders always store a snapshot of product data at purchase time
(name, price and selected options).

---

# Notifications

## BR-020 – Customer notifications
Customers receive Telegram notifications for significant order events.

## BR-021 – Employee notifications
Notifications are sent according to employee roles.

Example:

- New order → Order Reception
- Confirmed order → Kitchen
- Ready order → Pickup

---

# Audit

## BR-022 – Audit logging
Every important action is recorded, including:

- status changes
- payment changes
- menu changes
- employee management
- café settings

Each record stores:
- employee
- timestamp
- action
- target entity
- description

---

# Customer Account

## BR-023 – Editable customer data

An authenticated customer may edit only their trimmed display name in Sprint
3.9. Phone-number and Telegram-link changes require future dedicated secure
flows and are read-only in the profile.

## BR-024 – Customer ownership

Profile, order list, order detail, and repeat-order reads are derived from the
validated customer subject. Another customer's order identifier returns not
found and never exposes customer, employee, audit, or internal attribution
data.

## BR-025 – Customer order history

Order history is newest first, paginated, filterable by lifecycle group, and
searchable by order number or immutable product-name snapshot.

## BR-026 – Repeat order validation

Repeating an order validates its historical product and selected-option
identifiers against the current menu. Current prices are used. A missing,
hidden, deleted, unavailable, incompatible, or newly invalid configuration is
reported before any cart change. Invalid lines are not added, and the system
never silently removes an option or substitutes a product or option.

## BR-027 – Repeat order destination

Repeat order validation does not create an order. After the customer reviews
the result, only explicitly available lines are added to the anonymous local
cart and normal checkout revalidation remains authoritative.
