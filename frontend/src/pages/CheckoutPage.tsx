import { useMutation } from "@tanstack/react-query";
import { useEffect, useRef } from "react";
import { useForm } from "react-hook-form";
import { Link, useNavigate } from "react-router-dom";
import { createOrder } from "../api/orders";
import { ApiError } from "../api/client";
import { ErrorState } from "../components/ErrorState";
import {
  cartActions,
  selectCartHasBlockingIssues,
  selectCartItems,
  selectCartSubtotalMinor,
} from "../features/cart/cartSlice";
import { clearBrowserCartStorage } from "../features/cart/cartStorage";
import { useCartRevalidation } from "../features/cart/useCartRevalidation";
import { useAppDispatch, useAppSelector } from "../store";
import type {
  CreateOrderInput,
  PaymentMethod,
  PickupMode,
} from "../types/orders";
import { formatMoneyMinor } from "../utils/format";

interface CheckoutFormValues {
  comment: string;
  paymentMethod: PaymentMethod;
  pickupMode: PickupMode;
  requestedPickupTime: string;
}

export function CheckoutPage() {
  const dispatch = useAppDispatch();
  const navigate = useNavigate();
  const headingRef = useRef<HTMLHeadingElement>(null);
  const orderCreatedRef = useRef(false);
  const items = useAppSelector(selectCartItems);
  const subtotalMinor = useAppSelector(selectCartSubtotalMinor);
  const hasBlockingIssues = useAppSelector(selectCartHasBlockingIssues);
  const revalidation = useCartRevalidation();
  const form = useForm<CheckoutFormValues>({
    defaultValues: {
      comment: "",
      paymentMethod: "PayOnPickup",
      pickupMode: "AsSoonAsPossible",
      requestedPickupTime: "",
    },
  });
  const pickupMode = form.watch("pickupMode");
  const createMutation = useMutation({
    mutationFn: createOrder,
    onSuccess: (order) => {
      orderCreatedRef.current = true;
      dispatch(cartActions.clearCart());
      clearBrowserCartStorage();
      navigate(`/order-success/${order.id}`, {
        replace: true,
        state: { order },
      });
    },
  });

  useEffect(() => {
    if (items.length === 0 && !orderCreatedRef.current) {
      navigate("/", { replace: true });
    }
  }, [items.length, navigate]);

  useEffect(() => {
    const previousTitle = document.title;
    document.title = "Checkout - Mood Pickup";
    headingRef.current?.focus();
    return () => {
      document.title = previousTitle;
    };
  }, []);

  function submit(values: CheckoutFormValues) {
    const input: CreateOrderInput = {
      items: items.map((item) => ({
        productId: item.productId,
        optionValueIds: item.selectedOptions.map((option) => option.optionValueId),
        quantity: item.quantity,
        comment: null,
      })),
      comment: values.comment.trim() || null,
      paymentMethod: values.paymentMethod,
      pickupMode: values.pickupMode,
      requestedPickupTime:
        values.pickupMode === "Scheduled" && values.requestedPickupTime
          ? new Date(values.requestedPickupTime).toISOString()
          : null,
    };
    createMutation.mutate(input);
  }

  if (items.length === 0) {
    return null;
  }

  return (
    <section className="checkout-page">
      <div className="checkout-page__inner">
        <Link className="product-detail-back" to="/cart">
          <span aria-hidden="true">&larr;</span> Back to cart
        </Link>
        <div className="checkout-heading">
          <p className="eyebrow">Secure checkout</p>
          <h1 ref={headingRef} tabIndex={-1}>Review and place your order</h1>
          <p>
            The café will use your verified customer name and phone number from
            your account. Prices and availability are checked again before an
            order is created.
          </p>
        </div>

        <div className="checkout-layout">
          <form className="checkout-form" onSubmit={form.handleSubmit(submit)}>
            <fieldset>
              <legend>Pickup</legend>
              <label className="checkout-choice">
                <input
                  type="radio"
                  value="AsSoonAsPossible"
                  {...form.register("pickupMode")}
                />
                <span>
                  <strong>Prepare ASAP</strong>
                  <small>The café will prepare your order as soon as possible.</small>
                </span>
              </label>
              <label className="checkout-choice">
                <input
                  type="radio"
                  value="Scheduled"
                  {...form.register("pickupMode")}
                />
                <span>
                  <strong>Schedule a pickup</strong>
                  <small>Today only, in 15-minute intervals within the next 4 hours.</small>
                </span>
              </label>
              {pickupMode === "Scheduled" ? (
                <label className="checkout-field">
                  Requested pickup time
                  <input
                    aria-invalid={Boolean(form.formState.errors.requestedPickupTime)}
                    type="datetime-local"
                    {...form.register("requestedPickupTime", {
                      required: "Choose a requested pickup time.",
                    })}
                  />
                  <small>Business hours are currently 10:00-22:00 café time.</small>
                  {form.formState.errors.requestedPickupTime ? (
                    <span className="checkout-field__error" role="alert">
                      {form.formState.errors.requestedPickupTime.message}
                    </span>
                  ) : null}
                </label>
              ) : null}
            </fieldset>

            <fieldset>
              <legend>Payment</legend>
              <label className="checkout-choice">
                <input
                  type="radio"
                  value="PayOnPickup"
                  {...form.register("paymentMethod")}
                />
                <span><strong>Pay on pickup</strong><small>Pay when you collect your order.</small></span>
              </label>
              <label className="checkout-choice">
                <input
                  type="radio"
                  value="Online"
                  {...form.register("paymentMethod")}
                />
                <span><strong>Online payment</strong><small>Saved as your choice; payment processing is not connected yet.</small></span>
              </label>
            </fieldset>

            <label className="checkout-field">
              Comment for the café <span>(optional)</span>
              <textarea maxLength={500} rows={4} {...form.register("comment")} />
            </label>

            {createMutation.error ? <CheckoutError error={createMutation.error} /> : null}
            {hasBlockingIssues || revalidation.isFetching ? (
              <p className="checkout-warning" role="alert">
                {revalidation.isFetching
                  ? "Cart checks are still running. Please wait before placing the order."
                  : "Review the cart’s flagged items before checkout."}
              </p>
            ) : null}
            <button
              className="button checkout-submit"
              disabled={
                createMutation.isPending || hasBlockingIssues || revalidation.isFetching
              }
              type="submit"
            >
              {createMutation.isPending ? "Creating order…" : "Create order"}
            </button>
          </form>

          <aside aria-labelledby="checkout-summary-title" className="checkout-summary">
            <p className="eyebrow">Order summary</p>
            <h2 id="checkout-summary-title">Your selections</h2>
            <ul>
              {items.map((item) => (
                <li key={item.id}>
                  <div>
                    <strong>{item.productName}</strong>
                    <span>Quantity {item.quantity}</span>
                    {item.selectedOptions.length > 0 ? (
                      <small>
                        {item.selectedOptions
                          .map((option) => `${option.groupName}: ${option.valueName}`)
                          .join(" · ")}
                      </small>
                    ) : null}
                  </div>
                  <strong>{formatMoneyMinor(item.unitPriceMinor * item.quantity)}</strong>
                </li>
              ))}
            </ul>
            <dl>
              <div><dt>Subtotal</dt><dd>{formatMoneyMinor(subtotalMinor)}</dd></div>
              <div><dt>Discount</dt><dd>TJS 0.00</dd></div>
              <div className="checkout-summary__total"><dt>Grand total</dt><dd>{formatMoneyMinor(subtotalMinor)}</dd></div>
            </dl>
          </aside>
        </div>
      </div>
    </section>
  );
}

function CheckoutError({ error }: { error: unknown }) {
  const fieldErrors = error instanceof ApiError ? error.errors : undefined;
  return (
    <div className="checkout-error" role="alert">
      {fieldErrors ? (
        <ul>
          {Object.entries(fieldErrors).flatMap(([field, messages]) =>
            messages.map((message) => <li key={`${field}-${message}`}>{message}</li>),
          )}
        </ul>
      ) : <ErrorState error={error} />}
    </div>
  );
}
