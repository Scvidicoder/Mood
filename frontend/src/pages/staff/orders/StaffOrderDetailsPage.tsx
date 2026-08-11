import { useQuery } from "@tanstack/react-query";
import { Link, useParams } from "react-router-dom";
import { getStaffOrder } from "../../../api/orders";
import { ErrorState } from "../../../components/ErrorState";
import { LoadingState } from "../../../components/LoadingState";
import { formatDate, formatMoney } from "../../../utils/format";
import {
  orderStatusLabel,
  paymentMethodLabel,
  pickupModeLabel,
} from "../../../utils/orderPresentation";

export function StaffOrderDetailsPage() {
  const { id = "" } = useParams();
  const order = useQuery({
    queryKey: ["staff", "orders", "detail", id],
    queryFn: ({ signal }) => getStaffOrder(id, signal),
    enabled: Boolean(id),
    refetchInterval: 10_000,
  });

  if (order.isLoading) return <LoadingState message="Loading order details…" />;
  if (order.error || !order.data) return <ErrorState error={order.error} />;
  const value = order.data;

  return (
    <section>
      <Link className="staff-back-link" to="/staff/orders">← Back to orders</Link>
      <div className="staff-page-heading">
        <div><p className="eyebrow">Order details</p><h1>{value.orderNumber}</h1></div>
        <span className={`order-status order-status--${value.status.toLowerCase()}`}>{orderStatusLabel(value.status)}</span>
      </div>
      <div className="staff-order-detail-layout">
        <article className="panel">
          <h2>Customer and pickup</h2>
          <dl className="staff-order-fields">
            <div><dt>Customer</dt><dd>{value.customerName}</dd></div>
            <div><dt>Phone</dt><dd><a href={`tel:${value.customerPhoneNumber}`}>{value.customerPhoneNumber}</a></dd></div>
            <div><dt>Created</dt><dd>{formatDate(value.createdAt)}</dd></div>
            <div><dt>Pickup</dt><dd>{pickupModeLabel(value.pickupMode)}</dd></div>
            {value.requestedPickupTime ? <div><dt>Requested</dt><dd>{formatDate(value.requestedPickupTime)}</dd></div> : null}
            {value.estimatedReadyAt ? <div><dt>Estimated ready</dt><dd>{formatDate(value.estimatedReadyAt)}</dd></div> : null}
            <div><dt>Payment</dt><dd>{paymentMethodLabel(value.paymentMethod)}</dd></div>
          </dl>
          {value.comment ? <p className="staff-order-comment"><strong>Customer comment:</strong> {value.comment}</p> : null}
          {value.rejectReason ? <p className="order-reject-reason"><strong>Rejection reason:</strong> {value.rejectReason}</p> : null}
        </article>
        <article className="panel">
          <h2>Totals</h2>
          <dl className="staff-order-fields">
            <div><dt>Subtotal</dt><dd>{formatMoney(value.subtotal, value.currency)}</dd></div>
            <div><dt>Discount</dt><dd>{formatMoney(value.discountTotal, value.currency)}</dd></div>
            <div><dt>Total</dt><dd>{formatMoney(value.total, value.currency)}</dd></div>
          </dl>
        </article>
      </div>
      <article className="panel panel--spaced">
        <h2>Immutable item snapshots</h2>
        <div className="staff-order-items">
          {value.items.map((item, index) => (
            <section key={`${item.productId}-${index}`}>
              <div><h3>{item.quantity} × {item.productName}</h3><strong>{formatMoney(item.finalPrice * item.quantity, value.currency)}</strong></div>
              <p>Unit snapshot: {formatMoney(item.finalPrice, value.currency)} · Base {formatMoney(item.basePrice, value.currency)}</p>
              {item.options.length ? <ul>{item.options.map((option) => <li key={`${option.optionGroupName}-${option.optionValueName}`}>{option.optionGroupName}: {option.optionValueName} ({formatMoney(option.priceModifier, value.currency)})</li>)}</ul> : null}
              {item.comment ? <p><strong>Item comment:</strong> {item.comment}</p> : null}
            </section>
          ))}
        </div>
      </article>
    </section>
  );
}
