import type {
  AdminProduct,
  AdminProductListItem,
  MenuMutationResponse,
  PagedResponse,
  ProductInput,
} from "../../types/menu";
import { apiClient } from "../client";
import { queryString } from "../queryString";

export interface ProductFilters {
  categoryId?: string;
  search?: string;
  isAvailable?: boolean;
  isVisible?: boolean;
  includeDeleted?: boolean;
  page?: number;
  pageSize?: number;
}

export function getAdminProducts(
  filters: ProductFilters,
  signal?: AbortSignal,
): Promise<PagedResponse<AdminProductListItem>> {
  return apiClient.get(
    `admin/products${queryString({
      categoryId: filters.categoryId,
      search: filters.search,
      isAvailable: filters.isAvailable,
      isVisible: filters.isVisible,
      includeDeleted: filters.includeDeleted,
      page: filters.page ?? 1,
      pageSize: filters.pageSize ?? 20,
    })}`,
    { signal },
  );
}

export function getAdminProduct(
  id: string,
  signal?: AbortSignal,
): Promise<AdminProduct> {
  return apiClient.get(`admin/products/${id}`, { signal });
}

export function createProduct(
  input: ProductInput,
): Promise<MenuMutationResponse<AdminProduct>> {
  return apiClient.post("admin/products", input);
}

export function updateProduct(
  id: string,
  input: ProductInput & { rowVersion: string },
): Promise<MenuMutationResponse<AdminProduct>> {
  return apiClient.put(`admin/products/${id}`, input);
}

export function duplicateProduct(
  id: string,
  name?: string,
): Promise<MenuMutationResponse<AdminProduct>> {
  return apiClient.post(`admin/products/${id}/duplicate`, {
    name: name?.trim() || null,
  });
}

export function setProductAvailability(
  product: Pick<AdminProductListItem, "id" | "isAvailable" | "rowVersion">,
): Promise<MenuMutationResponse<AdminProduct>> {
  return apiClient.patch(`admin/products/${product.id}/availability`, {
    isAvailable: !product.isAvailable,
    rowVersion: product.rowVersion,
  });
}

export function setProductVisibility(
  product: Pick<AdminProductListItem, "id" | "isVisible" | "rowVersion">,
): Promise<MenuMutationResponse<AdminProduct>> {
  return apiClient.patch(`admin/products/${product.id}/visibility`, {
    isVisible: !product.isVisible,
    rowVersion: product.rowVersion,
  });
}

export function assignProductImage(
  id: string,
  imageId: string | null,
  rowVersion: string,
): Promise<MenuMutationResponse<AdminProduct>> {
  return apiClient.put(`admin/products/${id}/image`, {
    imageId,
    rowVersion,
  });
}

export function deleteProduct(product: AdminProductListItem): Promise<void> {
  return apiClient.delete(
    `admin/products/${product.id}${queryString({
      rowVersion: product.rowVersion,
    })}`,
  );
}

export function restoreProduct(
  product: AdminProductListItem,
): Promise<MenuMutationResponse<AdminProduct>> {
  return apiClient.post(`admin/products/${product.id}/restore`, {
    rowVersion: product.rowVersion,
  });
}

export function reorderProducts(
  categoryId: string,
  items: Array<Pick<AdminProductListItem, "id" | "displayOrder" | "rowVersion">>,
): Promise<AdminProductListItem[]> {
  return apiClient.put("admin/products/reorder", { categoryId, items });
}
