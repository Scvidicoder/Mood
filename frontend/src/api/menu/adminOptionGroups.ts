import type {
  AdminOptionGroup,
  OptionGroupInput,
  PagedResponse,
} from "../../types/menu";
import { apiClient } from "../client";
import { queryString } from "../queryString";

export interface OptionGroupFilters {
  search?: string;
  isActive?: boolean;
  includeDeleted?: boolean;
  page?: number;
  pageSize?: number;
}

export function getOptionGroups(
  filters: OptionGroupFilters,
  signal?: AbortSignal,
): Promise<PagedResponse<AdminOptionGroup>> {
  return apiClient.get(
    `admin/option-groups${queryString({
      search: filters.search,
      isActive: filters.isActive,
      includeDeleted: filters.includeDeleted,
      page: filters.page ?? 1,
      pageSize: filters.pageSize ?? 20,
    })}`,
    { signal },
  );
}

export function getOptionGroup(
  id: string,
  signal?: AbortSignal,
): Promise<AdminOptionGroup> {
  return apiClient.get(`admin/option-groups/${id}`, { signal });
}

export function createOptionGroup(
  input: OptionGroupInput,
): Promise<AdminOptionGroup> {
  return apiClient.post("admin/option-groups", input);
}

export function updateOptionGroup(
  id: string,
  input: OptionGroupInput & { rowVersion: string },
): Promise<AdminOptionGroup> {
  return apiClient.put(`admin/option-groups/${id}`, input);
}

export function setOptionGroupActive(
  group: AdminOptionGroup,
): Promise<AdminOptionGroup> {
  return apiClient.patch(`admin/option-groups/${group.id}/active`, {
    isActive: !group.isActive,
    rowVersion: group.rowVersion,
  });
}

export function deleteOptionGroup(group: AdminOptionGroup): Promise<void> {
  return apiClient.delete(
    `admin/option-groups/${group.id}${queryString({
      rowVersion: group.rowVersion,
    })}`,
  );
}

export function restoreOptionGroup(
  group: AdminOptionGroup,
): Promise<AdminOptionGroup> {
  return apiClient.post(`admin/option-groups/${group.id}/restore`, {
    rowVersion: group.rowVersion,
  });
}
