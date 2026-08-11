import { ApiError } from "../api/client";

export function fieldError(error: unknown, fieldName: string): string | undefined {
  if (!(error instanceof ApiError)) return undefined;
  const errors = error.errors ?? error.problemDetails?.errors;
  if (!errors) return undefined;
  const key = Object.keys(errors).find(
    (candidate) => candidate.toLowerCase() === fieldName.toLowerCase(),
  );
  return key ? errors[key]?.[0] : undefined;
}

export function isConcurrencyConflict(error: unknown): boolean {
  return error instanceof ApiError &&
    ["MENU_VERSION_CONFLICT", "EMPLOYEE_VERSION_CONFLICT"].includes(error.code ?? "");
}
