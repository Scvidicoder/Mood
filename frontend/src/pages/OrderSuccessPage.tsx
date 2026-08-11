import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useRef } from "react";
import { Link, useLocation, useParams } from "react-router-dom";
import { cancelOrder, getOrder } from "../api/orders";
import { ErrorState } from "../components/ErrorState";
import { LoadingState } from "../components/LoadingState";
import { RepeatOrderButton } from "../components/RepeatOrderButton";
import { useOrderNotifications } from "../hooks/useOrderNotifications";
import type { OrderDetail, OrderStatus } from "../types/orders";
import { formatDate, formatMoney } from "../utils/format";
import {
  orderStatusLabel,
  paymentMethodLabel,
} from "../utils/orderPresentation";

interface OrderSuccessLocationState {
  order?: OrderDetail;
}

const progressSteps: Array<{ status: OrderStatus; label: string }> = [
  { status: "PendingConfirmation", label: "Created" },
  { status: "Confirmed", label: "Confirmed" },
  { status: "Preparing", label: "Preparing" },
  { status: "ReadyForPickup", label: "Ready for pickup" },
  { status: "Completed", label: "Completed" },
];

export function OrderSuccessPage() {
  const { id = "" } = useParams();
  const location = useLocation();
  const queryClient = useQueryClient();
  const headingRef = useRef<HTMLHeadingElement>(null);
  const initialOrder = (location.state as OrderSuccessLocationState | null)?.order;
  const connectionState = useOrderNotifications(id);
  const order = useQuery({
    queryKey: ["orders", id],
    queryFn: ({ signal }) => getOrder(id, signal),
    enabled: Boolean(id),
    initialData: initialOrder?.id === id ? initialOrder : undefined,
    refetchInterval: (query) =>
      connectionState === "Connected" ||
      (query.state.data && ["Completed", "Cancelled", "Rejected"].includes(query.state.data.status))
        ? false
        : 15_000,
  });
  const cancelMutation = useMutation({
    mutationFn: () => cancelOrder(id),
    onSuccess: (updated) => {
      queryClient.setQueryData(["orders", id], updated);
      void queryClient.invalidateQueries({ queryKey: ["orders", "mine"] });
      void queryClient.invalidateQueries({ queryKey: ["profile"] });
    },
  });

  useEffect(() => {
    const previousTitle = document.title;
    document.title = "Order details - Mood Pickup";
    headingRef.current?.focus();
    return () => {
      document.title = previousTitle;
    };
  }, []);

  if (order.isLoading) {
    return <section className="page"><LoadingState message="Loading your order…" /></section>;
  }
  if (order.error || !order.data) {
    return <section className="page"><ErrorState error={order.error} /></section>;
  }

  const value = order.data;
  const reachedStatuses = new Set(value.statusHistory.map((history) => history.newStatus));
  reachedStatuses.add("PendingConfirmation");

  return (
    <section className="page profile-order-details-page">
      <div className="page-heading profile-order-heading">
        <div>
          <p className="eyebrow">Order {value.orderNumber}</p>
          <h1 ref={headingRef} tabIndex={-1}>{statusHeading(value)}</h1>
          <p>{statusMessage(value)}</p>
        </div>
        <div className="profile-order-heading__status">
          <span className={`order-status order-status--${value.status.toLowerCase()}`}>
            {orderStatusLabel(value.status)}
          </span>
          <span className={`connection-state connection-state--${connectionState.toLowerCase()}`}>
            Live updates: {connectionState}
          </span>
        </div>
      </div>

      {value.rejectReason ? (
        <div className="order-terminal-message order-terminal-message--rejected">
          <strong>{value.status === "Cancelled" ? "Cancellation reason" : "Rejection reason"}</strong>
          <p>{value.rejectReason}</p>
        </div>
      ) : null}

      <div className="profile-order-layout">
        <div className="profile-order-main">
          <section className="profile-order-section">
            <div className="profile-section-heading">
              <div>
                <p className="eyebrow">Live progress</p>
                <h2>Order timeline</h2>
              </div>
            </div>
            <ol className="profile-order-timeline" aria-label="Order progress timeline">
              {progressSteps.map((step, index) => {
                const isComplete = reachedStatuses.has(step.status);
                const isCurrent = value.status === step.status;
                return (
                  <li
                    aria-current={isCurrent ? "step" : undefined}
                    className={`${isComplete ? "is-complete" : ""} ${isCurrent ? "is-current" : ""}`.trim()}
                    key={step.status}
                  >
                    <span aria-hidden="true" className="profile-order-timeline__marker">
                      {isComplete ? "✓" : index + 1}
                    </span>
                    <div>
                      <strong>{step.label}</strong>
                      {timelineDate(value, step.status) ? (
                        <span>{formatDate(timelineDate(value, step.status)!)}</span>
                      ) : (
                        <span>Pending</span>
                      )}
                    </div>
                  </li>
                );
              })}
            </ol>
            {value.status === "Cancelled" || value.status === "Rejected" ? (
              <p className="profile-order-terminal-status">
                This workflow ended as <strong>{orderStatusLabel(value.status)}</strong>.
              </p>
            ) : null}
          </section>

          <section className="profile-order-section">
            <div className="profile-section-heading">
              <div>
                <p className="eyebrow">Historical snapshot</p>
                <h2>Products</h2>
              </div>
              <span>Prices and names at purchase</span>
            </div>
            <div className="profile-order-items">
              {value.items.map((item, index) => (
                <article className="profile-order-item" key={`${item.productId}-${index}`}>
                  <div className="profile-order-item__heading">
                    <div>
                      <h3>{item.quantity} × {item.productName}</h3>
                      <p>{formatMoney(item.finalPrice, value.currency)} each</p>
                    </div>
                    <strong>{formatMoney(item.finalPrice * item.quantity, value.currency)}</strong>
                  </div>
                  {item.options.length ? (
                    <ul className="profile-order-options">
                      {item.options.map((option, optionIndex) => (
                        <li key={`${option.optionGroupName}-${optionIndex}`}>
                          <span>{option.optionGroupName}</span>
                          <strong>{option.optionValueName}</strong>
                          <span>
                            {option.priceModifier > 0
                              ? `+${formatMoney(option.priceModifier, value.currency)}`
                              : "Included"}
                          </span>
                        </li>
                      ))}
                    </ul>
                  ) : (
                    <p className="field-help">No selected options.</p>
                  )}
                  {item.comment ? <p><strong>Item comment:</strong> {item.comment}</p> : null}
                  <p className="snapshot-note">
                    Snapshot: base {formatMoney(item.basePrice, value.currency)}
                    {item.weightGrams ? ` · ${item.weightGrams} g` : ""}
                    {item.volumeMilliliters ? ` · ${item.volumeMilliliters} ml` : ""}
                    {item.calories ? ` · ${item.calories} kcal` : ""}
                  </p>
                </article>
              ))}
            </div>
          </section>
        </div>

        <aside className="profile-order-summary">
          <section className="profile-order-section">
            <p className="eyebrow">Order information</p>
            <dl className="profile-order-facts">
              <div><dt>Order number</dt><dd>{value.orderNumber}</dd></div>
              <div><dt>Created</dt><dd>{formatDate(value.createdAt)}</dd></div>
              <OptionalDate label="Confirmed" value={value.confirmedAt} />
              <OptionalDate label="Preparation started" value={value.preparationStartedAt} />
              <OptionalDate label="Ready" value={value.readyAt} />
              <OptionalDate label="Completed" value={value.completedAt} />
              <OptionalDate label="Estimated ready" value={value.estimatedReadyAt} />
              <div><dt>Pickup</dt><dd>{pickupLabel(value)}</dd></div>
              <div><dt>Payment method</dt><dd>{paymentMethodLabel(value.paymentMethod)}</dd></div>
              <div>
                <dt>Payment status</dt>
                <dd>
                  {value.paymentReceived
                    ? `Received${value.paymentMethodUsed ? ` by ${value.paymentMethodUsed.toLowerCase()}` : ""}`
                    : "Due at pickup"}
                </dd>
              </div>
              <OptionalDate label="Payment received" value={value.paymentReceivedAt} />
            </dl>
            {value.comment ? (
              <div className="profile-order-comment">
                <strong>Customer comment</strong>
                <p>{value.comment}</p>
              </div>
            ) : null}
            <dl className="profile-order-totals">
              <div><dt>Subtotal</dt><dd>{formatMoney(value.subtotal, value.currency)}</dd></div>
              <div><dt>Discount</dt><dd>{formatMoney(value.discountTotal, value.currency)}</dd></div>
              <div><dt>Total</dt><dd>{formatMoney(value.total, value.currency)}</dd></div>
            </dl>
          </section>

          {cancelMutation.error ? <ErrorState error={cancelMutation.error} /> : null}
          <div className="profile-order-actions">
            {value.status === "PendingConfirmation" ? (
              <button
                className="button button-danger"
                disabled={cancelMutation.isPending}
                onClick={() => cancelMutation.mutate()}
                type="button"
              >
                {cancelMutation.isPending ? "Cancelling…" : "Cancel order"}
              </button>
            ) : null}
            <RepeatOrderButton orderId={value.id} orderNumber={value.orderNumber} />
            <Link className="button button-link" to="/profile/orders">Back to my orders</Link>
            <Link className="button button-secondary button-link" to="/">Back to menu</Link>
          </div>
        </aside>
      </div>
    </section>
  );
}

