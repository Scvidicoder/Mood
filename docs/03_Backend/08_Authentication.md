
# Authentication and Authorization

Version: 1.2 (Sprint 3.7 staff order authorization)

## 1. Purpose

This document defines authentication, session management, password security, token handling and role-based authorization for Mood Pickup System.

The system uses two separate authentication flows:

- Customers: phone number + one-time Telegram code
- Employees: username + password

Both flows issue JWT access tokens and rotating refresh-token sessions. The
refresh token is transported only in a Secure, HttpOnly cookie and is never
returned in JSON.

---

# 2. Customer Authentication

## 2.1 Customer Identity

A customer account is identified by a verified phone number.

Stored customer identity fields:

- CustomerId
- Name
- PhoneNumber
- TelegramChatId
- CreatedAt
- UpdatedAt

Phone numbers must be stored in normalized international format.

Example:

```text
+992900000000
```

The phone number must be unique.

## 2.2 No Password

Customers do not have passwords.

Every new login begins with phone verification.

Existing access tokens may keep the session active until expiration.

## 2.3 Telegram Linking

Telegram cannot send a message to an arbitrary phone number without prior user interaction.

The linking flow is:

1. Customer enters a phone number on the website.
2. Backend creates an authentication challenge.
3. Website shows a deep link to the Telegram bot.
4. Customer opens the bot.
5. Bot requests the customer's phone number through Telegram contact sharing.
6. Backend verifies that the shared number matches the challenge.
7. Backend stores the TelegramChatId link.
8. Bot sends the one-time code.

A Telegram account must not be linked to multiple customer accounts with different verified phone numbers.

Real mode implements this flow through a typed Bot API client and the
`/api/v1/telegram/webhook` endpoint. The website receives a 32-byte URL-safe
start token and a separate 32-byte client status secret. Only
domain-separated hashes are stored. `/start` binds the active challenge to one
private Telegram sender, and only a Telegram `contact` whose `user_id` matches
that sender can verify ownership. The same phone normalizer is used by the
website and contact path. Typed phone text, forwarded contacts, group chats,
bot messages, expired/replaced links, and conflicting identities are ignored
or rejected safely.

In `Development`, `Telegram:UseDevelopmentSender=true` preserves the approved
fake sender: any valid normalized number can receive an OTP and only that
sender logs it. The API never returns the code. The fake sender is rejected by
startup validation outside `Development`.

## 2.4 One-Time Code

Default rules:

- Six numeric digits
- Valid for five minutes
- One-time use
- Maximum five failed attempts
- Resend available after sixty seconds
- New code invalidates the previous code for the same challenge

The raw code must not be stored in the database.

Store only a secure hash and challenge metadata.

## 2.5 Challenge Data

Authentication challenge fields:

- Id
- PhoneNumber
- CodeHash
- TelegramChatId
- TelegramUserId
- TelegramUsername
- TelegramLinkTokenHash
- TelegramLinkExpiresAt
- TelegramStartedAt
- TelegramLinkUsedAt
- TelegramLinkedAt
- TelegramContactVerifiedAt
- TelegramLinkAttemptCount
- TelegramDeliveryFailureCount
- TelegramDeliveryFailedAt
- ClientStatusSecretHash
- OtpSentAt
- CreatedAt
- ExpiresAt
- AttemptCount
- MaximumAttempts
- IsUsed
- LastSentAt
- Purpose
- RequestIpHash
- UserAgentHash
- RowVersion

Possible purposes:

- Login
- Registration
- ChangePhoneNumber

## 2.6 Rate Limits

Rate limiting must be applied by:

- Phone number
- IP address
- Telegram chat
- Challenge

Recommended initial limits:

- Maximum five code requests per phone number per hour
- Maximum ten code requests per IP per hour
- Maximum five verification attempts per challenge
- Minimum sixty seconds between resends
- Maximum three mismatching contact submissions per challenge
- Fixed-window IP limits on link creation, webhook requests, and status polling
- Per-Telegram-user `/start` limits

These values must be configurable.

## 2.7 New Customer Registration

After successful OTP verification:

- If the phone number already exists, issue tokens.
- If the phone number does not exist, issue a short-lived registration token.

