import type { PagedResponse } from "./menu";

export type PaymentMethod = "PayOnPickup" | "Online";

export type PaymentMethodUsed = "Cash" | "Card";

export type PickupMode = "AsSoonAsPossible" | "Scheduled";

export type OrderStatus =
  | "PendingConfirmation"
  | "Confirmed"
  | "Preparing"
  | "ReadyForPickup"
  | "Completed"
  | "Cancelled"
  | "Rejected";

export interface OrderStatusHistory {
  oldStatus?: OrderStatus;
  newStatus: OrderStatus;
  timestamp: string;
  reason?: string;
}

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
  confirmedAt?: string;
  rejectedAt?: string;
  estimatedReadyAt?: string;
  rejectReason?: string;
  preparationStartedAt?: string;
  readyAt?: string;
  completedAt?: string;
  paymentReceived: boolean;
  paymentMethodUsed?: PaymentMethodUsed;
  paymentReceivedAt?: string;
  statusHistory: OrderStatusHistory[];
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
  preparationStartedAt?: string;
  readyAt?: string;
  completedAt?: string;
  paymentReceived: boolean;
  paymentMethodUsed?: PaymentMethodUsed;
}

export type CustomerOrdersPage = PagedResponse<OrderSummary>;

export type CustomerOrderFilter =
  | "All"
  | "Active"
  | "Completed"
  | "Cancelled"
  | "Rejected";

export interface RepeatOrderOption {
  productOptionGroupId: string;
  optionGroupName: string;
  optionValueId: string;
  optionValueName: string;
  priceModifier: number;
  volumeMilliliters?: number;
  calories?: number;
}

export interface RepeatOrderItem {
  productId: string;
  productName: string;
  basePrice: number;
  unitPrice: number;
  currency: string;
  quantity: number;
  options: RepeatOrderOption[];
}

export interface RepeatOrderIssue {
  productName: string;
  quantity: number;
  reasons: string[];
}

export interface RepeatOrderResult {
  sourceOrderNumber: string;
  availableItems: RepeatOrderItem[];
  unavailableItems: RepeatOrderIssue[];
}

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
  preparationStartedAt?: string;
  readyAt?: string;
  completedAt?: string;
  paymentReceived: boolean;
  paymentMethodUsed?: PaymentMethodUsed;
  itemQuantity: number;
  rowVersion: string;
}

export interface StaffOrderDetail extends StaffOrderSummary {
  rejectReason?: string;
  subtotal: number;
  discountTotal: number;
  confirmedAt?: string;
  rejectedAt?: string;
  statusHistory: OrderStatusHistory[];
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

export interface OrderVersionInput {
  rowVersion: string;
}

export interface RecordPaymentInput extends OrderVersionInput {
  paymentMethodUsed: PaymentMethodUsed;
}

export interface KitchenOrder {
  id: string;
  orderNumber: string;
  customerName: string;
  customerPhoneNumber: string;
  createdAt: string;
  pickupMode: PickupMode;
  requestedPickupTime?: string;
  estimatedReadyAt?: string;
  preparationStartedAt?: string;
  readyAt?: string;
  status: Extract<OrderStatus, "Confirmed" | "Preparing" | "ReadyForPickup">;
  paymentMethod: PaymentMethod;
  paymentReceived: boolean;
  paymentMethodUsed?: PaymentMethodUsed;
  total: number;
  currency: string;
  comment?: string;
  itemQuantity: number;
  rowVersion: string;
  items: OrderItem[];
}

export type KitchenOrdersPage = PagedResponse<KitchenOrder>;

export interface KitchenOrderFilters {
  status?: "Confirmed" | "Preparing" | "ReadyForPickup";
  createdFrom?: string;
  createdTo?: string;
  pickupFrom?: string;
  pickupTo?: string;
  orderNumber?: string;
}

export interface OrderRealtimeEvent {
  eventId: string;
  timestamp: string;
  entityId: string;
  orderNumber: string;
  status: OrderStatus;
  estimatedReadyAt?: string;
  rejectReason?: string;
  preparationStartedAt?: string;
  readyAt?: string;
  completedAt?: string;
  paymentReceived: boolean;
  paymentMethodUsed?: PaymentMethodUsed;
}
