import type {
  CustomerProfile,
  UpdateCustomerProfileInput,
} from "../types/profile";
import { apiClient } from "./client";

export function getProfile(signal?: AbortSignal): Promise<CustomerProfile> {
  return apiClient.get<CustomerProfile>("profile", { signal });
}

export function updateProfile(
  input: UpdateCustomerProfileInput,
): Promise<CustomerProfile> {
  return apiClient.put<CustomerProfile>("profile", input);
}
