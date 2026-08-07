export interface HealthCheck {
  name: string;
  status: string;
  description?: string | null;
}

export interface HealthReport {
  status: string;
  checks: HealthCheck[];
}

export interface SystemInfo {
  service: string;
  environment: string;
  apiVersion: string;
  utcTime: string;
}
