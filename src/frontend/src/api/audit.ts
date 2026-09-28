import { apiClient } from './apiClient';

/** Mirrors `AuditOutcome` (src/domain/Audit/AuditOutcome.cs). */
export type AuditOutcome = 'Success' | 'Failure';

/** Shape of `AuditEventResponse` - see src/api/AuditEndpoints.cs. TASK-AUD-03. */
export interface AuditEventResponse {
  id: string;
  occurredAt: string;
  action: string;
  actorSubject: string;
  outcome: AuditOutcome;
  applicationId: string | null;
  entityType: string | null;
  entityId: string | null;
  entityDisplayName: string | null;
  failureReason: string | null;
  correlationId: string | null;
}

/** Shape of `AuditEventSearchResponse` - see src/api/AuditEndpoints.cs. TASK-AUD-03. */
export interface AuditEventSearchResponse {
  items: AuditEventResponse[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface ListAuditEventsParams {
  actor?: string;
  action?: string;
  entityType?: string;
  outcome?: AuditOutcome;
  from?: string;
  to?: string;
  page?: number;
  pageSize?: number;
}

/** Scope discriminator for query keys: a specific application, or the platform-wide view. */
export type AuditScope = { applicationId: string } | { platform: true };

function scopeKey(scope: AuditScope): string {
  return 'applicationId' in scope ? scope.applicationId : 'platform';
}

/** TanStack Query keys for the TASK-AUD-03 endpoints - shared by both the platform-wide and per-application activity pages. */
export const auditQueryKeys = {
  all: ['audit'] as const,
  list: (scope: AuditScope, params: ListAuditEventsParams = {}) =>
    [...auditQueryKeys.all, 'list', scopeKey(scope), params] as const,
};

function buildQueryString(params: ListAuditEventsParams): string {
  const query = new URLSearchParams();
  if (params.actor) {
    query.set('actor', params.actor);
  }
  if (params.action) {
    query.set('action', params.action);
  }
  if (params.entityType) {
    query.set('entityType', params.entityType);
  }
  if (params.outcome) {
    query.set('outcome', params.outcome);
  }
  if (params.from) {
    query.set('from', params.from);
  }
  if (params.to) {
    query.set('to', params.to);
  }
  if (params.page) {
    query.set('page', String(params.page));
  }
  if (params.pageSize) {
    query.set('pageSize', String(params.pageSize));
  }
  const qs = query.toString();
  return qs ? `?${qs}` : '';
}

/** TASK-AUD-03: `GET /api/platform/audit` - platform-wide audit search (SuperAdmin/SuperViewer only). */
export function listPlatformAudit(
  params: ListAuditEventsParams = {},
): Promise<AuditEventSearchResponse> {
  return apiClient.get<AuditEventSearchResponse>(`/api/platform/audit${buildQueryString(params)}`);
}

/** TASK-AUD-03: `GET /api/applications/{appId}/activity` - application-scoped audit search (also platform-roles-only). */
export function listApplicationActivity(
  applicationId: string,
  params: ListAuditEventsParams = {},
): Promise<AuditEventSearchResponse> {
  return apiClient.get<AuditEventSearchResponse>(
    `/api/applications/${applicationId}/activity${buildQueryString(params)}`,
  );
}
