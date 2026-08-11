import type {
  CreateOrderInput,
  ConfirmOrderInput,
  CustomerOrdersPage,
  OrderDetail,
  OrderStatus,
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
): Promise<CustomerOrdersPage> {
  return apiClient.get<CustomerOrdersPage>(
    `orders/mine${queryString({ page, pageSize })}`,
    { signal },
  );
}

export function cancelOrder(id: string): Promise<OrderDetail> {
  return apiClient.post<OrderDetail>(`orders/${id}/cancel`);
}

export function getStaffOrders(
  status: OrderStatus | undefined = "PendingConfirmation",
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
