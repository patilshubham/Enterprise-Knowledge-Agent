import { apiClient } from './apiClient';

/** Mirrors `JobStatusCountsResponse` (src/application/Dashboards/DashboardDtos.cs) - shared by processing jobs and agent runs. */
export interface JobStatusCounts {
  queued: number;
  running: number;
  completed: number;
  failed: number;
  cancelled: number;
}

/** Mirrors `KnowledgeFileCountsResponse` - counts by user-facing (Disabled/IsEnabled-projected) status. */
export interface KnowledgeFileCounts {
  queued: number;
  processing: number;
  ready: number;
  failed: number;
  disabled: number;
  deleting: number;
  deleted: number;
}

/** Mirrors `ApplicationCountsResponse`. */
export interface ApplicationCounts {
  active: number;
  archived: number;
  deleted: number;
}

/** Mirrors `WorkerHeartbeatResponse` - staleness is already evaluated server-side. */
export interface WorkerHeartbeat {
  workerId: string;
  workerKind: string;
  lastHeartbeatAt: string;
  isStale: boolean;
}

/** Mirrors `QueueDepthResponse`. */
export interface QueueDepth {
  queueName: string;
  approximateMessageCount: number;
  deadLetterQueueMessageCount: number;
}

/** Mirrors `DashboardAttentionItem` - a single "needs attention" callout; no entity ids, counts-only overview. */
export interface DashboardAttentionItem {
  kind: string;
  message: string;
  count: number;
}

/** Shape of `PlatformDashboardResponse` - see `GET /api/platform/dashboard` (FR-DSH-001, TASK-PDSH-01). */
export interface PlatformDashboardResponse {
  applications: ApplicationCounts;
  knowledgeFiles: KnowledgeFileCounts;
  processingJobs: JobStatusCounts;
  agentRuns: JobStatusCounts;
  workers: WorkerHeartbeat[];
  aoaiAvailable: boolean;
  queues: QueueDepth[];
  attentionItems: DashboardAttentionItem[];
}

/** Shape of `ApplicationDashboardResponse` - see `GET /api/applications/{id}/dashboard` (FR-APP-004, TASK-ADSH-01). */
export interface ApplicationDashboardResponse {
  applicationId: string;
  applicationName: string;
  knowledgeFiles: KnowledgeFileCounts;
  processingJobs: JobStatusCounts;
  agentRuns: JobStatusCounts;
  activeAgents: number;
  totalAgents: number;
  generatedOutputs: number;
  attentionItems: DashboardAttentionItem[];
}

/** TanStack Query keys for the dashboard endpoints - invalidated by `SignalRProvider` on `DashboardChanged`. */
export const dashboardsQueryKeys = {
  all: ['dashboards'] as const,
  platform: () => [...dashboardsQueryKeys.all, 'platform'] as const,
  application: (applicationId: string) =>
    [...dashboardsQueryKeys.all, 'application', applicationId] as const,
};

/** TASK-PDSH-01/02: `GET /api/platform/dashboard` - requires `CanViewPlatformProcessing` (SuperAdmin/SuperViewer only). */
export function getPlatformDashboard(): Promise<PlatformDashboardResponse> {
  return apiClient.get<PlatformDashboardResponse>('/api/platform/dashboard');
}

/** TASK-ADSH-01/02: `GET /api/applications/{id}/dashboard` - 404s (not 403) for applications the caller can't view. */
export function getApplicationDashboard(
  applicationId: string,
): Promise<ApplicationDashboardResponse> {
  return apiClient.get<ApplicationDashboardResponse>(
    `/api/applications/${applicationId}/dashboard`,
  );
}
