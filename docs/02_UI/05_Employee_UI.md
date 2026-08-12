
# Employee UI Specification

Version: 1.0

## 1. Purpose

This document defines the employee-facing interface of Mood Pickup System.

The employee interface must be fast, readable and suitable for active café operations. It must avoid dense tables and excessive controls.

## 2. Roles

An employee may have multiple roles:

- Administrator
- Order Reception
- Kitchen
- Pickup
- Menu Manager
- Cashier
- Manager

The navigation and available actions depend on assigned roles.

## 3. Employee Routes

Suggested routes:

- `/staff/login`
- `/staff/dashboard`
- `/staff/reception`
- `/staff/kitchen`
- `/staff/pickup`
- `/staff/orders`
- `/staff/orders/{id}`
- `/staff/menu`
- `/staff/employees`
- `/staff/settings`
- `/staff/audit-log`
- `/staff/profile`

## 4. Global Layout

Desktop-first internal layout:

- Left sidebar
- Top header
- Main content area
- Real-time notification area
- Employee profile menu

Sidebar items are shown only when the employee has access.

Possible items:

- Dashboard
- Order Reception
- Kitchen
- Pickup
- Order History
- Menu
- Employees
- Settings
- Audit Log

## 5. Employee Login

Fields:

- Username
- Password

States:

- Loading
- Invalid credentials
- Temporary password
- Password change required

After login, an employee with `MustChangePassword = true` is redirected to the
staff profile password-change form. Other staff pages remain unavailable until
the change succeeds and the in-memory access token is refreshed. Employees
without that flag are redirected to Dashboard.

## 6. Dashboard

### 6.1 Goal

Provide a fast overview of the current café workload.

### 6.2 Summary Cards

Show role-dependent counters:

- New orders
- Confirmed orders
- Preparing
- Ready for pickup
- Delayed orders
- Orders completed today

Each card links to the relevant board.

### 6.3 Recent Activity

Display the latest important events:

- New order received
- Order confirmed
- Preparation started
- Order ready
- Order completed
- Time updated
- Order rejected

### 6.4 Urgent Information

Highlight:

- Orders past estimated ready time
- Orders close to pickup time
- Ready orders waiting for pickup
- Failed payments or refunds
- SignalR connection problems

Sprint 4.1 order cards/details show the online provider status. Paid rejected
orders are visibly `Refund required`; transaction reference, paid/refunded
times, and reconciliation reason are visible in staff details. No refund action
is offered until the official provider contract is implemented.

### 6.5 Role Awareness

Employees see only cards and shortcuts related to their assigned roles.

## 7. Order Reception Board

### 7.1 Goal

Allow reception employees to review and confirm new orders quickly.

### 7.2 Columns

Recommended columns:

- Waiting for Confirmation
- Confirmed
- Rejected Today

The board should remain simple and not display all order details at once.

### 7.3 Order Card

Compact card fields:

- Public order number
- Customer name
- Requested pickup type
- Requested pickup time
- Order creation time
- Item count
- Total price
- Payment method
- Payment status
- Comment indicator
- Time urgency indicator

### 7.4 Actions

Available actions:

- Open details
- Confirm
- Set estimated ready time
- Change estimated ready time
- Reject

Reject requires a reason.

### 7.5 Confirmation

Sprint 3.7 requires an estimated ready time in the confirmation dialog. The
backend validates that it is later than now, today, and inside working hours.

After confirmation:

- The customer status updates
- Kitchen employees receive a notification
- The order appears on the kitchen board
- The card disappears from the new-order list for all other employees

The `/staff/orders` dashboard is available to Administrator, Cashier, and
Manager. MenuManager alone cannot view or mutate orders. It uses responsive
cards, all-status filtering, disconnected refresh fallback, order details,
payment/completion actions where permitted, and GUID row-version conflicts.

### 7.6 Sorting

Default sorting:

1. Requested pickup time
2. ASAP orders
3. Order creation time

The board must prioritize urgency rather than order number.

## 8. Kitchen Board

### 8.1 Goal

Allow kitchen employees to manage confirmed orders and preparation.

### 8.2 Columns

- New
- Preparing
- Ready

### 8.3 Kitchen Card

The card should show enough information to begin work without opening details.

Fields:

- Public order number
- Requested or estimated pickup time
- Countdown or delay
- Product lines
- Quantity
- Selected sizes
- Milk options
- Syrups
- Other options
- Order comment indicator

Show payment method and whether pickup payment is still due. Kitchen employees
cannot mutate payment state.

Customer phone number is available only in order details.

### 8.4 Actions

- Start preparing
- Change estimated ready time
- Mark ready

### 8.5 Sequential Status Rules

Kitchen actions must respect:

Confirmed → Preparing → Ready for Pickup

Stages cannot be skipped.

Sprint 3.8 has no rollback action. Repeated, skipped, or reverse transitions
return ProblemDetails and leave the card unchanged.

### 8.6 Urgency Indicators

Use both text and visual indicators:

- More than 15 minutes remaining
- Less than 10 minutes remaining
- Pickup time reached
- Delayed

Do not rely on color alone.

### 8.7 Readability

Kitchen cards must use:

- Large text
- High contrast
- Minimal secondary information
- Clear product grouping
- Large action buttons

## 9. Pickup Board

### 9.1 Goal

Manage ready orders and complete handoff.

