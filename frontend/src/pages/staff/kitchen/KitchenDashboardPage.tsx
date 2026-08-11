import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useMemo, useState } from "react";
import { useAuth } from "../../../app/AuthProvider";
import {
  getKitchenOrders,
  markKitchenOrderReady,
  startKitchenOrder,
  updateKitchenOrderEta,
} from "../../../api/orders";
import { hasStaffCapability } from "../../../auth/permissions";
import { ErrorState } from "../../../components/ErrorState";
import { LoadingState } from "../../../components/LoadingState";
import { useToast } from "../../../components/ToastProvider";
import { useStaffConnectionState } from "../../../layouts/StaffLayout";
import type {
  KitchenOrder,
  KitchenOrderFilters,
} from "../../../types/orders";
import { formatDate, formatMoney } from "../../../utils/format";
import {
  orderStatusLabel,
  paymentMethodLabel,
  pickupModeLabel,
} from "../../../utils/orderPresentation";

interface FilterState {
  status: "" | "Confirmed" | "Preparing" | "ReadyForPickup";
  orderNumber: string;
  createdDate: string;
  pickupDate: string;
}

const emptyFilters: FilterState = {
  status: "",
  orderNumber: "",
  createdDate: "",
  pickupDate: "",
};

export function KitchenDashboardPage() {
  const { session } = useAuth();
  const { notify } = useToast();
  const queryClient = useQueryClient();
  const connectionState = useStaffConnectionState();
  const canWorkKitchen = hasStaffCapability(session, "workKitchen");
  const [filters, setFilters] = useState<FilterState>(emptyFilters);
  const [etaOrder, setEtaOrder] = useState<KitchenOrder | null>(null);
  const [estimatedReadyTime, setEstimatedReadyTime] = useState(readyTimeDefault);
  const now = useCurrentTime();
  const apiFilters = useMemo(() => toApiFilters(filters), [filters]);
  const orders = useQuery({
    queryKey: ["staff", "kitchen", apiFilters],
    queryFn: ({ signal }) => getKitchenOrders(apiFilters, 1, 100, signal),
    refetchInterval: connectionState === "Connected" ? false : 10_000,
  });

  const finishMutation = (message: string) => {
    notify(message);
    setEtaOrder(null);
    void queryClient.invalidateQueries({ queryKey: ["staff", "kitchen"] });
    void queryClient.invalidateQueries({ queryKey: ["staff", "orders"] });
  };
  const startMutation = useMutation({
    mutationFn: (order: KitchenOrder) =>
      startKitchenOrder(order.id, { rowVersion: order.rowVersion }),
    onSuccess: () => finishMutation("Preparation started."),
  });
  const readyMutation = useMutation({
    mutationFn: (order: KitchenOrder) =>
      markKitchenOrderReady(order.id, { rowVersion: order.rowVersion }),
    onSuccess: () => finishMutation("Order marked ready for pickup."),
  });
  const etaMutation = useMutation({
    mutationFn: ({ order, value }: { order: KitchenOrder; value: string }) =>
      updateKitchenOrderEta(order.id, {
        estimatedReadyTime: new Date(value).toISOString(),
        rowVersion: order.rowVersion,
      }),
    onSuccess: () => finishMutation("Estimated ready time updated."),
  });
  const mutationError =
    startMutation.error ?? readyMutation.error ?? etaMutation.error;

  function openEtaDialog(order: KitchenOrder) {
    etaMutation.reset();
    setEtaOrder(order);
    setEstimatedReadyTime(
      order.estimatedReadyAt
        ? toDateTimeLocal(new Date(order.estimatedReadyAt))
        : readyTimeDefault(),
    );
  }

  return (
    <section>
      <div className="staff-page-heading kitchen-heading">
        <div>
          <p className="eyebrow">Kitchen workflow</p>
          <h1>Kitchen dashboard</h1>
          <p>Confirmed, preparing, and ready orders update automatically.</p>
        </div>
        <span
          className={`connection-state connection-state--${connectionState.toLowerCase()}`}
        >
          Live updates: {connectionState}
        </span>
      </div>

      <div className="panel kitchen-filters">
        <label>
          Status
          <select
            value={filters.status}
            onChange={(event) =>
              setFilters((current) => ({
                ...current,
                status: event.target.value as FilterState["status"],
              }))
            }
          >
            <option value="">All active statuses</option>
            <option value="Confirmed">Confirmed</option>
            <option value="Preparing">Preparing</option>
            <option value="ReadyForPickup">Ready for pickup</option>
          </select>
        </label>
        <label>
          Order number
          <input
            maxLength={32}
            placeholder="MP-20260811"
            type="search"
            value={filters.orderNumber}
            onChange={(event) =>
              setFilters((current) => ({
                ...current,
                orderNumber: event.target.value,
              }))
            }
          />
        </label>
        <label>
          Created date
          <input
            type="date"
            value={filters.createdDate}
            onChange={(event) =>
              setFilters((current) => ({
                ...current,
                createdDate: event.target.value,
              }))
            }
          />
        </label>
        <label>
          Pickup date
          <input
            type="date"
            value={filters.pickupDate}
            onChange={(event) =>
              setFilters((current) => ({
                ...current,
                pickupDate: event.target.value,
              }))
            }
          />
        </label>
        <button
          className="button button-secondary"
          onClick={() => setFilters(emptyFilters)}
          type="button"
        >
          Clear filters
        </button>
      </div>

      {orders.isLoading ? <LoadingState message="Loading kitchen orders…" /> : null}
      {orders.error ? <ErrorState error={orders.error} /> : null}
      {mutationError ? <ErrorState error={mutationError} /> : null}
      {orders.data?.items.length === 0 ? (
        <div className="panel panel--spaced">
          <p className="empty-copy">No active kitchen orders match these filters.</p>
        </div>
      ) : null}
      {orders.data?.items.length ? (
        <div className="kitchen-order-grid">
          {orders.data.items.map((order) => (
            <KitchenCard
              canWorkKitchen={canWorkKitchen}
              key={order.id}
              now={now}
              onEditEta={openEtaDialog}
              onReady={(value) => readyMutation.mutate(value)}
              onStart={(value) => startMutation.mutate(value)}
              order={order}
              pending={startMutation.isPending || readyMutation.isPending}
            />
          ))}
        </div>
      ) : null}

      {etaOrder ? (
        <div
          aria-labelledby="kitchen-eta-title"
          aria-modal="true"
          className="modal-backdrop"
          role="dialog"
        >
          <form
            className="staff-order-dialog"
            onSubmit={(event) => {
              event.preventDefault();
              etaMutation.mutate({ order: etaOrder, value: estimatedReadyTime });
            }}
          >
            <p className="eyebrow">{etaOrder.orderNumber}</p>
            <h2 id="kitchen-eta-title">Edit estimated ready time</h2>
            <label>
              Estimated ready time
              <input
                autoFocus
                required
                type="datetime-local"
                value={estimatedReadyTime}
                onChange={(event) => setEstimatedReadyTime(event.target.value)}
              />
            </label>
            {etaMutation.error ? <ErrorState error={etaMutation.error} /> : null}
            <div className="staff-order-actions">
              <button
                className="button button-secondary"
                disabled={etaMutation.isPending}
                onClick={() => setEtaOrder(null)}
                type="button"
              >
                Cancel
              </button>
              <button className="button" disabled={etaMutation.isPending} type="submit">
                {etaMutation.isPending ? "Saving…" : "Save ETA"}
              </button>
            </div>
          </form>
        </div>
      ) : null}
    </section>
  );
}

