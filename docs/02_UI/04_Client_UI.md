
# Client UI Specification

Version: 1.2 (Sprint 3.5)

## 1. Purpose

This document defines the customer-facing interface of Mood Pickup System.

The customer interface is mobile-first and optimized for fast one-handed ordering.

Sprint 3.5 implements `/`, `/product/{id}`, and `/cart`: anonymous browsing,
search, category navigation/filtering, interactive option selection, dynamic
configured prices, and a persistent device-local cart. Checkout, backend order
creation, cafe operating status, payments, and order history remain planned.

## 2. Navigation Structure

Implemented customer routes:

- `/`
- `/product/{id}`
- `/cart`
- `/profile`
- `/login`
- `/verify`
- `/register`

Planned later routes include `/checkout`, `/orders/{id}`, and
`/profile/orders`.

Current global navigation:

- Mood text mark
- Menu
- Cart with total-item badge
- Profile
- Staff entry

---

# 3. Home and Menu Screen

## 3.1 Goal

Allow the customer to immediately browse the current public product catalog.
Customers can configure a product and build a local draft cart. No backend
order is created.

## 3.2 Layout

The customer menu screen contains:

1. Compact Mood banner
2. Search field
3. Sticky horizontal category navigation
4. Product sections
5. Product cards linked to details

Café status and address/contact configuration are deferred until their backend
contracts exist. Cart access is available globally and needs no backend
contract in Sprint 3.5.

## 3.3 Café Status

Possible states:

- Open
- Closed
- Temporarily not accepting orders

When ordering is unavailable, menu browsing remains possible, but ordering actions are disabled.

## 3.4 Category Navigation

Categories appear in manual display order.

Selecting a category updates the `category` query parameter, deep-links to the
corresponding section, and scrolls smoothly after the server-filtered results
load. Clearing the filter restores all grouped sections. Browser back/forward
restores category and search state.

When all categories are visible, the active category updates while the
customer scrolls.

## 3.5 Product Card

Each card displays:

- Image
- Name
- Short description
- Weight or volume
- Calories
- Price or "from" price
- Availability
- Orderability state and structured availability messages
- Details link

Cards remain focused on browsing and link to details rather than duplicating
the configurator. Product availability/orderability comes from the public API;
the frontend does not reproduce backend orderability.

## 3.6 States

Loading:
- Skeleton cards
- Skeleton categories

Empty:
- Search-specific or category-specific empty guidance

Error:
- Retry button
- Short error message

Unavailable product:
- Card remains visible
- Availability label shown

---

# 4. Product Details Screen

## 4.1 Goal

Allow the customer to inspect and configure a product using the current public
menu contract.

## 4.2 Displayed Data

- Product image
- Name
- Description
- Ingredients
- Base price
- "From" price
- Weight or volume
- Calories
- Option groups
- default and unavailable value markers
- price, calorie, and volume metadata
- real radio or checkbox controls
- configured unit price
- configured measurement/calorie value when one selected public value supplies
  an unambiguous explicit value
- structured local validation
- Add to cart or Save cart changes action

## 4.3 Option Group Presentation

Single-selection groups use radio semantics. Choosing a value replaces the
previous selection. Optional single groups with a zero minimum include an
explicit "No option" choice.

Multiple-selection groups use checkboxes. The UI allows deselection, blocks a
new choice at the configured maximum, and explains the limit. Required and
minimum constraints remain visible until satisfied. Unavailable values remain
visible but disabled.

## 4.4 Defaults, validation, and price

Only available backend defaults are selected. Contradictory defaults produce a
structured warning instead of an invented replacement, and Add to cart remains
disabled until the customer makes a valid choice.

The configured unit price is the base product price plus current selected
option price modifiers. Frontend calculation uses integer TJS minor units.
This value is a local preview only; Sprint 3.6 must recalculate it on the
backend.

## 4.5 Mobile Behavior

The dedicated route uses a one-column layout with touch-friendly fields and a
full-width action on mobile. Tablet and desktop expand to side-by-side
media/details and a multi-column option layout.

---

# 5. Cart Screen

## 5.1 Goal

Allow the customer to review and edit the current order draft.

## 5.2 Cart Item

Each item displays:

- Product name
- Selected options
- Unit price
- Quantity
- Item total
- Edit action
- Remove action

Completely identical configurations are merged into one line.

## 5.3 Available Actions

