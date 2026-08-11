import { useQuery } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { getMyOrders } from "../api/orders";
import { ErrorState } from "../components/ErrorState";
import { LoadingState } from "../components/LoadingState";
import { useOrderNotifications } from "../hooks/useOrderNotifications";
import { formatDate, formatMoney } from "../utils/format";
import {
  orderStatusLabel,
  pickupModeLabel,
} from "../utils/orderPresentation";

export function MyOrdersPage() {
  const connectionState = useOrderNotifications();
  const orders = useQuery({
    queryKey: ["orders", "mine", 1],
    queryFn: ({ signal }) => getMyOrders(1, 100, signal),
    refetchInterval: connectionState === "Connected" ? false : 15_000,
  });

  return (
    <section className="page customer-orders-page">
      <div className="page-heading orders-heading">
        <div>
          <p className="eyebrow">Customer orders</p>
          <h1>My orders</h1>
          <p>Track confirmation, preparation, pickup readiness, and completion here.</p>
        </div>
        <span className={`connection-state connection-state--${connectionState.toLowerCase()}`}>
          Live updates: {connectionState}
        </span>
      </div>

      {orders.isLoading ? <LoadingState message="Loading your orders…" /> : null}
      {orders.error ? <ErrorState error={orders.error} /> : null}
      {orders.data?.items.length === 0 ? (
        <div className="placeholder-card">
          <h2>No orders yet</h2>
          <p>Your placed orders will appear here.</p>
          <Link className="button button-link" to="/">Browse the menu</Link>
        </div>
      ) : null}
      {orders.data?.items.length ? (
        <div className="customer-order-grid">
          {orders.data.items.map((order) => (
            <article className="customer-order-card" key={order.id}>
              <div className="order-card-heading">
                <div>
                  <p className="eyebrow">{formatDate(order.createdAt)}</p>
                  <h2>{order.orderNumber}</h2>
                </div>
                <span className={`order-status order-status--${order.status.toLowerCase()}`}>
                  {orderStatusLabel(order.status)}
                </span>
              </div>
              <dl className="compact-order-details">
                <div><dt>Total</dt><dd>{formatMoney(order.total, order.currency)}</dd></div>
                <div><dt>Pickup</dt><dd>{pickupModeLabel(order.pickupMode)}</dd></div>
                <div><dt>Items</dt><dd>{order.itemQuantity}</dd></div>
                {order.estimatedReadyAt ? (
                  <div><dt>Estimated ready</dt><dd>{formatDate(order.estimatedReadyAt)}</dd></div>
                ) : null}
              </dl>
              {order.rejectReason ? (
                <p className="order-reject-reason"><strong>Reason:</strong> {order.rejectReason}</p>
              ) : null}
              <Link className="button button-secondary button-link" to={`/order-success/${order.id}`}>
                View order
              </Link>
            </article>
          ))}
        </div>
      ) : null}
    </section>
  );
}
