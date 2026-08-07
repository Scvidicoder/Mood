import { cartActions, selectCart } from "../features/cart/cartSlice";
import { useAppDispatch, useAppSelector } from "../store";

export function CartGlobalStatus() {
  const dispatch = useAppDispatch();
  const cart = useAppSelector(selectCart);
  const warning =
    cart.persistenceWarning ?? cart.restorationNotice?.message;

  return (
    <>
      <p aria-live="polite" className="visually-hidden" role="status">
        {cart.announcement ?? ""}
      </p>
      {warning ? (
        <aside className="cart-global-notice" role="status">
          <div>
            <strong>Cart notice</strong>
            <span>{warning}</span>
          </div>
          <button
            aria-label="Dismiss cart notice"
            onClick={() => dispatch(cartActions.dismissCartNotice())}
            type="button"
          >
            Dismiss
          </button>
        </aside>
      ) : null}
    </>
  );
}
