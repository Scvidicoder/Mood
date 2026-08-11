
# REST API Specification

Version: 1.5 (Sprint 3.6)

Version 1 base URL:

```text
/api/v1
```

Content type:

```text
application/json
```

Implemented through Sprint 3.6: authentication and session endpoints, system
and health endpoints, the public menu, interactive customer configuration and
local cart UI, authenticated customer checkout/orders, authorized menu
administration, employee menu audit reads, secure image upload, and
metadata-mediated public media delivery. Later staff, kitchen, payment,
notification, and tracking endpoints remain explicitly planned only.

Authentication:

```text
Authorization: Bearer <access-token>
```

---

# 1. Common Response Rules

## 1.1 Success Responses

Standard success responses use the appropriate HTTP status:

- `200 OK`
- `201 Created`
- `204 No Content`

## 1.2 Validation Error

```json
{
  "type": "validation_error",
  "title": "Request validation failed",
  "status": 400,
  "errors": {
    "phoneNumber": [
      "Phone number is invalid."
    ]
  }
}
```

## 1.3 Business Rule Error

```json
{
  "type": "concurrency_conflict",
  "title": "The menu changed while checkout was being processed",
  "status": 409,
  "code": "CHECKOUT_CONCURRENCY_CONFLICT",
  "detail": "Refresh the cart and try checkout again."
}
```

## 1.4 Not Found

```json
{
  "type": "not_found",
  "title": "Resource not found",
  "status": 404,
  "code": "ORDER_NOT_FOUND"
}
```

## 1.5 Unauthorized

```json
{
  "type": "unauthorized",
  "title": "Authentication required",
  "status": 401
}
```

## 1.6 Forbidden

```json
{
  "type": "forbidden",
  "title": "Access denied",
  "status": 403
}
```

---

# 2. Customer Authentication

Authentication responses return access tokens only. The backend stores the
rotating refresh token in a `Secure`, `HttpOnly`, `SameSite=Lax` cookie. A
separate readable CSRF cookie is used by `/auth/refresh` and `/auth/logout`;
clients must copy its value into the `X-CSRF-TOKEN` header. Neither access nor
refresh tokens may be stored in browser storage.

## POST `/auth/customer/request-code`

Creates a customer authentication challenge. In real mode an unlinked number
receives an opaque Telegram deep link and no OTP is generated yet. A customer
with an existing verified Telegram chat receives the OTP immediately.

### Request

```json
{
  "phoneNumber": "+992900000000"
}
```

### Response

```json
{
  "challengeId": "uuid",
  "expiresInSeconds": 300,
  "resendAvailableInSeconds": 60,
  "telegramBotUrl": "https://t.me/example_bot?start=opaque_url_safe_token",
  "clientChallengeSecret": "opaque-client-secret",
  "status": "WaitingForTelegramStart"
}
```

`telegramBotUrl` must be used as returned. The client challenge secret
authorizes status polling and is kept only in the current in-memory
authentication flow. Neither secret is an access token.

### Errors

- `400 INVALID_PHONE_NUMBER`
- `429 TOO_MANY_CODE_REQUESTS`
- `503 TELEGRAM_NOT_CONFIGURED`
- `503 TELEGRAM_DELIVERY_FAILED`

---

## POST `/auth/customer/challenge-status`

Returns only pre-authentication challenge state.

### Request

```json
{
  "challengeId": "uuid",
  "clientChallengeSecret": "opaque-client-secret"
}
```

### Response

```json
{
  "status": "WaitingForTelegramContact",
  "expiresInSeconds": 240,
  "canResend": false
}
```

Statuses are `WaitingForTelegramStart`, `WaitingForTelegramContact`,
`OtpSent`, `Expired`, `Locked`, and `Completed`. The response never includes a
phone number, Telegram identity, OTP, customer data, or account-existence
detail. An invalid secret returns `401 INVALID_CHALLENGE_STATUS_SECRET`.

---

## POST `/telegram/webhook`

Telegram calls `/api/v1/telegram/webhook` anonymously with
`X-Telegram-Bot-Api-Secret-Token`. Missing or incorrect secrets return
`401 TELEGRAM_WEBHOOK_UNAUTHORIZED` before model binding; payloads above 64 KiB
return `413 TELEGRAM_WEBHOOK_TOO_LARGE`. Valid supported and benign unsupported
updates return `200`. The endpoint accepts only the typed subset needed for
private message, `/start`, `/help`, and contact processing. Durable
`update_id` idempotency prevents duplicate OTP delivery.

