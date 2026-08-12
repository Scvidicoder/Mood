
# User Flows

Version: 1.0

## UF-001 Customer Registration

### Goal
Create a new customer account.

### Actors
- Customer
- Telegram Bot

### Flow
1. Customer enters phone number.
2. System checks whether the account exists.
3. Website shows the backend-provided Telegram deep link.
4. Customer opens the bot and shares their own contact using Telegram's
   contact button.
5. Backend verifies the Telegram sender and matching normalized phone number.
6. Telegram bot sends a one-time code.
7. Customer enters the code.
8. If this is the first login, customer enters a name.
9. System transfers the verified Telegram chat link to the new account and
   signs the customer in.

### Result
Customer receives access and JWT tokens.

---

## UF-002 Customer Login

1. Enter phone number.
2. If the customer already has a verified Telegram link, receive the code
   immediately. Otherwise complete the same deep-link and own-contact flow as
   registration.
3. Enter the Telegram code.
4. Login succeeds.

---

## UF-003 Place Order

Sprint 3.5 implements steps 1-4 as an anonymous device-local draft and Sprint
3.6 implements checkout/order creation. Sprint 3.7 adds the staff decision and
customer tracking that follow step 9.

1. Browse menu.
2. Configure products.
3. Add products to cart.
4. Review cart.
5. Select:
   - ASAP
   - One backend-provided 15-minute pickup time for today
6. Select payment method.
7. Add optional comment.
8. Review order.
9. Confirm.

Alternative:
- Online payment creates a pending payment and submits an ephemeral POST form
  to Alif WebCheckout. Alif returns the browser to `/payment/result`; that page
  loads server state and performs bounded server-side verification while
  SignalR supplies live updates.
- Pay on pickup creates the order immediately.

---

## UF-004 Order Processing

Customer
→ Pending Confirmation

Reception
→ Confirm and set estimated ready time

Kitchen
→ Preparing

Kitchen
→ Ready for Pickup

Pickup
→ Completed

---

## UF-005 Customer Cancellation

Precondition:
Order status is PendingConfirmation.

Flow:
1. Customer presses Cancel.
2. System validates status.
3. Order is cancelled.
4. Refund starts automatically if payment was online.

---

## UF-006 Kitchen Workflow

1. Kitchen receives notification.
2. Opens kitchen board.
3. Starts preparation.
4. Optionally updates estimated ready time.
5. Marks order as Ready.

---

## UF-007 Pickup Workflow

1. Pickup employee opens Ready board.
2. Verifies payment.
3. Records cash/card if required.
4. Hands over the order.
5. Marks Completed.

---

## UF-008 Repeat Order

1. Customer opens order history.
2. Selects Repeat order.
3. System validates products and options.
4. Unavailable or incompatible products/options are reported without silent
   removal, replacement, or substitution.
5. Customer explicitly chooses which currently available lines to add and
   reviews the updated cart.

---

## UF-009 Create Employee

1. Administrator opens `/staff/employees` and selects Create employee.
2. Administrator enters a trimmed full name, safe unique username, and one or
   more existing roles.
3. Backend generates and hashes a secure temporary password in the same
   transaction as the employee and audit record.
4. UI displays the raw temporary password once with a copy action.
5. Employee signs in and is redirected to the staff profile password-change
   form before ordinary staff functions become available.

## UF-010 Change Employee Roles

1. Administrator opens employee details.
2. Administrator changes identity fields and the multi-role selection.
3. Backend validates the latest row version, username uniqueness, known roles,
   and last-Administrator protection.
4. The atomic update returns a new row version and writes identity/role audit
   entries.

## UF-011 Disable and Enable Employee

1. Administrator confirms Disable.
2. Backend preserves the employee, revokes refresh sessions, advances the
   employee session version, and writes an audit record.
3. Login and previously issued staff access tokens stop working.
4. Administrator may enable the account later; the employee must sign in again.

## UF-012 Reset Employee Password

1. Administrator confirms Reset password with the current row version.
2. Backend replaces the password hash, forces password change, revokes sessions,
   invalidates existing access tokens, and writes a secret-free audit record.
3. UI displays the new temporary password once and confirms session revocation.
