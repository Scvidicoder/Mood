import type {
  AdminProductOptionGroup,
  AdminProductOptionValue,
  MenuMutationResponse,
} from "../../types/menu";
import { apiClient } from "../client";
import { queryString } from "../queryString";

export interface ProductGroupInput {
  optionGroupId: string;
  isRequired: boolean;
  minimumSelections: number;
  maximumSelections: number;
  displayOrder: number;
  isActive: boolean;
}

export interface ProductValueInput {
  optionValueId: string;
  priceModifier: number;
  isDefault: boolean;
  isAvailable: boolean;
  displayOrder: number;
  volumeMilliliters: number | null;
  calories: number | null;
}

export function addProductOptionGroup(
  productId: string,
  input: ProductGroupInput,
): Promise<MenuMutationResponse<AdminProductOptionGroup>> {
  return apiClient.post(`admin/products/${productId}/option-groups`, input);
}

export function updateProductOptionGroup(
  productId: string,
  assignmentId: string,
  input: Omit<ProductGroupInput, "optionGroupId"> & { rowVersion: string },
): Promise<MenuMutationResponse<AdminProductOptionGroup>> {
  return apiClient.put(
    `admin/products/${productId}/option-groups/${assignmentId}`,
    input,
  );
}

export function removeProductOptionGroup(
  productId: string,
  assignment: AdminProductOptionGroup,
): Promise<void> {
  return apiClient.delete(
    `admin/products/${productId}/option-groups/${assignment.id}${queryString({
      rowVersion: assignment.rowVersion,
    })}`,
  );
}

export function restoreProductOptionGroup(
  productId: string,
  assignment: AdminProductOptionGroup,
): Promise<MenuMutationResponse<AdminProductOptionGroup>> {
  return apiClient.post(
    `admin/products/${productId}/option-groups/${assignment.id}/restore`,
    { rowVersion: assignment.rowVersion },
  );
}

export function addProductOptionValue(
  productId: string,
  assignmentId: string,
  input: ProductValueInput,
): Promise<MenuMutationResponse<AdminProductOptionValue>> {
  return apiClient.post(
    `admin/products/${productId}/option-groups/${assignmentId}/values`,
    input,
  );
}

export function updateProductOptionValue(
  productId: string,
  assignmentValueId: string,
  input: Omit<ProductValueInput, "optionValueId"> & { rowVersion: string },
): Promise<MenuMutationResponse<AdminProductOptionValue>> {
  return apiClient.put(
    `admin/products/${productId}/option-values/${assignmentValueId}`,
    input,
  );
}

export function removeProductOptionValue(
  productId: string,
  assignment: AdminProductOptionValue,
): Promise<void> {
  return apiClient.delete(
    `admin/products/${productId}/option-values/${assignment.id}${queryString({
      rowVersion: assignment.rowVersion,
    })}`,
  );
}