---

## POST `/auth/customer/verify-code`

Verifies the one-time code.

### Request

```json
{
  "challengeId": "uuid",
  "code": "123456"
}
```

### Existing Customer Response

```json
{
  "isNewCustomer": false,
  "accessToken": "jwt",
  "expiresInSeconds": 900,
  "customer": {
    "id": "uuid",
    "name": "Ivan",
    "phoneNumber": "+992900000000"
  }
}
```

### New Customer Response

```json
{
  "isNewCustomer": true,
  "registrationToken": "temporary-token"
}
```

### Errors

- `400 INVALID_CODE`
- `410 CODE_EXPIRED`
- `429 TOO_MANY_ATTEMPTS`

---

## POST `/auth/customer/complete-registration`

### Request

```json
{
  "registrationToken": "temporary-token",
  "name": "Ivan"
}
```

### Response

```json
{
  "accessToken": "jwt",
  "expiresInSeconds": 900,
  "customer": {
    "id": "uuid",
    "name": "Ivan",
    "phoneNumber": "+992900000000"
  }
}
```

---

## POST `/auth/refresh`

The request body is empty. The rotating refresh token is read only from the
HttpOnly refresh cookie. The request must include the `X-CSRF-TOKEN` header
whose value matches the readable CSRF cookie.

### Response

```json
{
  "accessToken": "jwt",
  "expiresInSeconds": 900
}
```

Successful refresh rotates both the refresh cookie and CSRF cookie. Reusing a
revoked refresh token revokes its entire token family.

---

## POST `/auth/logout`

Revokes the refresh token from the HttpOnly cookie. The request body is empty
and the matching CSRF cookie/header pair is required.

Response:

```text
204 No Content
```

---

# 3. Employee Authentication

## POST `/staff/auth/login`

### Request

```json
{
  "username": "kitchen1",
  "password": "password"
}
```

### Response

```json
{
  "accessToken": "jwt",
  "expiresInSeconds": 900,
  "mustChangePassword": false,
  "employee": {
    "id": "uuid",
    "fullName": "Alex",
    "username": "kitchen1",
    "roles": [
      "Kitchen",
      "Pickup"
    ]
  }
}
```

---

## POST `/staff/auth/change-password`

Requires an employee bearer access token. Employees with temporary passwords
may call this endpoint even though operational staff policies remain disabled.

### Request

```json
{
  "currentPassword": "old-password",
  "newPassword": "new-password"
}
```

Response:

```text
204 No Content
```

---

# 4. Public Cafe Information

Planned, not implemented in Sprint 3.2.

## GET `/cafe`

### Response

```json
{
  "name": "Mood Dushanbe",
  "address": "Dushanbe",
  "phoneNumbers": [
    "+992..."
  ],
  "bannerUrl": "/media/banner.jpg",
  "status": "Open",
  "statusMessage": null,
  "todayWorkingHours": {
    "opensAt": "10:00",
    "closesAt": "00:00"
  },
  "acceptingOrders": true
}
```

---

## GET `/cafe/pickup-slots`

Query:

```text
?date=2026-08-05
```

### Response

```json
{
  "supportsAsap": true,
  "maximumAdvanceMinutes": 240,
  "intervalMinutes": 15,
  "slots": [
    "14:30",
    "14:45",
    "15:00"
  ]
}
```

---

# 5. Menu

Public menu endpoints are anonymous.

Sprint 3.5 does not change these Sprint 3.2/3.3 contracts. The customer
frontend consumes list data as read-only server state, requests at most 100
products per page, and loads subsequent pages when necessary to render the
complete grouped menu. Product detail is also the current source for local
configuration and cart revalidation. Search and category filtering remain
server-side.

The GUIDs in these responses are public resource identifiers needed for
category grouping, product routing, and future option selection. Responses do
not contain row versions, soft-delete/visibility flags, audit fields, media
storage keys, physical paths, timestamps, or other administrative metadata.

## GET `/categories`

### Response

```json
[
  {
    "id": "uuid",
    "name": "Coffee",
    "description": "Coffee drinks",
    "displayOrder": 1
  }
]
```

Only visible, non-deleted categories with at least one visible, non-deleted
product are returned. Ordering is `displayOrder`, then `name`. Unavailable
products still count because browsing remains available.

---

## GET `/products`

Query parameters:

- `categoryId`
- `search`
- `page`
- `pageSize`
- `includeUnavailable`

