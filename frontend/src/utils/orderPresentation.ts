import type {
  OrderStatus,
  PaymentMethod,
  PaymentStatus,
  PickupMode,
} from "../types/orders";

const statusLabels: Record<OrderStatus, string> = {
  PendingConfirmation: "Pending confirmation",
  Confirmed: "Confirmed",
  Preparing: "Preparing",
  ReadyForPickup: "Ready for pickup",
  Completed: "Completed",
  Cancelled: "Cancelled",
  Rejected: "Rejected",
};

export function orderStatusLabel(status: OrderStatus): string {
  return statusLabels[status];
}

export function paymentMethodLabel(method: PaymentMethod): string {
  return method === "PayOnPickup" ? "Pay on pickup" : "Online payment";
}

const paymentStatusLabels: Record<PaymentStatus, string> = {
  Pending: "Pending",
  Paid: "Paid",
  Failed: "Failed",
  Cancelled: "Cancelled",
  RefundRequired: "Refund required",
  RefundPending: "Refund pending",
  Refunded: "Refunded",
  ReconciliationRequired: "Reconciliation required",
};

export function paymentStatusLabel(status: PaymentStatus): string {
  return paymentStatusLabels[status];
}

export function pickupModeLabel(mode: PickupMode): string {
  return mode === "AsSoonAsPossible" ? "Prepare ASAP" : "Scheduled pickup";
}
