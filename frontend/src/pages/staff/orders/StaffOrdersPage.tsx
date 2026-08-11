import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { Link } from "react-router-dom";
import { useAuth } from "../../../app/AuthProvider";
import {
  completeOrder,
  confirmOrder,
  getStaffOrders,
  recordOrderPayment,
  rejectOrder,
  updateEstimatedReadyTime,
} from "../../../api/orders";
import { hasStaffCapability } from "../../../auth/permissions";
import { ErrorState } from "../../../components/ErrorState";
import { LoadingState } from "../../../components/LoadingState";
import { useToast } from "../../../components/ToastProvider";
import { useStaffConnectionState } from "../../../layouts/StaffLayout";
import type {
  OrderStatus,
  PaymentMethodUsed,
  StaffOrderSummary,
} from "../../../types/orders";
import { formatDate, formatMoney } from "../../../utils/format";
import {
  orderStatusLabel,
  paymentMethodLabel,
  pickupModeLabel,
} from "../../../utils/orderPresentation";

type DialogState =
  | {
      type: "confirm" | "reject" | "time" | "payment" | "complete";
      order: StaffOrderSummary;
    }
  | null;

export function StaffOrdersPage() {
  const queryClient = useQueryClient();
  const { session } = useAuth();
  const { notify } = useToast();
  const connectionState = useStaffConnectionState();
  const canCompleteOrders = hasStaffCapability(session, "completeOrders");
  const [status, setStatus] = useState<OrderStatus | "All">("PendingConfirmation");
  const [dialog, setDialog] = useState<DialogState>(null);
  const [estimatedReadyTime, setEstimatedReadyTime] = useState(readyTimeDefault);
  const [reason, setReason] = useState("");
  const [paymentMethodUsed, setPaymentMethodUsed] =
    useState<PaymentMethodUsed>("Cash");
  const orders = useQuery({
    queryKey: ["staff", "orders", status],
    queryFn: ({ signal }) =>
      getStaffOrders(status === "All" ? undefined : status, 1, 100, signal),
    refetchInterval: connectionState === "Connected" ? false : 10_000,
  });

  const finishMutation = (message: string) => {
    setDialog(null);
    setReason("");
    notify(message);
    void queryClient.invalidateQueries({ queryKey: ["staff", "orders"] });
    void queryClient.invalidateQueries({ queryKey: ["staff", "kitchen"] });
  };
  const confirmMutation = useMutation({
    mutationFn: ({ order, value }: { order: StaffOrderSummary; value: string }) =>
      confirmOrder(order.id, {
        estimatedReadyTime: new Date(value).toISOString(),
        rowVersion: order.rowVersion,
      }),
    onSuccess: () => finishMutation("Order confirmed."),
  });
  const rejectMutation = useMutation({
    mutationFn: ({ order, value }: { order: StaffOrderSummary; value: string }) =>
      rejectOrder(order.id, { reason: value.trim(), rowVersion: order.rowVersion }),
    onSuccess: () => finishMutation("Order rejected."),
  });
  const timeMutation = useMutation({
    mutationFn: ({ order, value }: { order: StaffOrderSummary; value: string }) =>
      updateEstimatedReadyTime(order.id, {
        estimatedReadyTime: new Date(value).toISOString(),
        rowVersion: order.rowVersion,
      }),
    onSuccess: () => finishMutation("Estimated ready time updated."),
  });
  const paymentMutation = useMutation({
    mutationFn: ({ order, method }: { order: StaffOrderSummary; method: PaymentMethodUsed }) =>
      recordOrderPayment(order.id, {
        paymentMethodUsed: method,
        rowVersion: order.rowVersion,
      }),
    onSuccess: () => finishMutation("Payment received."),
  });
  const completeMutation = useMutation({
    mutationFn: (order: StaffOrderSummary) =>
      completeOrder(order.id, { rowVersion: order.rowVersion }),
    onSuccess: () => finishMutation("Order completed."),
  });
  const activeMutation =
    dialog?.type === "confirm"
      ? confirmMutation
      : dialog?.type === "reject"
        ? rejectMutation
        : dialog?.type === "time"
          ? timeMutation
          : dialog?.type === "payment"
            ? paymentMutation
            : completeMutation;

  function openDialog(
    type: NonNullable<DialogState>["type"],
    order: StaffOrderSummary,
  ) {
    confirmMutation.reset();
    rejectMutation.reset();
    timeMutation.reset();
    paymentMutation.reset();
    completeMutation.reset();
    setEstimatedReadyTime(
      order.estimatedReadyAt
        ? toDateTimeLocal(new Date(order.estimatedReadyAt))
        : readyTimeDefault(),
    );
    setReason("");
    setPaymentMethodUsed("Cash");
    setDialog({ type, order });
  }

  function submitDialog(event: React.FormEvent) {
    event.preventDefault();
    if (!dialog) return;
    if (dialog.type === "reject") {
      rejectMutation.mutate({ order: dialog.order, value: reason });
    } else if (dialog.type === "payment") {
      paymentMutation.mutate({ order: dialog.order, method: paymentMethodUsed });
    } else if (dialog.type === "complete") {
      completeMutation.mutate(dialog.order);
    } else if (dialog.type === "confirm") {
      confirmMutation.mutate({ order: dialog.order, value: estimatedReadyTime });
    } else {
      timeMutation.mutate({ order: dialog.order, value: estimatedReadyTime });
    }
  }

  return (
    <section>
      <div className="staff-page-heading">
        <div>
          <p className="eyebrow">Order operations</p>
          <h1>Staff orders</h1>
          <p>Review orders, payment state, and customer handoff.</p>
        </div>
        <label className="staff-order-filter">
          Status
          <select
            value={status}
            onChange={(event) => setStatus(event.target.value as OrderStatus | "All")}
          >
            <option value="PendingConfirmation">Pending confirmation</option>
            <option value="Confirmed">Confirmed</option>
            <option value="Preparing">Preparing</option>
            <option value="ReadyForPickup">Ready for pickup</option>
            <option value="Completed">Completed</option>
            <option value="Rejected">Rejected</option>
            <option value="Cancelled">Cancelled</option>
            <option value="All">All orders</option>
          </select>
        </label>
      </div>

      {orders.isLoading ? <LoadingState message="Loading staff orders…" /> : null}
      {orders.error ? <ErrorState error={orders.error} /> : null}
      {orders.data?.items.length === 0 ? (
        <div className="panel"><p className="empty-copy">No orders match this status.</p></div>
      ) : null}
      {orders.data?.items.length ? (
        <div className="staff-order-grid">
          {orders.data.items.map((order) => (
            <article className="staff-order-card" key={order.id}>
              <div className="order-card-heading">
                <div>
                  <p className="eyebrow">{formatDate(order.createdAt)}</p>
                  <h2>{order.orderNumber}</h2>
                </div>
                <span className={`order-status order-status--${order.status.toLowerCase()}`}>
                  {orderStatusLabel(order.status)}
                </span>
              </div>
              <dl className="staff-order-fields">
                <div><dt>Customer</dt><dd>{order.customerName}</dd></div>
                <div><dt>Phone</dt><dd><a href={`tel:${order.customerPhoneNumber}`}>{order.customerPhoneNumber}</a></dd></div>
                <div><dt>Pickup</dt><dd>{pickupModeLabel(order.pickupMode)}</dd></div>
                {order.requestedPickupTime ? <div><dt>Requested</dt><dd>{formatDate(order.requestedPickupTime)}</dd></div> : null}
                <div><dt>Payment</dt><dd>{paymentMethodLabel(order.paymentMethod)}</dd></div>
                <div><dt>Payment received</dt><dd>{order.paymentReceived ? "Yes" : "No"}</dd></div>
                <div><dt>Items</dt><dd>{order.itemQuantity}</dd></div>
                <div><dt>Total</dt><dd>{formatMoney(order.total, order.currency)}</dd></div>
                {order.estimatedReadyAt ? <div><dt>Estimated ready</dt><dd>{formatDate(order.estimatedReadyAt)}</dd></div> : null}
              </dl>
              {order.comment ? <p className="staff-order-comment"><strong>Comment:</strong> {order.comment}</p> : null}
              <div className="staff-order-actions">
                <Link className="button button-secondary button-link" to={`/staff/orders/${order.id}`}>Details</Link>
                {order.status === "PendingConfirmation" ? (
                  <>
                    <button className="button" onClick={() => openDialog("confirm", order)} type="button">Confirm</button>
                    <button className="button button-danger" onClick={() => openDialog("reject", order)} type="button">Reject</button>
                  </>
                ) : null}
                {order.status === "Confirmed" ? (
                  <button className="button" onClick={() => openDialog("time", order)} type="button">Change estimated time</button>
                ) : null}
                {canCompleteOrders && order.status === "ReadyForPickup" ? (
                  <>
                    {order.paymentMethod === "PayOnPickup" && !order.paymentReceived ? (
                      <button className="button" onClick={() => openDialog("payment", order)} type="button">Record payment</button>
                    ) : null}
                    <button
                      className="button"
                      disabled={order.paymentMethod === "PayOnPickup" && !order.paymentReceived}
                      onClick={() => openDialog("complete", order)}
                      type="button"
                    >
                      Complete order
                    </button>
                  </>
                ) : null}
              </div>
            </article>
          ))}
        </div>
      ) : null}

      {dialog ? (
        <div aria-labelledby="staff-order-dialog-title" aria-modal="true" className="modal-backdrop" role="dialog">
          <form className="staff-order-dialog" onSubmit={submitDialog}>
            <p className="eyebrow">{dialog.order.orderNumber}</p>
            <h2 id="staff-order-dialog-title">{dialogTitle(dialog.type)}</h2>
            {dialog.type === "reject" ? (
              <label>Reason<textarea autoFocus maxLength={500} required rows={4} value={reason} onChange={(event) => setReason(event.target.value)} /></label>
            ) : dialog.type === "confirm" || dialog.type === "time" ? (
              <label>Estimated ready time<input autoFocus required type="datetime-local" value={estimatedReadyTime} onChange={(event) => setEstimatedReadyTime(event.target.value)} /></label>
            ) : dialog.type === "payment" ? (
              <label>
                Payment method used
                <select value={paymentMethodUsed} onChange={(event) => setPaymentMethodUsed(event.target.value as PaymentMethodUsed)}>
                  <option value="Cash">Cash</option>
                  <option value="Card">Card</option>
                </select>
              </label>
            ) : (
              <p>Confirm that the order has been handed to the customer.</p>
            )}
            {activeMutation.error ? <ErrorState error={activeMutation.error} /> : null}
            <div className="staff-order-actions">
              <button className="button button-secondary" disabled={activeMutation.isPending} onClick={() => setDialog(null)} type="button">Cancel</button>
              <button className={`button ${dialog.type === "reject" ? "button-danger" : ""}`} disabled={activeMutation.isPending} type="submit">
                {activeMutation.isPending ? "Saving…" : dialogAction(dialog.type)}
              </button>
            </div>
          </form>
        </div>
      ) : null}
    </section>
  );
}

function dialogTitle(type: NonNullable<DialogState>["type"]): string {
  const labels = {
    confirm: "Confirm order",
    reject: "Reject order",
    time: "Change estimated ready time",
    payment: "Record payment",
    complete: "Complete order",
  };
  return labels[type];
}

function dialogAction(type: NonNullable<DialogState>["type"]): string {
  const labels = {
    confirm: "Confirm order",
    reject: "Reject order",
    time: "Save time",
    payment: "Record payment",
    complete: "Complete order",
  };
  return labels[type];
}

function readyTimeDefault(): string {
  const date = new Date(Date.now() + 30 * 60 * 1000);
  date.setMinutes(Math.ceil(date.getMinutes() / 5) * 5, 0, 0);
  return toDateTimeLocal(date);
}

function toDateTimeLocal(date: Date): string {
  const offset = date.getTimezoneOffset() * 60_000;
  return new Date(date.getTime() - offset).toISOString().slice(0, 16);
}
