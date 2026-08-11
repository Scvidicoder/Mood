import { useMutation } from "@tanstack/react-query";
import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { repeatOrder } from "../api/orders";
import { cartActions } from "../features/cart/cartSlice";
import { repeatItemToCartLine } from "../features/orders/repeatOrder";
import { useAppDispatch } from "../store";
import type { RepeatOrderResult } from "../types/orders";
import { ErrorState } from "./ErrorState";

export function RepeatOrderButton({
  orderId,
  orderNumber,
}: {
  orderId: string;
  orderNumber: string;
}) {
  const dispatch = useAppDispatch();
  const navigate = useNavigate();
  const [result, setResult] = useState<RepeatOrderResult>();
  const validation = useMutation({
    mutationFn: () => repeatOrder(orderId),
    onSuccess: setResult,
  });

  function addAvailableItems() {
    if (!result) {
      return;
    }
    const now = new Date().toISOString();
    result.availableItems.forEach((item) => {
      dispatch(cartActions.addConfiguredLine(repeatItemToCartLine(item, now)));
    });
    navigate("/cart");
  }

  return (
    <>
      <button
        className="button button-secondary"
        disabled={validation.isPending}
        onClick={() => validation.mutate()}
        type="button"
      >
        {validation.isPending ? "Checking menu…" : "Repeat order"}
      </button>
      {validation.error ? <ErrorState error={validation.error} /> : null}
      {result ? (
        <div
          aria-labelledby={`repeat-title-${orderId}`}
          aria-modal="true"
          className="repeat-order-dialog-backdrop"
          role="dialog"
        >
          <div className="repeat-order-dialog">
            <div className="repeat-order-dialog__heading">
              <div>
                <p className="eyebrow">Current menu check</p>
                <h2 id={`repeat-title-${orderId}`}>Repeat {orderNumber}</h2>
              </div>
              <button
                aria-label="Close repeat order summary"
                className="icon-button"
                onClick={() => setResult(undefined)}
                type="button"
              >
                ×
              </button>
            </div>

            {result.availableItems.length ? (
              <section>
                <h3>Ready to add</h3>
                <ul className="repeat-order-list">
                  {result.availableItems.map((item, index) => (
                    <li key={`${item.productId}-${index}`}>
                      <strong>{item.quantity} × {item.productName}</strong>
                      {item.options.length ? (
                        <span>{item.options.map((option) => option.optionValueName).join(", ")}</span>
                      ) : null}
                    </li>
                  ))}
                </ul>
              </section>
            ) : (
              <p>No items from this order can currently be added.</p>
            )}

            {result.unavailableItems.length ? (
              <section className="repeat-order-unavailable">
                <h3>Unavailable items</h3>
                <p>These items will not be added. Nothing was substituted.</p>
                <ul className="repeat-order-list">
                  {result.unavailableItems.map((item, index) => (
                    <li key={`${item.productName}-${index}`}>
                      <strong>{item.quantity} × {item.productName}</strong>
                      <span>{item.reasons.join(" ")}</span>
                    </li>
                  ))}
                </ul>
              </section>
            ) : (
              <p className="menu-feedback">Every item is available with its original options.</p>
            )}

            <div className="repeat-order-dialog__actions">
              <button
                className="button button-secondary"
                onClick={() => setResult(undefined)}
                type="button"
              >
                Cancel
              </button>
              <button
                className="button"
                disabled={result.availableItems.length === 0}
                onClick={addAvailableItems}
                type="button"
              >
                Add available items to cart
              </button>
            </div>
          </div>
        </div>
      ) : null}
    </>
  );
}
