import type {
  CreateOrderInput,
  CustomerOrdersPage,
  OrderDetail,
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