function KitchenCard({
  order,
  now,
  canWorkKitchen,
  pending,
  onStart,
  onReady,
  onEditEta,
}: {
  order: KitchenOrder;
  now: number;
  canWorkKitchen: boolean;
  pending: boolean;
  onStart: (order: KitchenOrder) => void;
  onReady: (order: KitchenOrder) => void;
  onEditEta: (order: KitchenOrder) => void;
}) {
  return (
    <article className={`kitchen-order-card kitchen-order-card--${order.status.toLowerCase()}`}>
      <div className="order-card-heading">
        <div>
          <p className="eyebrow">{formatDate(order.createdAt)}</p>
          <h2>{order.orderNumber}</h2>
        </div>
        <span className={`order-status order-status--${order.status.toLowerCase()}`}>
          {orderStatusLabel(order.status)}
        </span>
      </div>
      <dl className="staff-order-fields kitchen-order-meta">
        <div><dt>Customer</dt><dd>{order.customerName}</dd></div>
        <div><dt>Pickup</dt><dd>{pickupModeLabel(order.pickupMode)}</dd></div>
        {order.requestedPickupTime ? (
          <div><dt>Requested pickup</dt><dd>{formatDate(order.requestedPickupTime)}</dd></div>
        ) : null}
        {order.estimatedReadyAt ? (
          <div><dt>Estimated ready</dt><dd>{formatDate(order.estimatedReadyAt)}</dd></div>
        ) : null}
        <div><dt>Payment</dt><dd>{paymentMethodLabel(order.paymentMethod)}</dd></div>
        <div>
          <dt>Payment status</dt>
          <dd>{order.paymentReceived ? "Received" : "Due at pickup"}</dd>
        </div>
        {order.paymentMethodUsed ? (
          <div><dt>Method used</dt><dd>{order.paymentMethodUsed}</dd></div>
        ) : null}
        <div><dt>Total</dt><dd>{formatMoney(order.total, order.currency)}</dd></div>
        <div>
          <dt>Preparation elapsed</dt>
          <dd>{elapsedPreparation(order, now)}</dd>
        </div>
      </dl>
      <div className="kitchen-item-list">
        {order.items.map((item, index) => (
          <section key={`${item.productId}-${index}`}>
            <h3>{item.quantity} × {item.productName}</h3>
            {item.options.length ? (
              <ul>
                {item.options.map((option) => (
                  <li key={`${option.optionGroupName}-${option.optionValueName}`}>
                    {option.optionGroupName}: {option.optionValueName}
                  </li>
                ))}
              </ul>
            ) : <p>No selected options</p>}
            {item.comment ? <p><strong>Item note:</strong> {item.comment}</p> : null}
          </section>
        ))}
      </div>
      {order.comment ? (
        <p className="staff-order-comment"><strong>Customer comment:</strong> {order.comment}</p>
      ) : null}
      <p className={`kitchen-urgency kitchen-urgency--${urgency(order, now).kind}`}>
        {urgency(order, now).label}
      </p>
      {canWorkKitchen ? (
        <div className="staff-order-actions kitchen-actions">
          {order.status === "Confirmed" ? (
            <button className="button" disabled={pending} onClick={() => onStart(order)} type="button">
              Start preparation
            </button>
          ) : null}
          {order.status === "Preparing" ? (
            <button className="button" disabled={pending} onClick={() => onReady(order)} type="button">
              Mark ready
            </button>
          ) : null}
          {order.status !== "ReadyForPickup" ? (
            <button className="button button-secondary" disabled={pending} onClick={() => onEditEta(order)} type="button">
              Edit ETA
            </button>
          ) : null}
        </div>
      ) : (
        <p className="kitchen-view-only">View only — kitchen actions require the Kitchen role.</p>
      )}
    </article>
  );
}

