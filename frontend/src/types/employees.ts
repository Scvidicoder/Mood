export type EmployeeStatusFilter = "All" | "Active" | "Disabled";

export interface EmployeeListItem {
  id: string;
  fullName: string;
  username: string;
  roles: string[];
  isActive: boolean;
  mustChangePassword: boolean;
  createdAt: string;
  updatedAt: string;
  lastLoginAt?: string;
  rowVersion: string;
}

export type EmployeeDetails = EmployeeListItem;

export interface CreateEmployeeResponse {
  employee: EmployeeDetails;
  temporaryPassword: string;
}

export interface ResetEmployeePasswordResponse {
  temporaryPassword: string;
  mustChangePassword: boolean;
  rowVersion: string;
  revokedSessionCount: number;
}

export interface EmployeeActionListItem {
  id: string;
  timestamp: string;
  actingEmployeeId?: string;
  actingEmployeeName: string;
  actionType: string;
  entityType: string;
  entityId: string;
  description: string;
  correlationId: string;
}

export interface RoleOption {
  name: string;
  displayName: string;
}

export interface EmployeePermission {
  permission: string;
  displayName: string;
  group: string;
  roleAllowed: boolean;
  override?: boolean;
  isAllowed: boolean;
}

export interface EmployeePermissionsResponse {
  employeeId: string;
  permissions: EmployeePermission[];
}

export interface EmployeePermissionOverride {
  permission: string;
  isAllowed: boolean;
}
