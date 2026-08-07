import type {
  AdminCategory,
  CategoryInput,
  PagedResponse,
} from "../../types/menu";
import { apiClient } from "../client";
import { queryString } from "../queryString";

export interface CategoryFilters {
  search?: string;
  includeDeleted?: boolean;
  page?: number;
  pageSize?: number;
}

export function getAdminCategories(
  filters: CategoryFilters,
  signal?: AbortSignal,
): Promise<PagedResponse<AdminCategory>> {
  return apiClient.get(
    `admin/categories${queryString({
      search: filters.search,
      includeDeleted: filters.includeDeleted,
      page: filters.page ?? 1,
      pageSize: filters.pageSize ?? 20,
    })}`,
    { signal },
  );
}

export function getAdminCategory(
  id: string,
  signal?: AbortSignal,
): Promise<AdminCategory> {
  return apiClient.get(`admin/categories/${id}`, { signal });
}

export function createCategory(input: CategoryInput): Promise<AdminCategory> {
  return apiClient.post("admin/categories", input);
}

export function updateCategory(
  id: string,
  input: CategoryInput & { rowVersion: string },
): Promise<AdminCategory> {
  return apiClient.put(`admin/categories/${id}`, input);
}

export function setCategoryVisibility(
  category: Pick<AdminCategory, "id" | "isVisible" | "rowVersion">,
): Promise<AdminCategory> {
  return apiClient.patch(`admin/categories/${category.id}/visibility`, {
    isVisible: !category.isVisible,
    rowVersion: category.rowVersion,
  });
}

export function deleteCategory(category: AdminCategory): Promise<void> {
  return apiClient.delete(
    `admin/categories/${category.id}${queryString({
      rowVersion: category.rowVersion,
    })}`,
  );
}

export function restoreCategory(category: AdminCategory): Promise<AdminCategory> {
  return apiClient.post(`admin/categories/${category.id}/restore`, {
    rowVersion: category.rowVersion,
  });
}

export function reorderCategories(
  items: Array<Pick<AdminCategory, "id" | "displayOrder" | "rowVersion">>,
): Promise<AdminCategory[]> {
  return apiClient.put("admin/categories/reorder", { items });
}
