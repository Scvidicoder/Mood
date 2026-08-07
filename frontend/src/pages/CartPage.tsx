import { memo, useEffect, useRef } from "react";
import { Link } from "react-router-dom";
import { PublicProductImage } from "../components/PublicProductImage";
import {
  cartActions,
  selectCartHasBlockingIssues,
  selectCartItems,
  selectCartSubtotalMinor,
  selectCartTotalQuantity,
} from "../features/cart/cartSlice";
import { useCartRevalidation } from "../features/cart/useCartRevalidation";
import { useAppDispatch, useAppSelector } from "../store";
import type { CartLine } from "../types/cart";
import { MAX_CART_QUANTITY } from "../types/cart";
import { formatMoneyMinor } from "../utils/format";

export function CartPage() {
  const dispatch = useAppDispatch();
  const items = useAppSelector(selectCartItems);
  const subtotalMinor = useAppSelector(selectCartSubtotalMinor);
  const totalQuantity = useAppSelector(selectCartTotalQuantity);
  const hasBlockingIssues = useAppSelector(selectCartHasBlockingIssues);
  const validation = useCartRevalidation();
  const headingRef = useRef<HTMLHeadingElement>(null);

  useEffect(() => {
    const previousTitle = document.title;
    document.title = "Your cart - Mood Pickup";
    headingRef.current?.focus();
    return () => {
      document.title = previousTitle;
    };
  }, []);

  if (items.length === 0) {
    return (
      <section className="cart-page">
        <div className="cart-page__inner cart-empty">
          <span aria-hidden="true" className="menu-feedback__mark">
            MP
          </span>
          <p className="eyebrow">Your local cart</p>
          <h1 ref={headingRef} tabIndex={-1}>
            Your cart is empty.
          </h1>
          <p>
            Browse the current menu, configure something you like, and it will
            appear here.
          </p>
          <Link className="button button-link" to="/">
            Browse the menu
          </Link>
        </div>
      </section>
    );
  }

  return (
    <section className="cart-page">
      <div className="cart-page__inner">
        <div className="cart-heading">
          <div>
            <p className="eyebrow">Your local cart</p>
            <h1 ref={headingRef} tabIndex={-1}>
              Review your choices
            </h1>
            <p>
              {totalQuantity} {totalQuantity === 1 ? "item" : "items"} stored
              on this device.
            </p>
          </div>
          <button
            className="text-button text-button--danger"
            onClick={() => dispatch(cartActions.clearCart())}
            type="button"
          >
            Clear cart
          </button>
        </div>

        <div className="cart-revalidation-toolbar">
          <p aria-live="polite" className="cart-revalidation-status">
            {validation.isFetching
              ? "Checking current menu details..."
              : "Cart items were checked against cached current product details."}
          </p>
          <button
            disabled={validation.isFetching}
            onClick={() => void validation.refresh()}
            type="button"
          >
            Refresh item checks
          </button>
        </div>

        <div className="cart-layout">
          <div className="cart-lines">
            {items.map((line) => (
              <CartLineCard key={line.id} line={line} />
            ))}
          </div>

          <aside aria-labelledby="cart-summary-title" className="cart-summary">
            <p className="eyebrow">Summary</p>
            <h2 id="cart-summary-title">Cart total</h2>
            <dl>
              <div>
                <dt>Item quantity</dt>
                <dd>{totalQuantity}</dd>
              </div>
              <div className="cart-summary__total">
                <dt>Subtotal</dt>
                <dd>{formatMoneyMinor(subtotalMinor)}</dd>
              </div>
            </dl>
            {hasBlockingIssues ? (
              <p className="cart-summary__warning">
                Review the flagged items before checkout is introduced.
              </p>
            ) : null}
            <div className="cart-next-sprint">
              <strong>Checkout is coming in Sprint 3.6.</strong>
              <p>
                No order has been created. The backend will recheck prices,
                options, and availability before a future order is accepted.
              </p>
            </div>
            <Link className="button button-link button-secondary" to="/">
              Continue browsing
            </Link>
          </aside>
        </div>
      </div>
    </section>
  );
}

const CartLineCard = memo(function CartLineCard({
  line,
}: {
  line: CartLine;
}) {
  const dispatch = useAppDispatch();
  const status = statusLabel(line);
  return (
    <article
      aria-labelledby={`cart-line-${line.id}`}
      className={`cart-line cart-line--${line.state}`}
    >
      <div className="cart-line__image">
        <PublicProductImage
          alt={line.productName}
          imageUrl={line.imageUrl}
          variant="card"
        />
      </div>
      <div className="cart-line__content">
        <div className="cart-line__heading">
          <div>
            <h2 id={`cart-line-${line.id}`}>{line.productName}</h2>
            <span className={`cart-line-state cart-line-state--${line.state}`}>
              {status}
            </span>
          </div>
          <strong>{formatMoneyMinor(line.unitPriceMinor * line.quantity)}</strong>
        </div>

        {line.selectedOptions.length > 0 ? (
          <ul aria-label={`Selected options for ${line.productName}`}>
            {line.selectedOptions.map((option) => (
              <li key={`${option.groupId}:${option.optionValueId}`}>
                <span>{option.groupName}</span>
                <strong>{option.valueName}</strong>
              </li>
            ))}
          </ul>
        ) : (
          <p className="cart-line__plain">No options selected.</p>
        )}

        {line.messages.length > 0 ? (
          <ul className="cart-line__messages">
            {line.messages.map((message, index) => (
              <li key={`${message}-${index}`}>{message}</li>
            ))}
          </ul>
        ) : null}

        <div className="cart-line__footer">
          <div
            aria-label={`Quantity for ${line.productName}`}
            className="quantity-control"
            role="group"
          >
            <button
              aria-label={`Decrease ${line.productName} quantity`}
              onClick={() =>
                dispatch(cartActions.decreaseQuantity(line.id))
              }
              type="button"
            >
              -
            </button>
            <output aria-live="polite">{line.quantity}</output>
            <button
              aria-label={`Increase ${line.productName} quantity`}
              disabled={line.quantity >= MAX_CART_QUANTITY}
              onClick={() =>
                dispatch(cartActions.increaseQuantity(line.id))
              }
              type="button"
            >
              +
            </button>
          </div>
          <span className="cart-line__unit">
            {formatMoneyMinor(line.unitPriceMinor)} each
          </span>
          <div className="cart-line__actions">
            <Link
              state={{ from: "/cart" }}
              to={`/product/${line.productId}?editLine=${encodeURIComponent(line.id)}`}
            >
              Edit options
            </Link>
            <button
              aria-label={`Remove ${line.productName} from cart`}
              onClick={() => dispatch(cartActions.removeLine(line.id))}
              type="button"
            >
              Remove
            </button>
          </div>
        </div>
      </div>
    </article>
  );
});

function statusLabel(line: CartLine): string {
  switch (line.state) {
    case "checking":
      return "Checking";
    case "updated":
      return "Updated";
    case "needsAttention":
      return "Needs attention";
    case "unavailable":
      return "Unavailable";
    default:
      return "Current";
  }
}
