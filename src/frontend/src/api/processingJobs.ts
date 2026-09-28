import { apiClient } from './apiClient';

/** Mirrors `JobStatus` (src/shared/Jobs/JobStatus.cs); sent as the enum name in query strings. */
export type JobStatus = 'Queued' | 'Running' | 'Completed' | 'Failed' | 'Cancelled';

/** Mirrors `ProcessingStage` (src/shared/Knowledge/ProcessingStage.cs). */
export type ProcessingStage =
  | 'Queued'
  | 'Downloading'
  | 'Validating'
  | 'Parsing'
  | 'Chunking'
  | 'GeneratingEmbeddings'
  | 'SavingVectors'
  | 'Finalizing'
  | 'Completed';

/** Shape of `ProcessingJobSummaryResponse` - see src/application/Jobs/ProcessingJobDtos.cs. TASK-PRDSH-01/02. */
export interface ProcessingJobSummaryResponse {
  id: string;
  applicationId: string;
  fileId: string;
  fileName: string;
  status: JobStatus;
  stage: ProcessingStage;
  progress: number;
  attemptNumber: number;
  createdAt: string;
  startedAt: string | null;
  completedAt: string | null;
  failureCategory: string | null;
  failureMessage: string | null;
  retryEligible: boolean;
}

/** Shape of `ProcessingJobDetailResponse` - adds worker/heartbeat/correlation fields for the detail drawer. */
export interface ProcessingJobDetailResponse extends ProcessingJobSummaryResponse {
  workerId: string | null;
  heartbeatAt: string | null;
  correlationId: string;
}

/** Shape of `JobRetryResponse` (src/api/JobsEndpoints.cs). TASK-JOB-06. */
export interface JobRetryResponse {
  id: string;
  status: JobStatus;
  attemptNumber: number;
}

export interface ListProcessingJobsParams {
  status?: JobStatus;
  from?: string;
  to?: string;
}

/** Scope discriminator for query keys: a specific application, or the platform-wide view. */
export type ProcessingJobsScope = { applicationId: string } | { platform: true };

function scopeKey(scope: ProcessingJobsScope): string {
  return 'applicationId' in scope ? scope.applicationId : 'platform';
}

/** TanStack Query keys for the TASK-PRDSH-01/02 endpoints - shared by both the platform-wide and per-application processing pages. */
export const processingJobsQueryKeys = {
  all: ['processingJobs'] as const,
  list: (scope: ProcessingJobsScope, params: ListProcessingJobsParams = {}) =>
    [...processingJobsQueryKeys.all, 'list', scopeKey(scope), params] as const,
  detail: (scope: ProcessingJobsScope, jobId: string) =>
    [...processingJobsQueryKeys.all, 'detail', scopeKey(scope), jobId] as const,
};

function buildQueryString(params: ListProcessingJobsParams): string {
  const query = new URLSearchParams();
  if (params.status) {
    query.set('status', params.status);
  }
  if (params.from) {
    query.set('from', params.from);
  }
  if (params.to) {
    query.set('to', params.to);
  }
  const qs = query.toString();
  return qs ? `?${qs}` : '';
}

/** TASK-PRDSH-01: `GET /api/platform/processing` - platform-wide processing-job listing (SuperAdmin/SuperViewer only). */
export function listPlatformProcessing(
  params: ListProcessingJobsParams = {},
): Promise<ProcessingJobSummaryResponse[]> {
  return apiClient.get<ProcessingJobSummaryResponse[]>(
    `/api/platform/processing${buildQueryString(params)}`,
  );
}

/** TASK-PRDSH-01: `GET /api/platform/processing/{jobId}` - platform-wide job detail. */
export function getPlatformProcessingDetail(jobId: string): Promise<ProcessingJobDetailResponse> {
  return apiClient.get<ProcessingJobDetailResponse>(`/api/platform/processing/${jobId}`);
}

/** TASK-PRDSH-02: `GET /api/applications/{appId}/processing` - application-scoped processing-job listing. */
export function listApplicationProcessing(
  applicationId: string,
  params: ListProcessingJobsParams = {},
): Promise<ProcessingJobSummaryResponse[]> {
  return apiClient.get<ProcessingJobSummaryResponse[]>(
    `/api/applications/${applicationId}/processing${buildQueryString(params)}`,
  );
}

/** TASK-PRDSH-02: `GET /api/applications/{appId}/processing/{jobId}` - application-scoped job detail. */
export function getApplicationProcessingDetail(
  applicationId: string,
  jobId: string,
): Promise<ProcessingJobDetailResponse> {
  return apiClient.get<ProcessingJobDetailResponse>(
    `/api/applications/${applicationId}/processing/${jobId}`,
  );
}

/** TASK-JOB-06: `POST /api/applications/{appId}/processing/{jobId}/retry` - manual retry of a Failed processing job. */
export function retryProcessingJob(
  applicationId: string,
  jobId: string,
): Promise<JobRetryResponse> {
  return apiClient.post<JobRetryResponse>(
    `/api/applications/${applicationId}/processing/${jobId}/retry`,
  );
}