The registration token:

- Is valid for ten minutes
- Allows only registration completion
- Cannot access regular customer APIs
- Contains the verified phone number and challenge reference

Customer registration requires only a name.

## 2.8 Change Phone Number

Changing the phone number requires verification of the new number.

Rules:

- New number must not belong to another customer.
- Existing account data and order history remain unchanged.
- Existing refresh tokens are revoked after successful change.
- A new authenticated session is issued.

---

# 3. Employee Authentication

## 3.1 Employee Identity

Employees authenticate with:

- Username
- Password

Username must be unique and case-insensitive.

Recommended normalization:

```text
KITCHEN1 -> kitchen1
```

## 3.2 Password Storage

Passwords must be stored only as secure password hashes.

Recommended implementation:

- ASP.NET Core PasswordHasher
- Or Argon2id if introduced consistently

Plaintext passwords must never be logged or stored.

## 3.3 Password Policy

Initial policy:

- Minimum twelve characters
- At least one uppercase letter
- At least one lowercase letter
- At least one digit
- At least one special character
- Must not equal username
- Must not be one of the most common passwords

The policy must be configurable.

The current implementation uses a deterministic configuration-driven denylist.
Development contains a small documented default list, and deployments may
replace or extend it through configuration. A compromised-password service is
deferred and recorded in technical debt.

## 3.4 Temporary Password

When an administrator creates or resets an employee password:

- A temporary password is created.
- `MustChangePassword` is set to true.
- Employee may authenticate.
- Access is limited to password change and profile endpoints.
- Employee must set a new password before using staff functions.

## 3.5 Password Reset

Only an administrator may reset another employee's password.

Reset action:

- Generates or accepts a temporary password.
- Revokes all employee refresh tokens.
- Sets `MustChangePassword = true`.
- Writes an audit record.

## 3.6 Employee Deletion

The first version does not support temporary blocking.

When an employee is deleted:

- The account can no longer authenticate.
- Refresh tokens are revoked.
- Historical audit records remain.
- The employee record should use soft deletion where required for history.

---

# 4. Access Tokens

## 4.1 Format

Access tokens use JWT.

Current algorithm:

```text
HS256
```

The HS256 key is supplied through `Jwt:SigningKey`, must contain at least 32
characters, and must be replaced through secret configuration for deployment.
Issuer, audience, signature, lifetime, and expiration are always validated.
The token issuer is isolated behind an interface so RS256 can replace HS256
later without changing the authentication API contract. RS256 remains the
preferred future hardening step.

## 4.2 Lifetime

Recommended access token lifetime:

```text
15 minutes
```

The value must be configurable.

## 4.3 Customer Claims

Example claims:

- `sub`: customer ID
- `account_type`: customer
- `phone_number`
- `jti`
- `iat`
- `exp`

Do not place sensitive profile or order data in JWT claims.

## 4.4 Employee Claims

Example claims:

- `sub`: employee ID
- `account_type`: employee
- `username`
- `roles`
- `must_change_password`
- `jti`
- `iat`
- `exp`

## 4.5 Audience and Issuer

Tokens must validate:

- Signature
- Issuer
- Audience
- Expiration
- Not-before timestamp where used

Production tokens must never accept `ValidateIssuer = false` or `ValidateAudience = false`.

---

# 5. Refresh Tokens

## 5.1 Storage

Refresh tokens are opaque random values.

Store only a cryptographic hash in the database.

Fields:

- Id
- AccountType
- CustomerId or EmployeeId
- TokenHash
- CreatedAt
- ExpiresAt
- RevokedAt
- ReplacedByTokenId
- CreatedByIpHash
- RevokedByIpHash
- UserAgentHash

## 5.2 Lifetime

Recommended refresh token lifetime:

```text
30 days
```

Configurable separately for customers and employees.

## 5.3 Rotation

Refresh tokens must rotate on every use.

Flow:

1. Browser sends the refresh token through its HttpOnly cookie and supplies the
   matching double-submit CSRF header.
2. Backend verifies hash and status.
3. Old refresh token is revoked.
4. New access token is issued.
5. New refresh token is issued.
6. Old token stores a reference to its replacement.

## 5.4 Reuse Detection