`page` defaults to `1`, `pageSize` defaults to `20`, and the maximum page size
is `100`. `includeUnavailable=false` explicitly removes unavailable products;
omitting it preserves the default business behavior and keeps them visible.
Search is trimmed and case-insensitive across name, short description, and
description.

### Response

```json
{
  "items": [
    {
      "id": "uuid",
      "categoryId": "uuid",
      "name": "Cappuccino",
      "shortDescription": "Espresso with milk",
      "imageUrl": "/media/ab/cd/generated-image.jpg",
      "priceFrom": 22.00,
      "currency": "TJS",
      "weightGrams": null,
      "volumeMilliliters": 250,
      "calories": 120,
      "isAvailable": true,
      "isOrderable": true,
      "availabilityIssues": []
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 1,
  "totalPages": 1
}
```

Deleted/hidden products and products below a deleted/hidden category never
appear. Unavailable products appear with `isAvailable=false`,
`isOrderable=false`, and a `PRODUCT_UNAVAILABLE` issue.

`imageUrl` is `null` when no non-deleted image is assigned. Otherwise it is the
provider-generated public path for the verified media object.

`priceFrom` is the base price plus the cheapest available, active, assigned
value from every active required single-selection group. Optional additions
are excluded. If a required group has no calculable value, the calculable
minimum (at least the base price) is returned and orderability issues explain
why the product cannot currently be ordered.

---

## GET `/products/{id}`

### Response

```json
{
  "id": "uuid",
  "categoryId": "uuid",
  "name": "Cappuccino",
  "description": "Espresso with steamed milk",
  "ingredients": "Coffee, milk",
  "imageUrl": "/media/ab/cd/generated-image.jpg",
  "basePrice": 22.00,
  "priceFrom": 22.00,
  "currency": "TJS",
  "weightGrams": null,
  "volumeMilliliters": 250,
  "calories": 120,
  "isAvailable": true,
  "isOrderable": true,
  "availabilityIssues": [],
  "optionGroups": [
    {
      "id": "uuid",
      "name": "Size",
      "selectionType": "Single",
      "isRequired": true,
      "minimumSelections": 1,
      "maximumSelections": 1,
      "displayOrder": 1,
      "values": [
        {
          "id": "uuid",
          "optionValueId": "uuid",
          "name": "Large",
          "description": null,
          "priceModifier": 8.00,
          "isDefault": false,
          "isAvailable": true,
          "displayOrder": 3,
          "volumeMilliliters": 450,
          "calories": 180
        }
      ]
    }
  ]
}
```

Hidden or deleted products return `404 PRODUCT_NOT_FOUND`; unavailable
products are returned. Only active, non-deleted global groups/values assigned
to the product are selectable. An unavailable assigned value remains in the
response with `isAvailable=false`. Unassigned global values are never exposed.

Sprint 3.5 renders this configuration as accessible radio/checkbox controls
and uses the public base price/modifiers for an untrusted local preview.
Selected public `optionValueId` values are retained for checkout.
`isOrderable` and `availabilityIssues` remain authoritative backend results;
the API still recalculates all commercial values at checkout.

---

# 6. Customer Profile

Planned, not implemented in Sprint 3.2.

## GET `/me`

### Response

```json
{
  "id": "uuid",
  "name": "Ivan",
  "phoneNumber": "+992900000000",
  "createdAt": "2026-08-05T10:00:00Z"
}
```

---

## PATCH `/me`

### Request

```json
{
  "name": "Ivan Updated"
}
```

### Response

Returns the updated profile.

---

## POST `/me/change-phone/request-code`

Requests verification for a new phone number.

---

## POST `/me/change-phone/confirm`

### Request

```json
{
  "challengeId": "uuid",
  "code": "123456"
}
```

---

# 7. Cart

The anonymous cart is a frontend-owned Redux draft persisted under
`moodpickup.cart.v1`. It uses existing anonymous product-detail reads to
configure and revalidate lines. No request is sent when a customer adds,
updates, removes, or clears a cart line.

The local cart supplies only these untrusted identifiers to checkout:

```json
{
  "productId": "uuid",
  "quantity": 1,
  "optionValueIds": [
    "uuid"
  ]
}
```

Saved names, prices, modifiers, availability, and orderability remain display
snapshots only. `POST /orders` revalidates product visibility/orderability,
option compatibility and availability, selection rules, and prices from
PostgreSQL. The API never accepts cart price, currency, image, or availability
fields as commercial truth.

There is no server cart API. `/cart` and `/cart/items` are not implemented and
clients must not call them.

