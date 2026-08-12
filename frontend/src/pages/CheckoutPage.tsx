import { useMutation, useQuery } from "@tanstack/react-query";
import { useEffect, useRef } from "react";
import { useForm } from "react-hook-form";
import { Link, useNavigate } from "react-router-dom";
import { createOrder, getPickupSlots } from "../api/orders";
import { getProfile } from "../api/profile";
import { ApiError } from "../api/client";
import {
  cartActions,
  selectCartHasBlockingIssues,
  selectCartItems,
  selectCartSubtotalMinor,
} from "../features/cart/cartSlice";
import { clearBrowserCartStorage } from "../features/cart/cartStorage";
import { useCartRevalidation } from "../features/cart/useCartRevalidation";
import { launchHostedPayment } from "../features/payments/launchHostedPayment";
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
  const submissionStartedRef = useRef(false);
  const items = useAppSelector(selectCartItems);
  const subtotalMinor = useAppSelector(selectCartSubtotalMinor);
  const hasBlockingIssues = useAppSelector(selectCartHasBlockingIssues);
  const revalidation = useCartRevalidation();
  const profile = useQuery({
    queryKey: ["profile"],
    queryFn: ({ signal }) => getProfile(signal),
  });
  const pickupSlots = useQuery({
    queryKey: ["orders", "pickup-slots"],
    queryFn: ({ signal }) => getPickupSlots(signal),
  });
  const form = useForm<CheckoutFormValues>({
    defaultValues: {
      comment: "",
      paymentMethod: "PayOnPickup",
      pickupMode: "AsSoonAsPossible",
      requestedPickupTime: "",
    },
  });
  const pickupMode = form.watch("pickupMode");
  const requestedPickupTime = form.watch("requestedPickupTime");
  const createMutation = useMutation({
    mutationFn: createOrder,
    onSuccess: (order) => {
      orderCreatedRef.current = true;
      dispatch(cartActions.clearCart());
      clearBrowserCartStorage();
      if (order.paymentMethod === "Online" && order.paymentLaunch) {
        launchHostedPayment(order.paymentLaunch);
        return;
      }
      navigate(`/order-success/${order.id}`, {
        replace: true,
        state: { order },
      });
    },
    onError: () => {
      submissionStartedRef.current = false;
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
    if (submissionStartedRef.current) {
      return;
    }
    submissionStartedRef.current = true;
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
          ? values.requestedPickupTime
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
              <legend>Customer</legend>
              {profile.isLoading ? (
                <p className="checkout-inline-status">Loading your verified profile…</p>
              ) : profile.data ? (
                <dl className="checkout-customer">
                  <div><dt>Name</dt><dd>{profile.data.name}</dd></div>
                  <div><dt>Phone</dt><dd>{profile.data.phoneNumber}</dd></div>
                </dl>
              ) : (
                <p className="checkout-inline-status">
                  Your verified customer profile will be used for this order.
                </p>
              )}
            </fieldset>

            <fieldset>
              <legend>Pickup Time</legend>
              <p className="checkout-inline-status">
                Choose ASAP or an available time today.
              </p>
              <input type="hidden" {...form.register("pickupMode")} />
              <input
                type="hidden"
                {...form.register("requestedPickupTime", {
                  validate: (value) =>
                    pickupMode !== "Scheduled" ||
                    Boolean(value) ||
                    "Choose an available pickup time.",
                })}
              />
              <div className="pickup-time-chips" role="group" aria-label="Pickup time">
                <button
                  aria-pressed={pickupMode === "AsSoonAsPossible"}
                  className="pickup-time-chip"
                  onClick={() => {
                    form.setValue("pickupMode", "AsSoonAsPossible", { shouldValidate: true });
                    form.setValue("requestedPickupTime", "", { shouldValidate: true });
                  }}
                  type="button"
                >
                  ASAP
                </button>
                {pickupSlots.data?.slots.map((slot) => (
                  <button
                    aria-pressed={
                      pickupMode === "Scheduled" &&
                      requestedPickupTime === slot.startsAt
                    }
                    className="pickup-time-chip"
                    key={slot.startsAt}
                    onClick={() => {
                      form.setValue("pickupMode", "Scheduled", { shouldValidate: true });
                      form.setValue("requestedPickupTime", slot.startsAt, { shouldValidate: true });
                    }}
                    type="button"
                  >
                    {slot.label}
                  </button>
                ))}
              </div>
              {pickupSlots.isLoading ? (
                <p className="checkout-inline-status" role="status">
                  Loading available times…
                </p>
              ) : pickupSlots.error ? (
                <div className="checkout-inline-error" role="alert">
                  <span>Available times could not be loaded.</span>
                  <button onClick={() => void pickupSlots.refetch()} type="button">
                    Retry
                  </button>
                </div>
              ) : pickupSlots.data?.slots.length === 0 ? (
                <p className="checkout-inline-status">
                  No later pickup times are available today. ASAP is still available.
                </p>
              ) : null}
              {form.formState.errors.requestedPickupTime ? (
                <span className="checkout-field__error" role="alert">
                  {form.formState.errors.requestedPickupTime.message}
                </span>
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
                <span><strong>Online payment</strong><small>Continue to Alif&apos;s hosted checkout after the order is created.</small></span>
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
              {createMutation.isPending ? "Placing order…" : "Place Order"}
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
  const friendlyMessage =
    error instanceof ApiError && error.status === 409
      ? "The menu changed while your order was being placed. Review your cart and try again."
      : "We couldn’t place your order. Please check your connection and try again.";
  return (
    <div className="checkout-error" role="alert">
      {fieldErrors ? (
        <ul>
          {Object.entries(fieldErrors).flatMap(([field, messages]) =>
            messages.map((message) => <li key={`${field}-${message}`}>{message}</li>),
          )}
        </ul>
      ) : <p className="error-state">{friendlyMessage}</p>}
    </div>
  );
}