- Increase quantity
- Decrease quantity
- Edit configuration
- Remove item
- Clear cart
- Continue browsing
- Read the explicit Sprint 3.6 checkout boundary

## 5.4 Price Summary

Display:

- Configured unit price
- Line total
- Cart subtotal
- Total item quantity

## 5.5 Cart ownership and persistence

The Sprint 3.5 cart is anonymous frontend state owned by Redux Toolkit. It is
not associated with the authenticated customer and is not sent to or stored by
the backend.

The cart is persisted under `moodpickup.cart.v1` using a versioned,
currency-scoped schema. Only public product/option identifiers, safe display
snapshots, integer price snapshots, quantity, and timestamps are stored.
Media URLs are refreshed from the public API and are not persisted.
Authentication tokens, CSRF values, customer identity, employee data,
administrative metadata, media storage keys, and physical paths are forbidden.

Malformed JSON, unsupported versions/currencies, invalid quantities, missing
fields, and duplicate configurations are sanitized or reset without crashing.
Storage read/write failure keeps the current in-memory cart usable and shows a
non-blocking warning.

## 5.6 States

Empty:
- "Your cart is empty."
- Return to menu button

Changed availability:
- Warning shown above affected item
- Checkout disabled until resolved

Changed price:
- Current price is shown
- Snapshot is refreshed from current public detail
- Customer is informed

Restored cart lines use:

- Current
- Updated
- Needs attention
- Unavailable
- Checking while non-blocking revalidation is in progress

Products are deduplicated before detail lookup. Up to four unique uncached
details are requested concurrently and the normal TanStack Query product-detail
cache is populated/reused. Product removal, product availability/orderability,
missing/unavailable values, changed constraints, changed names/images, and
changed prices are explained per line. Invalid lines are never silently
removed or replaced.

## 5.7 Edit configuration

Edit opens the shared product configurator with the cart line's public option
IDs prefilled. Saving updates the same line. If the edited canonical
configuration matches another line, quantities merge.

## 5.8 Quantity and totals

Quantity is a positive integer from 1 through the client safety limit of 99.
Decreasing one removes the line. The limit is a local safety choice, not a
backend business guarantee. Line total is configured unit price times quantity;
subtotal is the sum of line totals.

## 5.9 Checkout boundary

The cart contains informational Sprint 3.6 guidance, not a functional checkout
button. No order, pickup selection, customer form, comment, payment, or stock
reservation is created in Sprint 3.5.

---

# 6. Authentication Screens

## 6.1 Phone Entry

Fields:

- Phone number

Actions:

- Get code in Telegram

## 6.2 Telegram Linking

Route: `/login/telegram`

When linking is required, the page uses the exact backend-provided `https://t.me`
URL and presents three steps: open the bot, press Start, and share the contact
using Telegram's button. It shows the challenge countdown, polls protected
challenge status every 2.5 seconds, permits reopening Telegram, and exposes
retry/resend only when appropriate. `OtpSent` navigates to `/verify`; expired
or locked challenges stop polling. Refreshing with no in-memory route state
returns safely to phone entry.

## 6.3 Telegram Verification

Fields:

- Six-digit code

Display:

- Code expiration
- Resend countdown
- Error attempts

## 6.4 First Registration

After successful verification, new customers enter:

- Name

Phone number is already verified.

## 6.5 Errors

- Invalid phone
- Invalid or expired Telegram link
- Contact phone mismatch
- Telegram identity conflict
- Telegram delivery unavailable
- Invalid code
- Expired code
- Too many attempts
- Too many resend requests

---

# 7. Checkout Screen

## 7.1 Goal

Allow the customer to confirm all order details before creation.

## 7.2 Customer Information

Read-only:

- Name
- Phone number

The customer changes this data from the profile, not during checkout.

## 7.3 Pickup Time

Options:

- As soon as possible
- Select pickup time

Pickup time rules:

- Today only
- Up to four hours ahead
- Fifteen-minute intervals
- Only during café working hours

For ASAP:

- Display approximate standard preparation information
- Explain that an employee will confirm the time

## 7.4 Payment Method

Options:

- Online
- Pay on pickup

The selected method cannot be changed after order creation.

## 7.5 Comment

One optional comment for the entire order.

## 7.6 Order Review

Display:

- Items
- Options
- Quantity
- Unit price
- Option additions
- Total price
- Pickup selection
- Payment method
- Comment

## 7.7 Final Confirmation

Before creating the order, show a confirmation section with:

