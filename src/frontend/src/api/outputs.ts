import { apiClient } from './apiClient';

/** Mirrors `JobStatus` (src/shared/Jobs/JobStatus.cs); sent as the enum name in query strings (ASP.NET Core's default enum model binding). */
export type RunStatus = 'Queued' | 'Running' | 'Completed' | 'Failed' | 'Cancelled';

/** Shape of `OutputSummaryResponse` - see src/application/Outputs/OutputsContracts.cs. TASK-OUT-05. */
export interface OutputSummaryResponse {
  id: string;
  title: string;
  agentId: string;
  agentName: string;
  createdBy: string;
  createdAt: string;
  isEdited: boolean;
}

/** Shape of `CitationDetailResponse` - see src/application/Outputs/OutputsContracts.cs. TASK-CIT-04. */
export interface CitationDetailResponse {
  id: string;
  sourceId: string;
  fileName: string;
  headingPath: string;
  passage: string;
  characterStart: number;
  characterEnd: number;
  processedAt: string | null;
  sourceUnavailable: boolean;
}

/** Shape of `OutputDetailResponse` - see src/application/Outputs/OutputsContracts.cs. TASK-OUT-02. */
export interface OutputDetailResponse {
  id: string;
  applicationId: string;
  agentRunId: string;
  title: string;
  originalMarkdown: string;
  currentMarkdown: string;
  isEdited: boolean;
  createdBy: string;
  createdAt: string;
  lastEditedBy: string | null;
  lastEditedAt: string | null;
  agentId: string;
  agentName: string;
  deploymentSnapshot: string | null;
  requestedParametersJson: string | null;
  effectiveParametersJson: string | null;
  retrievalSettingsJson: string | null;
  citations: CitationDetailResponse[];
}

/** Shape of `RegenerateOutputResponse` - see src/application/Outputs/OutputsContracts.cs. TASK-OUT-04. */
export interface RegenerateOutputResponse {
  runId: string;
}

export interface ListOutputsParams {
  search?: string;
  agentId?: string;
  createdBy?: string;
  from?: string;
  to?: string;
  status?: RunStatus;
}

/** TanStack Query keys for the TASK-OUT-02/03/04/05 + TASK-CIT-04 endpoints. */
export const outputsQueryKeys = {
  all: ['outputs'] as const,
  list: (applicationId: string, params: ListOutputsParams = {}) =>
    [...outputsQueryKeys.all, 'list', applicationId, params] as const,
  detail: (applicationId: string, outputId: string) =>
    [...outputsQueryKeys.all, 'detail', applicationId, outputId] as const,
  citations: (applicationId: string, outputId: string) =>
    [...outputsQueryKeys.all, 'citations', applicationId, outputId] as const,
  citation: (applicationId: string, outputId: string, citationId: string) =>
    [...outputsQueryKeys.all, 'citation', applicationId, outputId, citationId] as const,
};

/** TASK-OUT-05: `GET /api/applications/{appId}/outputs` - output library listing (no server-side pagination; client paginates). */
export function listOutputs(
  applicationId: string,
  params: ListOutputsParams = {},
): Promise<OutputSummaryResponse[]> {
  const query = new URLSearchParams();
  if (params.search) {
    query.set('search', params.search);
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
  if (params.status) {
    query.set('status', params.status);
  }
  const qs = query.toString();
  return apiClient.get<OutputSummaryResponse[]>(
    `/api/applications/${applicationId}/outputs${qs ? `?${qs}` : ''}`,
  );
}

/** TASK-OUT-02: `GET /api/applications/{appId}/outputs/{outputId}` - full detail incl. citations; 404s for deleted/cross-app outputs. */
export function getOutputDetail(
  applicationId: string,
  outputId: string,
): Promise<OutputDetailResponse> {
  return apiClient.get<OutputDetailResponse>(
    `/api/applications/${applicationId}/outputs/${outputId}`,
  );
}

/** TASK-OUT-03: `PUT .../outputs/{outputId}` - admin-only edit; permanently marks the output as edited. */
export function editOutput(
  applicationId: string,
  outputId: string,
  currentMarkdown: string,
): Promise<OutputDetailResponse> {
  return apiClient.put<OutputDetailResponse>(
    `/api/applications/${applicationId}/outputs/${outputId}`,
    { currentMarkdown },
  );
}

/** TASK-OUT-04: `POST .../outputs/{outputId}/regenerate` - queues a brand-new run with the original request against the agent's current live config. */
export function regenerateOutput(
  applicationId: string,
  outputId: string,
): Promise<RegenerateOutputResponse> {
  return apiClient.post<RegenerateOutputResponse>(
    `/api/applications/${applicationId}/outputs/${outputId}/regenerate`,
  );
}

/** TASK-OUT-04: `DELETE .../outputs/{outputId}` - soft delete; idempotent. */
export function deleteOutput(applicationId: string, outputId: string): Promise<void> {
  return apiClient.delete<void>(`/api/applications/${applicationId}/outputs/${outputId}`);
}

/** TASK-CIT-04: `GET .../outputs/{outputId}/citations` - full citation/evidence list for the output. */
export function getOutputCitations(
  applicationId: string,
  outputId: string,
): Promise<CitationDetailResponse[]> {
  return apiClient.get<CitationDetailResponse[]>(
    `/api/applications/${applicationId}/outputs/${outputId}/citations`,
  );
}

/** TASK-CIT-04: `GET .../outputs/{outputId}/citations/{citationId}` - single citation's evidence detail. */
export function getOutputCitation(
  applicationId: string,
  outputId: string,
  citationId: string,
): Promise<CitationDetailResponse> {
  return apiClient.get<CitationDetailResponse>(
    `/api/applications/${applicationId}/outputs/${outputId}/citations/${citationId}`,
  );
}
