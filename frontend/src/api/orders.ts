import type {
  CreateOrderInput,
  ConfirmOrderInput,
  CustomerOrderFilter,
  CustomerOrdersPage,
  KitchenOrder,
  KitchenOrderFilters,
  KitchenOrdersPage,
  OrderDetail,
  OrderVersionInput,
  OrderStatus,
  RecordPaymentInput,
  RepeatOrderResult,
  RejectOrderInput,
  StaffOrderDetail,
  StaffOrdersPage,
  UpdateEstimatedReadyTimeInput,
} from "../types/orders";
import { apiClient } from "./client";
import { queryString } from "./queryString";

export function createOrder(input: CreateOrderInput): Promise<OrderDetail> {
  return apiClient.post<OrderDetail>("orders", input);
}

export function getOrder(id: string, signal?: AbortSignal): Promise<OrderDetail> {
  return apiClient.get<OrderDetail>(`orders/${id}`, { signal });
}

export function getMyOrders(
  page = 1,
  pageSize = 20,
  signal?: AbortSignal,
  filter: CustomerOrderFilter = "All",
  search?: string,
): Promise<CustomerOrdersPage> {
  return apiClient.get<CustomerOrdersPage>(
    `orders/mine${queryString({ page, pageSize, filter, search })}`,
    { signal },
  );
}

export function repeatOrder(id: string): Promise<RepeatOrderResult> {
  return apiClient.post<RepeatOrderResult>(`orders/${id}/repeat`);
}

export function cancelOrder(id: string): Promise<OrderDetail> {
  return apiClient.post<OrderDetail>(`orders/${id}/cancel`);
}

export function getStaffOrders(
  status?: OrderStatus,
  page = 1,
  pageSize = 100,
  signal?: AbortSignal,
): Promise<StaffOrdersPage> {
  return apiClient.get<StaffOrdersPage>(
    `staff/orders${queryString({ status, page, pageSize })}`,
    { signal },
  );
}

export function getStaffOrder(
  id: string,
  signal?: AbortSignal,
): Promise<StaffOrderDetail> {
  return apiClient.get<StaffOrderDetail>(`staff/orders/${id}`, { signal });
}

export function confirmOrder(
  id: string,
  input: ConfirmOrderInput,
): Promise<StaffOrderDetail> {
  return apiClient.post<StaffOrderDetail>(`staff/orders/${id}/confirm`, input);
}

export function rejectOrder(
  id: string,
  input: RejectOrderInput,
): Promise<StaffOrderDetail> {
  return apiClient.post<StaffOrderDetail>(`staff/orders/${id}/reject`, input);
}

export function updateEstimatedReadyTime(
  id: string,
  input: UpdateEstimatedReadyTimeInput,
): Promise<StaffOrderDetail> {
  return apiClient.put<StaffOrderDetail>(
    `staff/orders/${id}/estimated-ready-time`,
    input,
  );
}

export function getKitchenOrders(
  filters: KitchenOrderFilters = {},
  page = 1,
  pageSize = 100,
  signal?: AbortSignal,
): Promise<KitchenOrdersPage> {
  return apiClient.get<KitchenOrdersPage>(
    `staff/kitchen/orders${queryString({ ...filters, page, pageSize })}`,
    { signal },
  );
}

export function startKitchenOrder(
  id: string,
  input: OrderVersionInput,
): Promise<KitchenOrder> {
  return apiClient.post<KitchenOrder>(`staff/kitchen/${id}/start`, input);
}

export function markKitchenOrderReady(
  id: string,
  input: OrderVersionInput,
): Promise<KitchenOrder> {
  return apiClient.post<KitchenOrder>(`staff/kitchen/${id}/ready`, input);
}

export function updateKitchenOrderEta(
  id: string,
  input: UpdateEstimatedReadyTimeInput,
): Promise<KitchenOrder> {
  return apiClient.patch<KitchenOrder>(`staff/kitchen/${id}/eta`, input);
}

export function recordOrderPayment(
  id: string,
  input: RecordPaymentInput,
): Promise<StaffOrderDetail> {
  return apiClient.post<StaffOrderDetail>(
    `staff/orders/${id}/record-payment`,
    input,
  );
}

export function completeOrder(
  id: string,
  input: OrderVersionInput,
): Promise<StaffOrderDetail> {
  return apiClient.post<StaffOrderDetail>(`staff/orders/${id}/complete`, input);
}
