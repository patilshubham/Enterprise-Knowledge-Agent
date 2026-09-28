import { apiClient } from './apiClient';

/** Mirrors `JobStatus` (src/shared/Jobs/JobStatus.cs); sent as the enum name in query strings. */
export type JobStatus = 'Queued' | 'Running' | 'Completed' | 'Failed' | 'Cancelled';

/** Mirrors `RunStage` (src/shared/Execution/RunStage.cs). */
export type RunStage =
  | 'Queued'
  | 'Preparing'
  | 'Retrieving'
  | 'BuildingContext'
  | 'Generating'
  | 'Validating'
  | 'Saving'
  | 'Completed'
  | 'Failed'
  | 'Cancelled';

/** Shape of `AgentRunSummaryResponse` - see src/application/Execution/AgentRunDtos.cs. TASK-RDSH-01. */
export interface AgentRunSummaryResponse {
  id: string;
  applicationId: string;
  agentId: string;
  agentName: string;
  requestedBy: string;
  status: JobStatus;
  stage: RunStage;
  progress: number;
  createdAt: string;
  startedAt: string | null;
  completedAt: string | null;
  failureCategory: string | null;
  failureMessage: string | null;
  retryEligible: boolean;
}

/** Shape of `RunEvidenceResponse` - see src/application/Execution/AgentRunDtos.cs. TASK-RDSH-02. */
export interface RunEvidenceResponse {
  id: string;
  sourceId: string;
  fileName: string;
  headingPath: string;
  passage: string;
  characterStart: number;
  characterEnd: number;
  processedAt: string | null;
}

/** Shape of `AgentRunDetailResponse` - see src/application/Execution/AgentRunDtos.cs. TASK-RDSH-02. */
export interface AgentRunDetailResponse {
  id: string;
  applicationId: string;
  agentId: string;
  agentName: string;
  agentPromptSnapshot: string;
  knowledgeScopeSnapshotJson: string | null;
  deploymentSnapshot: string | null;
  requestedParametersJson: string | null;
  effectiveParametersJson: string | null;
  retrievalSettingsJson: string | null;
  requestedBy: string;
  userRequest: string;
  outputTitle: string | null;
  status: JobStatus;
  stage: RunStage;
  progress: number;
  workerId: string | null;
  createdAt: string;
  startedAt: string | null;
  heartbeatAt: string | null;
  completedAt: string | null;
  failureCategory: string | null;
  failureMessage: string | null;
  correlationId: string;
  cancellationRequested: boolean;
  retryEligible: boolean;
  generatedOutputId: string | null;
  evidence: RunEvidenceResponse[];
}

/** Shape of `JobRetryResponse` (src/api/JobsEndpoints.cs). TASK-JOB-06. */
export interface JobRetryResponse {
  id: string;
  status: JobStatus;
  attemptNumber: number;
}

export interface ListRunsParams {
  status?: JobStatus;
  agentId?: string;
  createdBy?: string;
  from?: string;
  to?: string;
}

/** TanStack Query keys for the TASK-RDSH-01/02 endpoints. */
export const runsQueryKeys = {
  all: ['runs'] as const,
  list: (applicationId: string, params: ListRunsParams = {}) =>
    [...runsQueryKeys.all, 'list', applicationId, params] as const,
  detail: (applicationId: string, runId: string) =>
    [...runsQueryKeys.all, 'detail', applicationId, runId] as const,
};

/** TASK-RDSH-01: `GET /api/applications/{appId}/runs` - agent-run dashboard listing (no server-side pagination; client paginates). */
export function listRuns(
  applicationId: string,
  params: ListRunsParams = {},
): Promise<AgentRunSummaryResponse[]> {
  const query = new URLSearchParams();
  if (params.status) {
    query.set('status', params.status);
  }
  if (params.agentId) {
    query.set('agentId', params.agentId);
  }
  if (params.createdBy) {
    query.set('createdBy', params.createdBy);
  }
  if (params.from) {
    query.set('from', params.from);
  }
  if (params.to) {
    query.set('to', params.to);
  }
  const qs = query.toString();
  return apiClient.get<AgentRunSummaryResponse[]>(
    `/api/applications/${applicationId}/runs${qs ? `?${qs}` : ''}`,
  );
}

/** TASK-RDSH-02: `GET /api/applications/{appId}/runs/{runId}` - full run detail incl. snapshot/evidence; 404s for deleted/cross-app runs. */
export function getRunDetail(
  applicationId: string,
  runId: string,
): Promise<AgentRunDetailResponse> {
  return apiClient.get<AgentRunDetailResponse>(`/api/applications/${applicationId}/runs/${runId}`);
}

/** TASK-JOB-06: `POST /api/applications/{appId}/runs/{runId}/retry` - manual retry of a Failed agent run. */
export function retryRun(applicationId: string, runId: string): Promise<JobRetryResponse> {
  return apiClient.post<JobRetryResponse>(`/api/applications/${applicationId}/runs/${runId}/retry`);
}

/** Request body for `submitRun` - mirrors `SubmitAgentRunRequest` (src/application/Execution/SubmitAgentRunRequest.cs). */
export interface SubmitRunRequest {
  userRequest: string;
  outputTitle?: string | null;
  idempotencyKey: string;
}

/** Shape of `AgentRunSubmissionResponse` (src/api/RunsEndpoints.cs). */
export interface AgentRunSubmissionResponse {
  runId: string;
}

/** TASK-RUN-01: `POST .../agents/{agentId}/runs` - submits a new agent run. */
export function submitRun(
  applicationId: string,
  agentId: string,
  request: SubmitRunRequest,
): Promise<AgentRunSubmissionResponse> {
  return apiClient.post<AgentRunSubmissionResponse>(
    `/api/applications/${applicationId}/agents/${agentId}/runs`,
    request,
  );
}

/** TASK-RUN-05: `POST .../agents/{agentId}/runs/{runId}/cancel` - pre-generation cancellation (nested under agentId, unlike list/detail/retry above). */
export function cancelRun(applicationId: string, agentId: string, runId: string): Promise<void> {
  return apiClient.post<void>(
    `/api/applications/${applicationId}/agents/${agentId}/runs/${runId}/cancel`,
  );
}
