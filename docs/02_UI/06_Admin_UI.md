# Admin UI Specification

Version: 1.1 (Sprint 3.3)

## Purpose and scope

Sprint 3.3 implements the authenticated internal staff shell and menu
administration under `/staff`. It is operational, responsive, and intentionally
separate from the future customer menu experience.

Implemented roles:

- `MenuManager`: dashboard, menu overview, categories, products, option groups,
  option values, product option configuration, profile, and logout.
- `Administrator`: all MenuManager pages plus audit-log list and detail.
- Other employees: authenticated dashboard/profile only; protected menu routes
  return a forbidden page.

Reception, kitchen, pickup boards, employee management, orders, working hours,
cafe settings, analytics, and the customer menu UI are not implemented in this
sprint.

## Routes

```text
/staff
/staff/profile
/staff/menu
/staff/menu/categories
/staff/menu/categories/new
/staff/menu/categories/:id
/staff/menu/products
/staff/menu/products/new
/staff/menu/products/:id
/staff/menu/option-groups
/staff/menu/option-groups/new
/staff/menu/option-groups/:id
/staff/audit-log
/staff/audit-log/:id
```

`/staff/login` remains the unauthenticated employee entry point.

## Staff layout

The reusable shell displays Mood Pickup, employee name and roles,
authentication state, SignalR connection state, permission-filtered navigation,
profile, and logout. It uses a persistent desktop sidebar and responsive mobile
navigation. Protected content is withheld while authentication restoration is
pending. Customer sessions and insufficient employee permissions show a
forbidden page; unauthenticated users are redirected to `/staff/login`.

Access tokens remain in memory. Staff/authentication state does not use
`localStorage` or `sessionStorage`. The separate anonymous customer cart may
use the documented `moodpickup.cart.v1` localStorage key, but never contains
staff or authentication data.

## Menu overview

`/staff/menu` derives current category, product, unavailable-product,
hidden-product, and option-group counts from the administrative list APIs. It
provides create shortcuts. Administrators also see recent audit activity;
MenuManagers do not request or render audit history.

## Categories

The category page provides server pagination, search, include-deleted filtering,
product counts, visibility/deletion state, timestamps, create/edit,
visibility toggle, soft delete, restore, and accessible move-up/move-down
ordering through the atomic batch reorder endpoint.

The create/edit form trims text, applies shape validation, maps backend
`ProblemDetails.errors` to fields, retains returned GUID `rowVersion` values,
warns about unsaved browser navigation, and presents the shared concurrency
conflict notice.

## Products and images

The product list provides category, text, availability, visibility, and
include-deleted filters; pagination; thumbnails; exact TJS formatting; status
and orderability summaries; create/edit; duplicate; availability/visibility
toggles; delete/restore; and category-scoped accessible reordering. Dense
desktop rows become labeled cards on narrow screens.

The editor contains:

1. Basic information.
2. Decimal TJS price and nullable measurements.
3. Image preview/upload/assignment/removal.
4. Availability and visibility.
5. Product-specific option configuration.
6. Orderability status and exact backend issues.
7. Creation/update metadata.

The browser accepts JPEG, PNG, and WebP and rejects empty, unsupported, and
over-5-MB selections before upload as advisory validation. The backend is
authoritative. A replacement is uploaded first and then assigned; removal sets
`imageId` to `null`. Old media is never automatically deleted.

## Option groups, values, and product configuration

Global option groups support search, active/deleted filters, create/edit,
activate/deactivate, delete, and restore. The form validates obvious selection
shape errors: Single maximum is one, required minimum is at least one, and
minimum does not exceed maximum.

Global values are managed inline with create/edit, active toggle, soft delete,
restore, and explicit display order. This section does not contain product
prices.

The product editor assigns only selected global groups and values. Each group
configures required/minimum/maximum/order/active state. Each allowed value
configures its product-specific price modifier, availability, order, volume,
calories, and (for Single groups) default state. Choosing a new Single default
first unsets the previous default; stale conflicts trigger a refetch.

## Draft and orderability behavior

Backend-accepted drafts are successful saves, not generic errors. After menu
and option mutations the UI shows the returned `orderability` state:

- `Orderable`;
- `Draft — configuration incomplete`;
- issue messages such as missing values/defaults;
- hidden and unavailable conditions.

## Concurrency

Every applicable update, toggle, delete, restore, and reorder sends the latest
GUID `rowVersion`. `409 MENU_VERSION_CONFLICT` never silently overwrites data.
Forms preserve local text and offer reload/discard actions. List mutations
refetch the server resource; reorder failures restore server ordering and show
a concise notification.

## Audit log

Only `CanViewAuditLog` users see `/staff/audit-log`. The list supports employee,
action, entity type, entity ID, date range, and pagination filters. Detail
shows identity, entity reference, timestamp, correlation ID, changed fields,
and old/new JSON. JSON is parsed and rendered as text in `<pre>` elements;
`dangerouslySetInnerHTML` is not used.

## Common states and accessibility

Pages provide loading, retryable error, empty/no-result, validation, success,
forbidden, and conflict states. Forms have semantic labels and keyboard-safe
controls. Reorder actions have descriptive accessible names. Dialogs use modal
semantics, Escape handling, focus placement/trapping, and reasonable touch
targets. Focus indicators and screen-reader live regions cover uploads,
connection state, mutations, and toasts.
