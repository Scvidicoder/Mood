
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

Sprint 3.5 implements steps 1-4 as an anonymous device-local draft. Steps 5-9
require Sprint 3.6 backend checkout/order creation and are not currently
callable.

1. Browse menu.
2. Configure products.
3. Add products to cart.
4. Review cart.
5. Select:
   - ASAP
   - Pickup time
6. Select payment method.
7. Add optional comment.
8. Review order.
9. Confirm.

Alternative:
- Online payment redirects to payment provider.
- Pay on pickup creates the order immediately.

---

## UF-004 Order Processing

Customer
→ Pending Confirmation

Reception
→ Confirm

Kitchen
→ Preparing

Kitchen
→ Ready for Pickup

Pickup
→ Completed

---

## UF-005 Customer Cancellation

Precondition:
Order status is not Preparing.

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
4. Unavailable options are replaced with defaults when possible.
5. Customer reviews updated cart.