---

# 8. Checkout

## POST `/orders`

Creates a persistent order from the authenticated customer's local cart draft.
Requires the `Customer` policy; anonymous callers receive `401`. Customer name
and phone number come from the authenticated profile and cannot be submitted
by the client. Validation, daily order-number allocation, item/option snapshot
creation, and commit run inside one serializable transaction.

### Request

```json
{
  "items": [
    {
      "productId": "8a2ecf44-96f9-4c91-80a9-13c4d141d124",
      "optionValueIds": ["b152d3b2-24fb-4dc8-ab8b-dc9f7041a3d2"],
      "quantity": 2,
      "comment": "Less foam"
    }
  ],
  "pickupMode": "Scheduled",
  "requestedPickupTime": "2026-08-07T15:30:00+05:00",
  "paymentMethod": "PayOnPickup",
  "comment": "Please keep it warm"
}
```

`pickupMode` is `AsSoonAsPossible` or `Scheduled`; `paymentMethod` is
`PayOnPickup` or `Online`. `Online` stores only the selected method and does
not call a payment provider. For `AsSoonAsPossible`, `requestedPickupTime`
must be null. For `Scheduled`, it is required and must be today, on a
15-minute interval, inside configured business hours and the next four hours.
The default café configuration is `Asia/Dushanbe`, `10:00-22:00`, and `TJS`.

Checkout rejects missing, hidden, deleted, unavailable, or non-orderable
products; unassigned, deleted, unavailable, duplicate, or incompatible option
values; and unmet selection ranges. It recalculates all line/order totals. A
failure returns RFC 7807 `400 validation_error` fields and writes no partial
order. A menu concurrency conflict returns `409 CHECKOUT_CONCURRENCY_CONFLICT`.

### Response (`201 Created`)

```json
{
  "id": "55d83050-08ff-458d-976e-e47c72cf6d75",
  "orderNumber": "MP-20260807-00015",
  "status": "PendingConfirmation",
  "paymentMethod": "PayOnPickup",
  "pickupMode": "Scheduled",
  "requestedPickupTime": "2026-08-07T10:30:00Z",
  "comment": "Please keep it warm",
  "subtotal": 48.00,
  "discountTotal": 0.00,
  "total": 48.00,
  "currency": "TJS",
  "createdAt": "2026-08-07T10:05:00Z",
  "items": [
    {
      "productId": "8a2ecf44-96f9-4c91-80a9-13c4d141d124",
      "productName": "Cappuccino",
      "isAvailableAtPurchase": true,
      "basePrice": 22.00,
      "finalPrice": 24.00,
      "quantity": 2,
      "comment": "Less foam",
      "options": [
        {
          "optionGroupName": "Size",
          "optionValueName": "Small",
          "priceModifier": 2.00,
          "displayOrder": 1
        }
      ]
    }
  ]
}
```

`finalPrice` is the server-calculated unit price. Item and selected-option
values, including product availability at purchase time, are immutable
snapshots; later menu edits do not alter historical
orders. The human-readable order number is unique and sequential per café
date; the primary key remains a GUID. `POST /checkout/preview` is not
implemented.

---

# 9. Customer Orders

All endpoints in this section require the `Customer` policy. A customer can
read only orders whose `CustomerId` matches the validated JWT subject. A lookup
of another customer's order returns `404` rather than exposing its existence.

## GET `/orders/mine`

Returns newest-first summary records for the authenticated customer.

Query parameters:

- `page` (default `1`, minimum `1`)
- `pageSize` (default `20`, range `1-100`)

### Response (`200 OK`)

```json
{
  "items": [
    {
      "id": "55d83050-08ff-458d-976e-e47c72cf6d75",
      "orderNumber": "MP-20260807-00015",
      "status": "PendingConfirmation",
      "paymentMethod": "PayOnPickup",
      "pickupMode": "Scheduled",
      "requestedPickupTime": "2026-08-07T10:30:00Z",
      "total": 48.00,
      "currency": "TJS",
      "itemQuantity": 2,
      "createdAt": "2026-08-07T10:05:00Z"
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 1,
  "totalPages": 1
}
```

## GET `/orders/{id}`

Returns the complete immutable order snapshot. Its response shape is the same
as `POST /orders`. It returns `404 ORDER_NOT_FOUND` when the order does not
exist or is not owned by the caller.

Only `PendingConfirmation` and `Cancelled` statuses exist in Sprint 3.6.
There are no cancellation, repeat, staff, kitchen, payment, notification, or
tracking endpoints yet.