### 9.2 Columns

- Ready for Pickup
- Completed Today

### 9.3 Card Fields

- Public order number
- Customer name
- Phone number
- Ready since
- Waiting duration
- Payment method
- Payment status
- Total price
- Item count

### 9.4 Pay on Pickup Flow

Before completion:

1. Employee selects received payment type:
   - Cash
   - Card
2. Payment is marked as received.
3. Complete button becomes available.

An unpaid pay-on-pickup order cannot be completed.

### 9.5 Completion

When employee marks the order completed:

- Customer sees Completed
- Order moves to history
- Completion is written to audit log

### 9.6 Rollback

Completion rollback is not available in Sprint 3.8.

## 10. Order Details

### 10.1 Common Data

Display:

- Order number
- Status
- Customer name
- Phone number
- Created time
- Requested pickup time
- Estimated ready time
- Actual ready time
- Payment method
- Payment status
- Refund status
- Items
- Selected options
- Prices
- Total
- Comment
- Rejection reason
- Status history
- Employee actions

### 10.2 Role-Based Actions

Reception:

- Confirm
- Reject
- Change time

Kitchen:

- Start preparing
- Change time
- Mark ready

Pickup:

- Record payment
- Complete

Administrator:

- All permitted actions

### 10.3 Concurrency

If another employee changes the order while it is open:

- Show a visible update
- Refresh current data
- Prevent overwriting newer changes
- Explain that the order was updated by another employee

## 11. Real-Time Notifications

### 11.1 New Order

Recipients:

- Employees with Order Reception role

Notification types:

- In-app toast
- One short sound
- Browser notification when allowed
- Counter badge
- New card on board

The sound plays once.

### 11.2 Confirmed Order

Recipients:

- Employees with Kitchen role

### 11.3 Ready Order

Recipients:

- Employees with Pickup role

### 11.4 Shared Synchronization

When one employee performs an action:

- All boards update immediately
- Old cards disappear where needed
- New cards appear in the correct column
- Counters update
- Duplicate processing is prevented

## 12. Browser Notifications

The system should request permission after employee login, not before.

Browser notifications are used when the staff tab is hidden or minimized.

Notification content should be concise:

- New order #152
- Order #152 confirmed
- Order #152 ready for pickup

## 13. Sound Notifications

Rules:

- One short sound per new relevant event
- No repeated sound loop
- Different sound types may be used for:
  - New order
  - Ready order
- Employees may mute sounds in profile preferences

## 14. Connection State

The interface must show SignalR state:

- Connected
- Reconnecting
- Disconnected

When disconnected:

- Display a persistent warning
- Retry automatically
- Allow manual refresh
- Avoid pretending that the board is current

## 15. Order History

### 15.1 Goal

Allow employees to search and review previous orders.

### 15.2 Filters

- Date range
- Status
- Payment method
- Payment status
- Order number
- Customer phone
- Customer name

### 15.3 List Fields

- Order number
- Created date
- Customer
- Final status
- Total
- Payment method
- Completed or rejected time

### 15.4 Pagination

Server-side pagination is required.

## 16. Employee Profile

Display:

- Full name
- Username
- Assigned roles

Actions:

- Change password
- Configure notification sound
- Logout
- View own action history

## 17. Accessibility and Usability

Requirements:

- Keyboard-accessible actions
- Visible focus state
- Large touch targets on tablets
- Confirm destructive actions
- No critical information by color alone
- Clear loading and error states
- Short labels
- No unnecessary animations

## 18. Responsive Behavior

### Desktop

Primary employee environment.

- Sidebar visible
- Multiple columns visible
- Dense but readable board

### Tablet

Supported for kitchen and pickup.

- Collapsible sidebar
- Horizontally scrollable board or stacked columns
- Large controls

### Mobile

Supported for urgent actions and order review.

- One board column at a time
- Bottom navigation or compact menu
- Full-screen order details

## 19. Common States

Every board must support:

- Loading
- Empty
- Error
- Unauthorized
- Forbidden
- Disconnected
- Stale data
- No results

## 20. Design Direction

Employee UI must feel:

- Operational
- Calm
- Fast
- Clear
- Reliable

Avoid:

- Decorative dashboards
- Overloaded cards
- Small controls
- Large data tables for active workflows
- Hidden critical actions
- Excessive confirmation dialogs

## 21. Administrator Employee Management

`/staff/employees` is available only to Administrator. The sidebar item is not
rendered for other roles and the backend `CanManageEmployees` policy remains
authoritative.

The responsive list supports name/username search, exact role filtering,
Active/Disabled filtering, server pagination, status and password-change
indicators, created/last-login dates, create, details/edit, disable/enable, and
password reset. Desktop uses a table; narrow screens render the same semantic
rows as labeled cards without horizontal page overflow.

`/staff/employees/new` uses a multi-role checkbox group populated by
`GET /admin/roles`. After creation, the generated temporary password is shown
prominently with a copy button and one-time warning. It is kept only in local
component state, never in a URL, local/session storage, Redux, or query data.

`/staff/employees/{id}` combines editable identity/roles, account state,
disable/enable, password reset, and paginated/filterable action history. Reset
shows the new password once and reports how many active refresh sessions were
revoked. Stale writes show the shared conflict notice; last-Administrator
errors display “At least one active Administrator account must remain.”
