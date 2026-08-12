import type { CustomerPayment } from "../types/orders";
import { apiClient } from "./client";

export function getPayment(
  paymentId: string,
  signal?: AbortSignal,
): Promise<CustomerPayment> {
  return apiClient.get<CustomerPayment>(`payments/${paymentId}`, { signal });
}

export function verifyPayment(paymentId: string): Promise<CustomerPayment> {
  return apiClient.post<CustomerPayment>(`payments/${paymentId}/verify`);
}

export type DevelopmentPaymentOutcome =
  | "success"
  | "failed"
  | "cancel"
  | "pending";

export function simulateDevelopmentPayment(
  paymentId: string,
  outcome: DevelopmentPaymentOutcome,
): Promise<CustomerPayment> {
  return apiClient.post<CustomerPayment>(`dev/payments/${paymentId}/${outcome}`);
}
