
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

## BR-007 – Allowed rollback
Only these rollbacks are allowed:

Preparing → Confirmed
ReadyForPickup → Preparing
Completed → ReadyForPickup

Rollback requires a reason and is written to the audit log.

## BR-008 – Customer cancellation
Sprint 3.7 customers may cancel an order only while it is
`PendingConfirmation`. Once confirmed, customer cancellation is forbidden.

## BR-009 – Cafe rejection
The café may reject an order only while it is `PendingConfirmation`.
Rejecting a confirmed order is forbidden. A rejection reason is mandatory.

## BR-010 – Estimated ready time
In Sprint 3.7, estimated ready time is assigned or changed by Administrator,
Cashier, or Manager order staff. Confirmation requires the time to be later
than now, today in the café time zone, and inside working hours.

---

# Payments

## BR-011 – Payment methods
Supported payment methods:

- Online
- Pay on Pickup

## BR-012 – Online payment
Online payment is completed immediately after checkout.

## BR-013 – Automatic refund
If an online order is cancelled by the customer while pending confirmation or rejected by the café,
a full refund is initiated automatically.

## BR-014 – Pickup payment
Orders with "Pay on Pickup" cannot be completed until payment is marked as received.

## BR-015 – On-site payment type
For pickup payments the employee must record:

- Cash
- Bank Card

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
