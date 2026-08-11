import type { PagedResponse } from "./menu";

export type PaymentMethod = "PayOnPickup" | "Online";

export type PickupMode = "AsSoonAsPossible" | "Scheduled";

export type OrderStatus =
  | "PendingConfirmation"
  | "Confirmed"
  | "Cancelled"
  | "Rejected";

export interface CreateOrderItemInput {
  productId: string;
  optionValueIds: string[];
  quantity: number;
  comment: string | null;
}

export interface CreateOrderInput {
  items: CreateOrderItemInput[];
  comment: string | null;
  paymentMethod: PaymentMethod;
  pickupMode: PickupMode;
  requestedPickupTime: string | null;
}

export interface OrderItemOption {
  optionGroupName: string;
  optionValueName: string;
  priceModifier: number;
  caloriesModifier?: number;
  volumeModifier?: number;
  displayOrder: number;
}

export interface OrderItem {
  productId: string;
  productName: string;
  isAvailableAtPurchase: boolean;
  basePrice: number;
  finalPrice: number;
  calories?: number;
  volumeMilliliters?: number;
  weightGrams?: number;
  quantity: number;
  comment?: string;
  options: OrderItemOption[];
}

export interface OrderDetail {
  id: string;
  orderNumber: string;
  status: OrderStatus;
  paymentMethod: PaymentMethod;
  pickupMode: PickupMode;
  requestedPickupTime?: string;
  comment?: string;
  subtotal: number;
  discountTotal: number;
  total: number;
  currency: string;
  createdAt: string;
  estimatedReadyAt?: string;
  rejectReason?: string;
  items: OrderItem[];
}

export interface OrderSummary {
  id: string;
  orderNumber: string;
  status: OrderStatus;
  paymentMethod: PaymentMethod;
  pickupMode: PickupMode;
  requestedPickupTime?: string;
  total: number;
  currency: string;
  itemQuantity: number;
  createdAt: string;
  estimatedReadyAt?: string;
  rejectReason?: string;
}

export type CustomerOrdersPage = PagedResponse<OrderSummary>;

export interface StaffOrderSummary {
  id: string;
  orderNumber: string;
  customerName: string;
  customerPhoneNumber: string;
  createdAt: string;
  pickupMode: PickupMode;
  requestedPickupTime?: string;
  paymentMethod: PaymentMethod;
  total: number;
  currency: string;
  comment?: string;
  status: OrderStatus;
  estimatedReadyAt?: string;
  itemQuantity: number;
  rowVersion: string;
}

export interface StaffOrderDetail extends StaffOrderSummary {
  rejectReason?: string;
  subtotal: number;
  discountTotal: number;
  confirmedAt?: string;
  rejectedAt?: string;
  items: OrderItem[];
}

export type StaffOrdersPage = PagedResponse<StaffOrderSummary>;

export interface ConfirmOrderInput {
  estimatedReadyTime: string;
  rowVersion: string;
}

export interface RejectOrderInput {
  reason: string;
  rowVersion: string;
}

export interface UpdateEstimatedReadyTimeInput {
  estimatedReadyTime: string;
  rowVersion: string;
}

export interface OrderRealtimeEvent {
  eventId: string;
  timestamp: string;
  entityId: string;
  orderNumber: string;
  status: OrderStatus;
  estimatedReadyAt?: string;
  rejectReason?: string;
}