function useCurrentTime(): number {
  const [now, setNow] = useState(Date.now());
  useEffect(() => {
    const interval = window.setInterval(() => setNow(Date.now()), 30_000);
    return () => window.clearInterval(interval);
  }, []);
  return now;
}

function elapsedPreparation(order: KitchenOrder, now: number): string {
  if (!order.preparationStartedAt) return "Not started";
  const end = order.readyAt ? new Date(order.readyAt).getTime() : now;
  const minutes = Math.max(
    0,
    Math.floor((end - new Date(order.preparationStartedAt).getTime()) / 60_000),
  );
  if (minutes < 60) return `${minutes} min`;
  return `${Math.floor(minutes / 60)} hr ${minutes % 60} min`;
}

function urgency(order: KitchenOrder, now: number) {
  if (order.status === "ReadyForPickup") {
    return { kind: "ready", label: "Ready for pickup" };
  }
  const target = order.requestedPickupTime ?? order.estimatedReadyAt;
  if (!target) return { kind: "normal", label: "No pickup target" };
  const minutes = Math.ceil((new Date(target).getTime() - now) / 60_000);
  if (minutes <= 0) return { kind: "delayed", label: `Delayed by ${Math.abs(minutes)} min` };
  if (minutes < 10) return { kind: "soon", label: `${minutes} min remaining` };
  return { kind: "normal", label: `${minutes} min remaining` };
}

function toApiFilters(filters: FilterState): KitchenOrderFilters {
  const created = dateBounds(filters.createdDate);
  const pickup = dateBounds(filters.pickupDate);
  return {
    status: filters.status || undefined,
    orderNumber: filters.orderNumber.trim() || undefined,
    createdFrom: created?.from,
    createdTo: created?.to,
    pickupFrom: pickup?.from,
    pickupTo: pickup?.to,
  };
}

function dateBounds(value: string): { from: string; to: string } | undefined {
  if (!value) return undefined;
  const from = new Date(`${value}T00:00:00`);
  const to = new Date(from);
  to.setDate(to.getDate() + 1);
  return { from: from.toISOString(), to: to.toISOString() };
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
