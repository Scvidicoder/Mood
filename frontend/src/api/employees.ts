import type {
  CreateEmployeeResponse,
  EmployeeActionListItem,
  EmployeeDetails,
  EmployeeListItem,
  EmployeePermissionOverride,
  EmployeePermissionsResponse,
  EmployeeStatusFilter,
  ResetEmployeePasswordResponse,
  RoleOption,
} from "../types/employees";
import type { PagedResponse } from "../types/menu";
import { apiClient } from "./client";
import { queryString } from "./queryString";

export interface EmployeeFilters {
  search?: string;
  role?: string;
  status?: EmployeeStatusFilter;
  page?: number;
  pageSize?: number;
}

export interface EmployeeActionFilters {
  actionType?: string;
  entityType?: string;
  dateFrom?: string;
  dateTo?: string;
  page?: number;
  pageSize?: number;
}

export function getEmployees(
  filters: EmployeeFilters,
  signal?: AbortSignal,
): Promise<PagedResponse<EmployeeListItem>> {
  return apiClient.get(
    `admin/employees${queryString({
      ...filters,
      page: filters.page ?? 1,
      pageSize: filters.pageSize ?? 20,
    })}`,
    { signal },
  );
}

export function getEmployee(
  id: string,
  signal?: AbortSignal,
): Promise<EmployeeDetails> {
  return apiClient.get(`admin/employees/${id}`, { signal });
}

export function getEmployeeRoles(signal?: AbortSignal): Promise<RoleOption[]> {
  return apiClient.get("admin/roles", { signal });
}

export function getEmployeePermissions(
  id: string,
  signal?: AbortSignal,
): Promise<EmployeePermissionsResponse> {
  return apiClient.get(`admin/employees/${id}/permissions`, { signal });
}

export function replaceEmployeePermissionOverrides(
  id: string,
  overrides: EmployeePermissionOverride[],
): Promise<EmployeePermissionsResponse> {
  return apiClient.put(`admin/employees/${id}/permissions`, { overrides });
}

export function createEmployee(input: {
  fullName: string;
  username: string;
  roles: string[];
}): Promise<CreateEmployeeResponse> {
  return apiClient.post("admin/employees", input);
}

export function updateEmployee(
  id: string,
  input: {
    fullName: string;
    username: string;
    roles: string[];
    rowVersion: string;
  },
): Promise<EmployeeDetails> {
  return apiClient.put(`admin/employees/${id}`, input);
}

export function disableEmployee(
  id: string,
  rowVersion: string,
): Promise<EmployeeDetails> {
  return apiClient.post(`admin/employees/${id}/disable`, { rowVersion });
}

export function enableEmployee(
  id: string,
  rowVersion: string,
): Promise<EmployeeDetails> {
  return apiClient.post(`admin/employees/${id}/enable`, { rowVersion });
}

export function resetEmployeePassword(
  id: string,
  rowVersion: string,
): Promise<ResetEmployeePasswordResponse> {
  return apiClient.post(`admin/employees/${id}/reset-password`, { rowVersion });
}

export function getEmployeeActions(
  id: string,
  filters: EmployeeActionFilters,
  signal?: AbortSignal,
): Promise<PagedResponse<EmployeeActionListItem>> {
  return apiClient.get(
    `admin/employees/${id}/actions${queryString({
      ...filters,
      page: filters.page ?? 1,
      pageSize: filters.pageSize ?? 20,
    })}`,
    { signal },
  );
}