If a revoked refresh token is used again:

- Treat as possible token theft.
- Revoke the entire refresh-token family.
- Require new authentication.
- Write a security log entry.

## 5.5 Logout

Logout revokes the current refresh token.

A future "logout all devices" action may revoke all active refresh tokens for the account.

---

# 6. Authorization

## 6.1 Customer Authorization

Customers may access only:

- Their own profile
- Their own orders
- Their own notifications

Sprint 3.6 enforces this with the `Customer` policy on `POST /orders`,
`GET /orders/mine`, and `GET /orders/{id}`. Checkout derives the customer ID,
name, and phone number from the validated access-token subject and the database
profile; the request cannot choose a customer or submit contact identity. An
order ID owned by another customer returns `404`, not a disclosure of the
other order's existence.

Sprint 3.5's anonymous cart is device-local frontend state and is not an
authenticated server resource. If a future backend cart is introduced, it must
be customer-owned and follow the ownership rule above.

Resource ownership must be checked on the server.

Never rely on the frontend to hide unauthorized IDs.

## 6.2 Employee Roles

Supported roles:

- Administrator
- OrderReception
- Kitchen
- Pickup
- MenuManager
- Cashier
- Manager

An employee may have multiple roles.

## 6.3 Role Permissions

### Administrator

Full access to all staff and admin functions.

### OrderReception

- View new orders
- View order details
- Confirm orders
- Reject eligible orders
- Set or change estimated ready time
- View customer contact information

### Kitchen

- View confirmed and preparing orders
- Start preparation
- Set or change estimated ready time
- Mark order ready
- View customer contact information in details
- Cannot record payment or complete an order

### Pickup

- View ready orders
- Record on-site payment
- Complete orders
- View customer contact information

### MenuManager

- Manage categories
- Manage products
- Manage option groups
- Manage availability
- Upload menu images

### Cashier and Manager

- View staff order lists and complete order details
- View active kitchen status
- Confirm pending orders and assign estimated ready time
- Reject pending orders with a reason
- Change the estimated ready time of confirmed orders

Cashier may record pickup payment and complete ready orders. Manager has
view-only kitchen access and cannot perform kitchen, payment, or completion
actions.

`MenuManager` alone has no order-management access.

## 6.4 Policy-Based Authorization

ASP.NET Core authorization policies should be used instead of role checks scattered across controllers.

Examples:

- `CanReceiveOrders`
- `CanWorkKitchen`
- `CanViewKitchen` (`Kitchen`, `Cashier`, `Manager`, `Pickup`, or Administrator)
- `CanIssueOrders`
- `CanManageMenu`
- `CanManageEmployees`
- `CanManageCafeSettings`
- `CanViewAuditLog`
- `CanManageOrders` (`Cashier`, `Manager`, or the Administrator override)

`CanWorkKitchen` is Kitchen or Administrator. `CanIssueOrders` is Cashier,
Pickup, or Administrator. The separate policies keep kitchen preparation and
customer handoff/payment mutually restricted while preserving Administrator
override.

Policies may allow Administrator automatically.

## 6.5 Multiple Roles

When an employee has multiple roles:

- Claims include all assigned roles.
- UI displays all permitted sections.
- Notifications are delivered for all relevant roles.
- Duplicate notifications for the same event should be merged where possible.

---

# 7. CSRF, CORS and Token Transport

## 7.1 Access Token Transport

Preferred browser design:

- Access token kept in application memory.
- Refresh token stored in a Secure, HttpOnly cookie.
- Access and refresh tokens are never stored in `localStorage` or
  `sessionStorage`.

This reduces exposure of the refresh token to JavaScript.

## 7.2 Cookie Requirements

Refresh cookie:

- `HttpOnly`
- `Secure`
- `SameSite=Lax` or stricter where compatible
- Narrow path, for example `/api/v1/auth`
- Appropriate expiration

The current refresh cookie uses `SameSite=Lax` and is restricted to
`/api/v1/auth`. The separate readable CSRF cookie uses path `/` because the
frontend must read it from routes such as `/login` and `/profile`.

## 7.3 CSRF Protection

Refresh and logout use double-submit CSRF protection:

