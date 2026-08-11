import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useRef } from "react";
import { Link, useLocation, useParams } from "react-router-dom";
import { cancelOrder, getOrder } from "../api/orders";
import { ErrorState } from "../components/ErrorState";
import { LoadingState } from "../components/LoadingState";
import { useOrderNotifications } from "../hooks/useOrderNotifications";
import type { OrderDetail } from "../types/orders";
import { formatDate, formatMoney } from "../utils/format";
import {
  orderStatusLabel,
  paymentMethodLabel,
} from "../utils/orderPresentation";

interface OrderSuccessLocationState {
  order?: OrderDetail;
}

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
  const workflowSteps = ["Confirmed", "Preparing", "ReadyForPickup", "Completed"] as const;
  const currentStep = workflowSteps.indexOf(
    value.status as (typeof workflowSteps)[number],
  );
  return (
    <section className="order-success-page">
      <div className="order-success-card">
        <span aria-hidden="true" className="menu-feedback__mark">MP</span>
        <p className="eyebrow">Order tracking · {connectionState}</p>
        <h1 ref={headingRef} tabIndex={-1}>{statusHeading(value)}</h1>
        <p>{statusMessage(value)}</p>
        {!(["Rejected", "Cancelled"] as string[]).includes(value.status) ? (
          <ol className="order-progress" aria-label="Order progress">
            {workflowSteps.map((status, index) => (
              <li
                className={index <= currentStep ? "order-progress__step--complete" : ""}
                key={status}
              >
                <span aria-hidden="true">{index + 1}</span>
                {orderStatusLabel(status)}
              </li>
            ))}
          </ol>
        ) : null}
        <dl className="order-success-details">
          <div><dt>Order number</dt><dd>{value.orderNumber}</dd></div>
          <div><dt>Total</dt><dd>{formatMoney(value.total, value.currency)}</dd></div>
          <div><dt>Pickup</dt><dd>{pickupLabel(value)}</dd></div>
          <div><dt>Payment</dt><dd>{paymentMethodLabel(value.paymentMethod)}</dd></div>
          <div><dt>Payment status</dt><dd>{value.paymentReceived ? "Received" : "Due at pickup"}</dd></div>
          <div><dt>Status</dt><dd>{orderStatusLabel(value.status)}</dd></div>
          {value.estimatedReadyAt ? (
            <div><dt>Estimated ready</dt><dd>{formatDate(value.estimatedReadyAt)}</dd></div>
          ) : null}
        </dl>
        {value.rejectReason ? (
          <p className="order-reject-reason"><strong>Reason:</strong> {value.rejectReason}</p>
        ) : null}
        <div className="customer-status-history">
          <h2>Status history</h2>
          <ol className="order-status-history">
            {value.statusHistory.map((history, index) => (
              <li key={`${history.timestamp}-${index}`}>
                <strong>{orderStatusLabel(history.newStatus)}</strong>
                <span>{formatDate(history.timestamp)}</span>
                {history.reason ? <p>{history.reason}</p> : null}
              </li>
            ))}
          </ol>
        </div>
        {cancelMutation.error ? <ErrorState error={cancelMutation.error} /> : null}
        <div className="order-success-actions">
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
          <Link className="button button-secondary button-link" to="/orders">My orders</Link>
          <Link className="button button-link" to="/">Back to menu</Link>
        </div>
      </div>
    </section>
  );
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
    case "Confirmed":
      return "Your order is confirmed.";
    case "Preparing":
      return "Your order is being prepared.";
    case "ReadyForPickup":
      return "Your order is ready for pickup.";
    case "Completed":
      return "Your order is complete.";
    case "Rejected":
      return "The cafe could not accept this order.";
    case "Cancelled":
      return "This order was cancelled.";
    default:
      return "Thanks — your order is in.";
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
  if (order.status === "Completed") {
    return "This order has been collected and completed.";
  }
  if (order.status === "Rejected") {
    return "Review the cafe's reason below. No employee information is shared.";
  }
  if (order.status === "Cancelled") {
    return "No further staff action will be taken for this order.";
  }
  return "Your order is waiting for cafe confirmation. Keep the order number for pickup.";
}
