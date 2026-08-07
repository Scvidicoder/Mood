import type { AdminOptionValue, OptionValueInput } from "../../types/menu";
import { apiClient } from "../client";
import { queryString } from "../queryString";

export function getOptionValues(
  groupId: string,
  includeDeleted = false,
  signal?: AbortSignal,
): Promise<AdminOptionValue[]> {
  return apiClient.get(
    `admin/option-groups/${groupId}/values${queryString({ includeDeleted })}`,
    { signal },
  );
}

export function createOptionValue(
  groupId: string,
  input: OptionValueInput,
): Promise<AdminOptionValue> {
  return apiClient.post(`admin/option-groups/${groupId}/values`, input);
}

export function updateOptionValue(
  id: string,
  input: OptionValueInput & { rowVersion: string },
): Promise<AdminOptionValue> {
  return apiClient.put(`admin/option-values/${id}`, input);
}

export function setOptionValueActive(
  value: AdminOptionValue,
): Promise<AdminOptionValue> {
  return apiClient.patch(`admin/option-values/${value.id}/active`, {
    isActive: !value.isActive,
    rowVersion: value.rowVersion,
  });
}

export function deleteOptionValue(value: AdminOptionValue): Promise<void> {
  return apiClient.delete(
    `admin/option-values/${value.id}${queryString({
      rowVersion: value.rowVersion,
    })}`,
  );
}

export function restoreOptionValue(
  value: AdminOptionValue,
): Promise<AdminOptionValue> {
  return apiClient.post(`admin/option-values/${value.id}/restore`, {
    rowVersion: value.rowVersion,
  });
}
