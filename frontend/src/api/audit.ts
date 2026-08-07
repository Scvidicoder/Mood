import type { AuditLogDetail, AuditLogListItem } from "../types/audit";
import type { PagedResponse } from "../types/menu";
import { apiClient } from "./client";
import { queryString } from "./queryString";

export interface AuditFilters {
  employeeId?: string;
  actionType?: string;
  entityType?: string;
  entityId?: string;
  dateFrom?: string;
  dateTo?: string;
  page?: number;
  pageSize?: number;
}

export function getAuditLog(
  filters: AuditFilters,
  signal?: AbortSignal,
): Promise<PagedResponse<AuditLogListItem>> {
  return apiClient.get(
    `admin/audit-log${queryString({
      ...filters,
      page: filters.page ?? 1,
      pageSize: filters.pageSize ?? 20,
    })}`,
    { signal },
  );
}

export function getAuditLogDetail(
  id: string,
  signal?: AbortSignal,
): Promise<AuditLogDetail> {
  return apiClient.get(`admin/audit-log/${id}`, { signal });
}