---

# 10. Reception API

Planned, not implemented in Sprint 3.2.

Required role:

- `OrderReception`
- or `Administrator`

## GET `/staff/reception/orders`

Query parameters:

- `status`
- `page`
- `pageSize`

---

## POST `/staff/orders/{id}/confirm`

### Request

```json
{
  "estimatedReadyTime": null
}
```

### Response

Returns the updated order.

---

## POST `/staff/orders/{id}/reject`

### Request

```json
{
  "reason": "Kitchen is temporarily unavailable."
}
```

---

## PUT `/staff/orders/{id}/estimated-ready-time`

Required role:

- `OrderReception`
- `Kitchen`
- `Administrator`

### Request

```json
{
  "estimatedReadyTime": "2026-08-05T15:00:00+05:00",
  "rowVersion": "base64-version"
}
```

---

# 11. Kitchen API

Planned, not implemented in Sprint 3.2.

Required role:

- `Kitchen`
- or `Administrator`

## GET `/staff/kitchen/orders`

Returns:

- Confirmed
- Preparing
- Ready for Pickup

---

## POST `/staff/orders/{id}/start-preparing`

### Request

```json
{
  "rowVersion": "base64-version"
}
```

---

## POST `/staff/orders/{id}/mark-ready`

---

## POST `/staff/orders/{id}/rollback`

### Request

```json
{
  "targetStatus": "Preparing",
  "reason": "Status changed by mistake.",
  "rowVersion": "base64-version"
}
```

Only explicitly allowed rollback transitions are accepted.

---

# 12. Pickup API

Planned, not implemented in Sprint 3.2.

Required role:

- `Pickup`
- or `Administrator`

## GET `/staff/pickup/orders`

---

## POST `/staff/orders/{id}/record-payment`

### Request

```json
{
  "onSitePaymentMethod": "Cash",
  "rowVersion": "base64-version"
}
```

---

## POST `/staff/orders/{id}/complete`

Fails when pay-on-pickup payment has not been recorded.

---

# 13. Staff Order Search

Planned, not implemented in Sprint 3.2.

## GET `/staff/orders`

Query parameters:

- `status`
- `paymentMethod`
- `paymentStatus`
- `customerName`
- `customerPhone`
- `orderNumber`
- `dateFrom`
- `dateTo`
- `page`
- `pageSize`

Required role:

Any employee role with order access.

---

## GET `/staff/orders/{id}`

Returns complete order details, customer contact data and action history.

---

# 14. Admin Categories

Authorization policy: `CanManageMenu` (`MenuManager` or `Administrator`).

Implemented routes:

```text
GET    /admin/categories
GET    /admin/categories/{id}
POST   /admin/categories
PUT    /admin/categories/{id}
PUT    /admin/categories/reorder
PATCH  /admin/categories/{id}/visibility
DELETE /admin/categories/{id}?rowVersion={guid}
POST   /admin/categories/{id}/restore
```

The list supports `includeDeleted`, `search`, `page`, and `pageSize`. It returns
timestamps, non-deleted product count, deletion/visibility state, and a GUID
`rowVersion`. Deleted rows are returned only with `includeDeleted=true`;
details by ID may deliberately return a deleted row.

Create request:

```json
{
  "name": "Coffee",
  "description": "Coffee drinks",
  "displayOrder": 1,
  "isVisible": true
}
```

Create returns `201`, the administrative category DTO, and its `Location`.
Category names are not unique by the approved Sprint 3.1 database strategy.
They are trimmed, normalized, length-checked, and indexed.

Update request:

```json
{
  "name": "Specialty Coffee",
  "description": "Coffee drinks",
  "displayOrder": 1,
  "isVisible": true,
  "rowVersion": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
}
```

Visibility uses the same GUID concurrency contract:

```json
{
  "isVisible": false,
  "rowVersion": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
}
```

Reorder request:

### Request

```json
{
  "items": [
    {
      "id": "uuid",
      "displayOrder": 1,
      "rowVersion": "guid"
    }
  ]
}
```

Reorder is atomic, rejects duplicate IDs, and concurrency-checks every row.
Display orders do not have to be unique. Delete is soft and repeated delete
returns `409 CATEGORY_ALREADY_DELETED`. Restore requires
`{"rowVersion":"guid"}` and never restores products.

---

# 15. Admin Products

Authorization policy: `CanManageMenu`.

Implemented routes:

```text
GET    /admin/products
GET    /admin/products/{id}
POST   /admin/products
PUT    /admin/products/{id}
POST   /admin/products/{id}/duplicate
PUT    /admin/products/reorder
PATCH  /admin/products/{id}/availability
PATCH  /admin/products/{id}/visibility
PUT    /admin/products/{id}/image
DELETE /admin/products/{id}?rowVersion={guid}
POST   /admin/products/{id}/restore
```

The list supports `categoryId`, `search`, `isAvailable`, `isVisible`,
`includeDeleted`, `page`, and `pageSize`, and returns lightweight rows without
option graphs. Sprint 3.3 additively includes `imageUrl`, `isOrderable`, and
`availabilityIssues` on every list row so the staff list can render thumbnails
and draft warnings without per-row detail requests. Details return
category/image metadata (including media `url`), every configured assignment,
soft-deletion/active states, timestamps, GUID row versions, and `orderability`.

Create request:

```json
{
  "categoryId": "uuid",
  "name": "Cappuccino",
  "shortDescription": "Espresso with steamed milk",
  "description": "Full description",
  "ingredients": "Coffee, milk",
  "basePrice": 22.00,
  "defaultWeightGrams": null,
  "defaultVolumeMilliliters": 250,
  "defaultCalories": 120,
  "imageId": null,
  "isAvailable": true,
  "isVisible": true,
  "displayOrder": 1
}
```

Update adds `"rowVersion":"guid"`. Product names follow the Sprint 3.1
non-unique normalized/indexed strategy. A category must exist and not be
deleted; a hidden category is allowed for drafts. An image ID must reference a
non-deleted `MediaFile`.

Availability request:

```json
{
  "isAvailable": false,
  "rowVersion": "guid"
}
```

Visibility uses `isVisible` plus `rowVersion`. Uploading and assigning remain
separate operations; image assignment references a successful media upload:

```json
{
  "imageId": "uuid-or-null",
  "rowVersion": "guid"
}
```

Duplicate accepts `{"name":"Optional Copy Name"}` and copies base fields,
group/value assignments, modifiers, defaults, availability, and ordering. It
references the same media metadata. Product reorder requires:

```json
{
  "categoryId": "uuid",
  "items": [
    {
      "id": "uuid",
      "displayOrder": 1,
      "rowVersion": "guid"
    }
  ]
}
```

All rows must belong to `categoryId`; the operation is atomic. Delete is soft,
keeps assignments, and repeated delete returns
`409 PRODUCT_ALREADY_DELETED`. Restore requires `{"rowVersion":"guid"}` and
does not restore related deleted global options.

Product create/update/duplicate/flag/image/restore responses use:

```json
{
  "resource": {},
  "orderability": {
    "isOrderable": false,
    "issues": [
      {
        "code": "PRODUCT_UNAVAILABLE",
        "message": "The product is unavailable.",
        "productOptionGroupId": null
      }
    ]
  }
}
```

---

# 16. Admin Option Groups

Authorization policy: `CanManageMenu`.

Global option definitions:

```text
GET    /admin/option-groups
GET    /admin/option-groups/{id}
POST   /admin/option-groups
PUT    /admin/option-groups/{id}
PATCH  /admin/option-groups/{id}/active
DELETE /admin/option-groups/{id}?rowVersion={guid}
POST   /admin/option-groups/{id}/restore

GET    /admin/option-groups/{groupId}/values?includeDeleted={bool}
POST   /admin/option-groups/{groupId}/values
PUT    /admin/option-values/{id}
PATCH  /admin/option-values/{id}/active
DELETE /admin/option-values/{id}?rowVersion={guid}
POST   /admin/option-values/{id}/restore
```

The group list accepts `search`, `isActive`, `includeDeleted`, `page`, and
`pageSize`. `isActive` was added in Sprint 3.3 so the staff UI can filter active
definitions without client-side paging errors.

Option-group create/update fields are `name`, `description`, `selectionType`,
`defaultIsRequired`, `defaultMinimumSelections`,
`defaultMaximumSelections`, `displayOrder`, and `isActive`; update adds
`rowVersion`. `Single` maximum cannot exceed one, minimum cannot exceed
maximum, and required groups need a minimum of at least one.

Option-value create fields are `name`, `description`, `displayOrder`, and
`isActive`; update adds `rowVersion`. Active (non-deleted) normalized names are
unique inside one global group but may be reused in another group. Global
values never store product-specific prices.

Product configuration:

```text
POST   /admin/products/{productId}/option-groups
PUT    /admin/products/{productId}/option-groups/{assignmentId}
DELETE /admin/products/{productId}/option-groups/{assignmentId}?rowVersion={guid}
POST   /admin/products/{productId}/option-groups/{assignmentId}/restore

POST   /admin/products/{productId}/option-groups/{assignmentId}/values
PUT    /admin/products/{productId}/option-values/{assignmentValueId}
DELETE /admin/products/{productId}/option-values/{assignmentValueId}?rowVersion={guid}
```

Group assignment create request:

```json
{
  "optionGroupId": "uuid",
  "isRequired": true,
  "minimumSelections": 1,
  "maximumSelections": 1,
  "displayOrder": 1,
  "isActive": true
}
```

Update omits `optionGroupId` and adds `rowVersion`. Assignment delete disables
the assignment with `IsActive=false`; restore re-enables it. The Sprint 3.1
schema intentionally has no assignment soft-delete column.

Value assignment create request:

```json
{
  "optionValueId": "uuid",
  "priceModifier": 8.00,
  "isDefault": false,
  "isAvailable": true,
  "displayOrder": 1,
  "volumeMilliliters": 450,
  "calories": 180
}
```

Update omits `optionValueId` and adds `rowVersion`. Removal physically removes
only the product-value join row; existing order snapshots are deliberately
independent and are never changed by the removal.

Wrong-group and duplicate values/groups are conflicts. A single group cannot
have multiple defaults. Non-negative prices/dimensions and selection ranges
are structurally required.

## Draft versus orderable

Structurally impossible writes return `409`; examples are wrong-group values,
duplicate assignments, invalid ranges, negative price modifiers, multiple
single-selection defaults, and stale versions. Progressive drafts are saved:
a required group may temporarily have no values or no available default.
Product configuration responses use the `resource + orderability` envelope so
the admin can continue editing while seeing exact issues.

---

# 17. Admin Employees

Planned, not implemented in Sprint 3.2.

Required role:

- `Administrator`

## GET `/admin/employees`

## POST `/admin/employees`

### Request

```json
{
  "fullName": "Alex",
  "username": "kitchen1",
  "temporaryPassword": "password",
  "roles": [
    "Kitchen",
    "Pickup"
  ]
}
```

## PUT `/admin/employees/{id}`

## POST `/admin/employees/{id}/reset-password`

### Response

```json
{
  "temporaryPassword": "generated-password"
}
```

## DELETE `/admin/employees/{id}`

## GET `/admin/employees/{id}/actions`

---

# 18. Working Hours and Cafe Settings

Planned, not implemented in Sprint 3.2.

## GET `/admin/working-hours`

## PUT `/admin/working-hours`

### Request

```json
{
  "days": [
    {
      "dayOfWeek": "Monday",
      "isClosed": false,
      "opensAt": "10:00",
      "closesAt": "00:00"
    }
  ]
}
```

## POST `/admin/cafe/temporary-closure`

### Request

```json
{
  "isClosed": true,
  "message": "We are temporarily not accepting orders."
}
```

## GET `/admin/cafe-settings`

## PUT `/admin/cafe-settings`

---

# 19. Audit Log

Authorization policy: `CanViewAuditLog` (currently `Administrator`).

## GET `/admin/audit-log`

Query parameters:

- `employeeId`
- `actionType`
- `entityType`
- `entityId`
- `dateFrom`
- `dateTo`
- `page`
- `pageSize`

The paged list returns timestamp, employee ID/name, action/entity identifiers,
description, and correlation ID. It intentionally omits old/new JSON.

## GET `/admin/audit-log/{id}`

Returns the same metadata plus the relevant changed fields in
`oldValuesJson`/`newValuesJson`. Audit data never contains passwords, OTPs,
JWTs, refresh/CSRF tokens, or signing keys. There are no audit mutation
endpoints.

---

# 20. Notifications

Planned, not implemented in Sprint 3.2.

## GET `/notifications`

Returns customer or employee notifications for the authenticated account.

## POST `/notifications/{id}/read`

## POST `/notifications/read-all`

---

# 21. Media Upload

Implemented in Sprint 3.3.

## POST `/admin/media/images`

Authorization policy: `CanManageMenu` (`MenuManager` or `Administrator`).

Request field: `file`.

Content type:

```text
multipart/form-data
```

### Response

Returns `201 Created`:

