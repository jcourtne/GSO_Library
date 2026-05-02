export interface AuditEvent {
  id: number;
  eventType: string;
  username?: string;
  targetUsername?: string;
  ipAddress?: string;
  detail?: string;
  createdAt: string;
}

export const AUDIT_EVENT_TYPES = [
  'AccountDisable',
  'AccountEnable',
  'ArrangementCreate',
  'ArrangementDelete',
  'EnsembleCreate',
  'EnsembleDelete',
  'FileDelete',
  'FileDownload',
  'FileUpload',
  'LoginFailure',
  'LoginSuccess',
  'PasswordReset',
  'PerformanceCreate',
  'PerformanceDelete',
  'RoleGrant',
  'RoleRemove',
  'TokenRefresh',
] as const;
