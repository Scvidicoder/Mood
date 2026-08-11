import { useQuery } from "@tanstack/react-query";
import { useEffect, useRef } from "react";
import { Link, useLocation, useParams } from "react-router-dom";
import { getOrder } from "../api/orders";
import { ErrorState } from "../components/ErrorState";
import { LoadingState } from "../components/LoadingState";
import type { OrderDetail } from "../types/orders";
import { formatDate, formatMoney } from "../utils/format";

interface OrderSuccessLocationState {
  order?: OrderDetail;
}

export function OrderSuccessPage() {
  const { id = "" } = useParams();
  const location = useLocation();
  const headingRef = useRef<HTMLHeadingElement>(null);
  const initialOrder = (location.state as OrderSuccessLocationState | null)?.order;
  const order = useQuery({
    queryKey: ["orders", id],
    queryFn: ({ signal }) => getOrder(id, signal),
    enabled: Boolean(id),
    initialData: initialOrder?.id === id ? initialOrder : undefined,
  });

  useEffect(() => {
    const previousTitle = document.title;
    document.title = "Order created - Mood Pickup";
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
  return (
    <section className="order-success-page">
      <div className="order-success-card">
        <span aria-hidden="true" className="menu-feedback__mark">MP</span>
        <p className="eyebrow">Order created</p>
        <h1 ref={headingRef} tabIndex={-1}>Thanks — your order is in.</h1>
        <p>
          Your order is waiting for café confirmation. Keep the order number for
          your pickup.
        </p>
        <dl className="order-success-details">
          <div><dt>Order number</dt><dd>{value.orderNumber}</dd></div>
          <div><dt>Total</dt><dd>{formatMoney(value.total, value.currency)}</dd></div>
          <div><dt>Pickup</dt><dd>{pickupLabel(value)}</dd></div>
          <div><dt>Payment</dt><dd>{paymentLabel(value.paymentMethod)}</dd></div>
          <div><dt>Status</dt><dd>{statusLabel(value.status)}</dd></div>
        </dl>
        <Link className="button button-link" to="/">Back to menu</Link>
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

function paymentLabel(method: OrderDetail["paymentMethod"]): string {
  return method === "PayOnPickup" ? "Pay on pickup" : "Online payment";
}

function statusLabel(status: OrderDetail["status"]): string {
  return status === "PendingConfirmation" ? "Pending confirmation" : "Cancelled";
}