- Backend issues a cryptographically random readable CSRF cookie.
- Frontend copies it into the `X-CSRF-TOKEN` header.
- Backend compares the two values in constant time.
- Missing or mismatched values return `403 CSRF_VALIDATION_FAILED`.
- Refresh rotates both the refresh token and the CSRF token.

## 7.4 CORS

Production CORS must allow only approved frontend origins.

Never use unrestricted origin with credentials.

Development origin examples may include local frontend ports.

---

# 8. Security Logging

Security-relevant events must be logged:

- Successful employee login
- Failed employee login
- OTP requested
- OTP verification failed
- OTP verification succeeded
- Refresh-token reuse detected
- Password reset
- Phone number changed
- Role assignment changed
- Account deleted
- Repeated rate-limit violations

Logs must never contain:

- Raw passwords
- Raw OTP codes
- Raw refresh tokens
- Full JWT tokens

Phone numbers should be masked in general logs.

---

# 9. Session and Device Behavior

## 9.1 Multiple Devices

Customers and employees may have multiple active sessions.

Each session has its own refresh-token family.

## 9.2 Session Revocation

Revoke sessions when:

- Employee password is reset
- Employee account is deleted
- Customer phone number changes
- Refresh-token reuse is detected
- Administrator performs future forced logout action

## 9.3 Clock Skew

JWT validation may allow a small clock skew, for example:

```text
30 seconds
```

Do not use large default clock skew.

---

# 10. SignalR Authentication

SignalR connections require a valid access token.

The backend must:

- Authenticate the connection
- Resolve customer or employee identity
- Add the connection to authorized groups only
- Remove the connection from groups on disconnect

Customers may join only:

```text
customer:{customerId}
```

Employees may join role groups based on current role assignments.

Role changes should take effect on the next token refresh or forced reconnect.

---

# 11. Telegram Bot Security

Bot webhook requests use Telegram's `secret_token` feature. The exact
`X-Telegram-Bot-Api-Secret-Token` header is compared with the configured
secret in constant time before model binding. The webhook is anonymous,
private-message-only, limited to 64 KiB, IP rate limited, and durably
idempotent by `update_id`.

The bot must not trust user-supplied phone text.

Only Telegram's contact-sharing payload should be accepted for initial phone linking.

Bot commands and callbacks must be associated with the correct Telegram user and active challenge.

One authentication challenge must not be reusable by another Telegram account.

At startup, real mode optionally calls `getMe`, validates the configured bot
username, registers `{PublicBaseUrl}{WebhookPath}` with `setWebhook` for only
`message` updates, and verifies it using `getWebhookInfo`. Registration failure
fails startup. The cached startup state participates in readiness; liveness
does not depend on Telegram and health requests do not call Telegram.

---

# 12. Error Handling

Authentication errors should be specific enough for the user but not reveal sensitive account existence unnecessarily.

Examples:

- "Invalid or expired code."
- "Unable to sign in."
- "Too many attempts. Try again later."

Employee login should not reveal whether the username or password was incorrect.

---

# 13. Configuration

Authentication configuration must come from environment variables or secret storage.

Required configuration includes:

- JWT issuer
- JWT audience
- Signing key or certificate
- Access-token lifetime
- Refresh-token lifetime
- OTP lifetime
- OTP retry limits
- Telegram bot token
- Telegram bot username
- Telegram webhook secret
- Telegram public HTTPS backend base URL
- Telegram webhook path and startup-registration flags
- Telegram Development-sender selection
- Allowed frontend origins
- Cookie settings

Secrets must not be committed to Git.

---

# 14. Development Mode

Development may use:

- Fake Telegram sender
- Local JWT signing key
- Fake customer and employee accounts

Development shortcuts must:

- Be disabled automatically outside Development
- Be clearly documented
- Never be controlled by a public request parameter

The Development fake sender generates random six-digit codes; it does not use a
fixed public test code. It is registered only in `Development`. Automated tests
replace it with a non-logging in-memory test double.

Real mode uses `TelegramOtpSender`. It sends the configurable OTP message
through `sendMessage`, removes the contact keyboard, and logs only the
challenge ID. Bot token, webhook secret, start token, status secret, contact
phone, and OTP are never written to production logs.