- Back and edit
- Confirm order

For online payment, confirmation starts the payment flow.

For pay on pickup, confirmation creates the order immediately.

---

# 8. Online Payment Screen

## 8.1 Goal

Complete payment through an abstract payment provider.

## 8.2 States

- Redirecting
- Waiting for confirmation
- Paid successfully
- Payment failed
- Payment cancelled
- Payment timeout

The customer must not create duplicate payments by refreshing the page.

## 8.3 Successful Result

After successful payment, redirect to the order details page.

---

# 9. Order Details Screen

## 9.1 Goal

Show the current state of one order in real time.

## 9.2 Main Information

- Public order number
- Current status
- Progress indicator
- Requested pickup time
- Estimated ready time
- Actual ready time
- Payment method
- Payment status
- Refund status
- Items
- Total price
- Comment
- Café rejection reason
- Last status update time

## 9.3 Order Progress

Display customer-friendly statuses:

- Waiting for confirmation
- Confirmed
- Preparing
- Ready for pickup
- Completed

Terminal alternatives:

- Cancelled
- Rejected by café

## 9.4 Real-Time Updates

SignalR updates:

- Status
- Estimated ready time
- Payment state
- Refund state

The screen updates without refresh.

## 9.5 Cancellation

The Cancel button is available only while the order is Pending Confirmation.
It disappears once the café confirms or rejects the order.

Before cancellation, display confirmation.

Online-paid orders show that a full refund will be initiated.

## 9.6 Time Changes

When the café changes estimated time, show a visible message:

- Previous time
- New time
- Update timestamp

## 9.7 Ready State

When Ready for Pickup:

- Strong visual highlight
- Pickup instructions
- Café address
- Telegram notification confirmation

---

# 10. Current Orders Screen

## 10.1 Goal

Show all non-terminal orders.

Each order card displays:

- Order number
- Status
- Estimated ready time
- Total
- Created date
- Open details action

Multiple active orders are allowed.

---

# 11. Order History Screen

## 11.1 Goal

Show completed, cancelled and rejected orders.

Each card displays:

- Order number
- Date
- Final status
- Total
- Repeat order button
- View details button

## 11.2 Repeat Order

The system checks current product and option availability.

Rules:

- Unavailable products are not added
- Unavailable optional values are removed
- Unavailable required values are replaced by defaults when possible
- Current prices are always used
- Customer sees a summary of all changes

The repeated order is added to the cart, not created immediately.

---

# 12. Profile Screen

## 12.1 Displayed Data

- Name
- Phone number

## 12.2 Actions

- Edit name
- Change phone number
- View current orders
- View order history
- Logout

## 12.3 Change Phone Number

Flow:

1. Enter new phone number
2. Verify through Telegram
3. Replace old phone number
4. Keep order history and account data

---

# 13. Notifications

Customer notification channels:

- In-app
- Browser notification
- Telegram

Important events:

- Order confirmed
- Estimated time assigned
- Estimated time changed
- Preparing
- Ready for pickup
- Order rejected
- Refund started
- Refund completed
- Ready order reminder

The ready reminder is sent once after fifteen minutes.

---

# 14. Responsive Design

## Mobile

- Primary target
- One-column layout
- Sticky actions
- Large controls
- Minimum touch target: 44x44 px
- Category navigation remains accessible

## Tablet

- Wider cards
- Two-column menu where space allows

## Desktop

- Wider content container
- Multi-column product grid
- Cart summary may remain visible beside checkout content

---

# 15. Accessibility

Requirements:

- Keyboard navigation
- Visible focus states
- Semantic labels
- Sufficient contrast
- No information communicated by color alone
- Form errors connected to inputs
- Text alternatives for meaningful images

---

# 16. Common UI States

Every customer screen must support:

- Loading
- Empty
- Error
- Offline or connection lost
- Unauthorized
- Forbidden
- Stale data warning

SignalR disconnection must not block basic use. The interface reconnects
automatically and falls back to 15-second API refresh on Sprint 3.7 order
tracking and My Orders pages. Duplicate event IDs are ignored.

---

# 17. Design Direction

Mood Pickup should feel:

- Modern
- Calm
- Premium but approachable
- Photo-driven
- Fast
- Clear

Avoid:

- Overloaded navigation
- Excessive modal dialogs
- Dense tables
- Large blocks of text
- Decorative animation that slows ordering

The final design must use Mood's approved brand assets, colors, logo and photography.