```json
{
  "id": "uuid",
  "originalFileName": "cappuccino.jpg",
  "contentType": "image/jpeg",
  "fileSizeBytes": 245678,
  "width": 1200,
  "height": 800,
  "url": "/media/ab/cd/generated-image.jpg",
  "createdAt": "2026-08-06T10:00:00Z"
}
```

The API never returns a physical path. `originalFileName` is sanitized leaf-name
metadata only; it is never used as a storage key.

Validation and normalization:

- non-empty file, at most configured encoded bytes (5 MiB by default);
- submitted MIME must be one of `image/jpeg`, `image/png`, or `image/webp`;
- actual decoded format must match the submitted MIME;
- SVG, GIF, executables, renamed content, malformed images, and multi-frame
  images are rejected;
- dimensions are at most 6000 x 6000 by default;
- estimated RGBA decoded bytes are at most 256 MiB by default;
- the complete image is decoded and re-encoded in the same format, which strips
  metadata, embedded non-image/trailing content, and scripts;
- a cryptographically random, partitioned storage key is generated;
- metadata plus a `MediaImageUploaded` employee audit entry are saved;
- database/audit failure removes the newly written object.

Expected failures use `ProblemDetails`, including `errors.file`.

## GET `/media/{storageKey}`

This public route is rooted at `/media`, not `/api/v1`. It returns only a
non-deleted `MediaFile` whose provider/key matches an existing object beneath
the configured storage root.

Successful responses use the verified content type, range processing, an ETag,
and:

```text
Cache-Control: public,max-age=31536000,immutable
```

Missing metadata, deleted metadata, missing objects, malformed keys, and
traversal attempts return `404`. Directory browsing and arbitrary filesystem
reads are not enabled.

Assigning a different image or `imageId:null` changes only the product
reference. Previous media metadata and bytes remain for deliberate future
cleanup.

---

# 22. SignalR Hub

Hub endpoint:

```text
/hubs/notifications
```

Client groups:

- `customer:{customerId}`
- `role:OrderReception`
- `role:Kitchen`
- `role:Pickup`
- `staff:all`

Main events:

- `OrderCreated`
- `OrderConfirmed`
- `OrderStatusChanged`
- `EstimatedReadyTimeChanged`
- `OrderReady`
- `OrderCompleted`
- `PaymentStatusChanged`
- `RefundStatusChanged`
- `DashboardCountersChanged`

All SignalR events contain only data the recipient is authorized to see.

---

# 23. Concurrency

Mutable staff actions must use optimistic concurrency.

Requests include:

```json
{
  "rowVersion": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
}
```

Conflict response:

```json
{
  "type": "concurrency_conflict",
  "title": "Menu item was changed by another employee",
  "status": 409,
  "code": "MENU_VERSION_CONFLICT",
  "currentResource": {
    "id": "uuid",
    "rowVersion": "current-guid"
  }
}
```

Sprint 3.2 menu row versions are GUID strings. Every successful update returns
a new value. Pre-detected stale writes include `currentResource`; a race caught
by EF still returns `409 MENU_VERSION_CONFLICT` without exposing internals.

Menu error codes include:

```text
CATEGORY_NOT_FOUND
PRODUCT_NOT_FOUND
OPTION_GROUP_NOT_FOUND
OPTION_VALUE_NOT_FOUND
MEDIA_FILE_NOT_FOUND
CATEGORY_ALREADY_DELETED
PRODUCT_ALREADY_DELETED
DUPLICATE_OPTION_VALUE_NAME
PRODUCT_OPTION_GROUP_ALREADY_ASSIGNED
PRODUCT_OPTION_VALUE_ALREADY_ASSIGNED
OPTION_VALUE_GROUP_MISMATCH
INVALID_OPTION_SELECTION_RULES
MULTIPLE_DEFAULT_VALUES_NOT_ALLOWED
MENU_CONFIGURATION_INVALID
MENU_VERSION_CONFLICT
FORBIDDEN
VALIDATION_ERROR
```

Category/product duplicate-name codes are not used because Sprint 3.1
explicitly permits duplicate normalized names for those entities. Option-value
names remain uniquely enforced per non-deleted group.

---

# 24. Pagination

Default:

```text
page=1
pageSize=20
```

Maximum page size:

```text
100
```

Common paged response:

```json
{
  "items": [],
  "page": 1,
  "pageSize": 20,
  "totalCount": 0,
  "totalPages": 0
}
```

---

# 25. API Versioning

Version 1 routes may be exposed as:

```text
/api/v1
```

The implementation should use ASP.NET API versioning from the beginning to avoid breaking future clients.