function OptionalDate({ label, value }: { label: string; value?: string }) {
  return value ? <div><dt>{label}</dt><dd>{formatDate(value)}</dd></div> : null;
}

function timelineDate(order: OrderDetail, status: OrderStatus): string | undefined {
  switch (status) {
    case "PendingConfirmation": return order.createdAt;
    case "Confirmed": return order.confirmedAt;
    case "Preparing": return order.preparationStartedAt;
    case "ReadyForPickup": return order.readyAt;
    case "Completed": return order.completedAt;
    default: return undefined;
  }
}

function pickupLabel(order: OrderDetail): string {
  return order.pickupMode === "AsSoonAsPossible"
    ? "Prepare ASAP"
    : order.requestedPickupTime
      ? `Scheduled for ${formatDate(order.requestedPickupTime)}`
      : "Scheduled pickup";
}

function statusHeading(order: OrderDetail): string {
  switch (order.status) {
    case "Confirmed": return "Your order is confirmed.";
    case "Preparing": return "Your order is being prepared.";
    case "ReadyForPickup": return "Your order is ready for pickup.";
    case "Completed": return "Your order is complete.";
    case "Rejected": return "The cafe could not accept this order.";
    case "Cancelled": return "This order was cancelled.";
    default: return "Thanks — your order is in.";
  }
}

function statusMessage(order: OrderDetail): string {
  if (order.status === "Confirmed") {
    return order.estimatedReadyAt
      ? `The cafe expects your order to be ready at ${formatDate(order.estimatedReadyAt)}.`
      : "The cafe accepted your order.";
  }
  if (order.status === "Preparing") {
    return order.estimatedReadyAt
      ? `The kitchen is working on your order. Estimated ready time: ${formatDate(order.estimatedReadyAt)}.`
      : "The kitchen is working on your order now.";
  }
  if (order.status === "ReadyForPickup") {
    return order.paymentReceived
      ? "Your order is ready. Show your order number at pickup."
      : "Your order is ready. Please pay when you collect it.";
  }
  if (order.status === "Completed") return "This order has been collected and completed.";
  if (order.status === "Rejected") return "The cafe's reason is shown below.";
  if (order.status === "Cancelled") return "No further staff action will be taken for this order.";
  return "Your order is waiting for cafe confirmation.";
}
