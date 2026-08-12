import { useQuery } from "@tanstack/react-query";
import { useState, type FormEvent } from "react";
import { Link } from "react-router-dom";
import { getMyOrders } from "../api/orders";
import { ErrorState } from "../components/ErrorState";
import { LoadingState } from "../components/LoadingState";
import { RepeatOrderButton } from "../components/RepeatOrderButton";
import { useOrderNotifications } from "../hooks/useOrderNotifications";
import type { CustomerOrderFilter } from "../types/orders";
import { formatDate, formatMoney } from "../utils/format";
import {
  orderStatusLabel,
  paymentStatusLabel,
  pickupModeLabel,
} from "../utils/orderPresentation";

const filters: CustomerOrderFilter[] = [
  "All",
  "Active",
  "Completed",
  "Cancelled",
  "Rejected",
];

export function MyOrdersPage() {
  const connectionState = useOrderNotifications();
  const [filter, setFilter] = useState<CustomerOrderFilter>("All");
  const [searchDraft, setSearchDraft] = useState("");
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const orders = useQuery({
    queryKey: ["orders", "mine", { page, filter, search }],
    queryFn: ({ signal }) =>
      getMyOrders(page, 12, signal, filter, search || undefined),
    refetchInterval: connectionState === "Connected" ? false : 15_000,
  });

  function submitSearch(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setPage(1);
    setSearch(searchDraft.trim());
  }

  const isSearchEmpty = Boolean(search) && orders.data?.items.length === 0;
  const emptyMessage = isSearchEmpty
    ? "Nothing matches your search"
    : filter === "Active"
      ? "Your active orders will appear here"
      : "No orders yet";

  return (
    <section className="page customer-orders-page">
      <div className="page-heading orders-heading">
        <div>
          <p className="eyebrow">Personal cabinet</p>
          <h1>My orders</h1>
          <p>Search, review, track, and repeat your Mood Pickup orders.</p>
        </div>
        <span className={`connection-state connection-state--${connectionState.toLowerCase()}`}>
          Live updates: {connectionState}
        </span>
      </div>

      <div className="customer-order-toolbar">
        <div aria-label="Order status filters" className="customer-order-filters" role="group">
          {filters.map((value) => (
            <button
              aria-pressed={filter === value}
              className={filter === value ? "is-active" : ""}
              key={value}
              onClick={() => {
                setFilter(value);
                setPage(1);
              }}
              type="button"
            >
              {value}
            </button>
          ))}
        </div>
        <form className="customer-order-search" onSubmit={submitSearch}>
          <label className="sr-only" htmlFor="customer-order-search">Search orders</label>
          <input
            id="customer-order-search"
            maxLength={120}
            onChange={(event) => setSearchDraft(event.target.value)}
            placeholder="Order number or product"
            type="search"
            value={searchDraft}
          />
          <button className="button" type="submit">Search</button>
        </form>
      </div>

      {orders.isLoading ? <LoadingState message="Loading your orders…" /> : null}
      {orders.error ? <ErrorState error={orders.error} /> : null}
      {orders.data?.items.length === 0 ? (
        <div className="placeholder-card customer-orders-empty">
          <h2>{emptyMessage}</h2>
          <p>
            {isSearchEmpty
              ? "Try another order number or product name."
              : "When you place an order, its progress will be available here."}
          </p>
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
                {order.onlinePaymentStatus ? (
                  <div><dt>Payment</dt><dd>{paymentStatusLabel(order.onlinePaymentStatus)}</dd></div>
                ) : null}
                {order.estimatedReadyAt ? (
                  <div><dt>Estimated ready</dt><dd>{formatDate(order.estimatedReadyAt)}</dd></div>
                ) : null}
              </dl>
              {order.rejectReason ? (
                <p className="order-reject-reason"><strong>Reason:</strong> {order.rejectReason}</p>
              ) : null}
              <div className="customer-order-card__actions">
                <Link className="button button-link" to={`/profile/orders/${order.id}`}>
                  View details
                </Link>
                <RepeatOrderButton orderId={order.id} orderNumber={order.orderNumber} />
              </div>
            </article>
          ))}
        </div>
      ) : null}

      {orders.data && orders.data.totalPages > 1 ? (
        <nav aria-label="Order history pages" className="customer-order-pagination">
          <button
            className="button button-secondary"
            disabled={page <= 1}
            onClick={() => setPage((current) => Math.max(1, current - 1))}
            type="button"
          >
            Previous
          </button>
          <span>Page {orders.data.page} of {orders.data.totalPages}</span>
          <button
            className="button button-secondary"
            disabled={page >= orders.data.totalPages}
            onClick={() => setPage((current) => current + 1)}
            type="button"
          >
            Next
          </button>
        </nav>
      ) : null}
    </section>
  );
}
