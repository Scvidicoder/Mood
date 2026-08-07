import type { HealthReport, SystemInfo } from "../types/health";
import { apiClient } from "./client";

export const healthApi = {
  getLive(signal?: AbortSignal): Promise<HealthReport> {
    return apiClient.getRoot<HealthReport>("/health/live", { signal });
  },
  getReady(signal?: AbortSignal): Promise<HealthReport> {
    return apiClient.getRoot<HealthReport>("/health/ready", { signal });
  },
  getSystemInfo(signal?: AbortSignal): Promise<SystemInfo> {
    return apiClient.get<SystemInfo>("/system/info", { signal });
  },
};
