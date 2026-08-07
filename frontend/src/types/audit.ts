export interface AuditLogListItem {
  id: string;
  timestamp: string;
  employeeId: string;
  employeeName: string;
  actionType: string;
  entityType: string;
  entityId: string;
  description: string;
  correlationId: string;
}

export interface AuditLogDetail extends AuditLogListItem {
  oldValuesJson?: string;
  newValuesJson?: string;
}
