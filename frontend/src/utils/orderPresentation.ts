import type { OrderStatus, PaymentMethod, PickupMode } from "../types/orders";

const statusLabels: Record<OrderStatus, string> = {
  PendingConfirmation: "Pending confirmation",
  Confirmed: "Confirmed",
  Cancelled: "Cancelled",
  Rejected: "Rejected",
};

export function orderStatusLabel(status: OrderStatus): string {
  return statusLabels[status];
}

export function paymentMethodLabel(method: PaymentMethod): string {
  return method === "PayOnPickup" ? "Pay on pickup" : "Online payment";
}

export function pickupModeLabel(mode: PickupMode): string {
  return mode === "AsSoonAsPossible" ? "Prepare ASAP" : "Scheduled pickup";
}
