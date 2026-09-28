import { apiClient } from './apiClient';

/** Mirrors `AgentStatus` (src/shared/Agents/AgentStatus.cs); sent/received as a string (`.ToString()` on the DTO). */
export type AgentStatus = 'Active' | 'Disabled' | 'Deleted';

/** Mirrors `KnowledgeScopeType` (src/shared/Agents/KnowledgeScopeType.cs). */
export type KnowledgeScopeType = 'AllReadyEnabled' | 'Selected';

/** Mirrors `RetrievalMode` (src/shared/Retrieval/RetrievalMode.cs). */
export type RetrievalMode = 'Semantic' | 'Hybrid';

/**
 * Shape of `AgentResponse` - see src/application/Agents/AgentDtos.cs.
 * `prompt` is `null` when redacted for the caller (FR-AGT-006 - only
 * SuperAdmin/ApplicationAdmin receive the full prompt).
 */
export interface AgentResponse {
  id: string;
  applicationId: string;
  name: string;
  description: string | null;
  prompt: string | null;
  status: AgentStatus;
  knowledgeScopeType: KnowledgeScopeType;
  selectedFileIds: string[];
  generationDeployment: string;
  temperature: number;
  maximumOutputTokens: number;
  topP: number | null;
  retrievalMode: RetrievalMode;
  retrievalCount: number;
  minimumSimilarity: number;
  maximumEvidenceTokens: number;
  includeNeighbors: boolean;
  strictGrounding: boolean;
  citationsRequired: boolean;
  createdBy: string;
  createdAt: string;
  updatedBy: string | null;
  updatedAt: string | null;
  concurrencyToken: number;
}

/** Shape of `CreateAgentRequest`/`UpdateAgentRequest` - see src/application/Agents/AgentDtos.cs (same shape for both). */
export interface AgentFormRequest {
  name: string;
  description?: string | null;
  prompt: string;
  knowledgeScopeType: KnowledgeScopeType;
  selectedFileIds: string[] | null;
  generationDeployment: string;
  temperature: number;
  maximumOutputTokens: number;
  topP: number | null;
  retrievalMode: RetrievalMode;
  retrievalCount: number;
  minimumSimilarity: number;
  maximumEvidenceTokens: number;
  includeNeighbors: boolean;
  strictGrounding: boolean;
  citationsRequired: boolean;
}

/** TanStack Query keys for the TASK-AGT-02 endpoints. */
export const agentsQueryKeys = {
  all: ['agents'] as const,
  list: (applicationId: string) => [...agentsQueryKeys.all, 'list', applicationId] as const,
  detail: (applicationId: string, agentId: string) =>
    [...agentsQueryKeys.all, 'detail', applicationId, agentId] as const,
};

/** TASK-AGT-02: `GET /api/applications/{appId}/agents` - prompt redacted for Viewers (FR-AGT-006). */
export function listAgents(applicationId: string): Promise<AgentResponse[]> {
  return apiClient.get<AgentResponse[]>(`/api/applications/${applicationId}/agents`);
}

/** TASK-AGT-02: `GET /api/applications/{appId}/agents/{id}` - 404s for agents the caller can't view (cross-app masking). */
export function getAgent(applicationId: string, agentId: string): Promise<AgentResponse> {
  return apiClient.get<AgentResponse>(`/api/applications/${applicationId}/agents/${agentId}`);
}

/** TASK-AGT-02: `POST /api/applications/{appId}/agents` - requires `CanManageAgents`. */
export function createAgent(
  applicationId: string,
  request: AgentFormRequest,
): Promise<AgentResponse> {
  return apiClient.post<AgentResponse>(`/api/applications/${applicationId}/agents`, request);
}

/** TASK-AGT-02/FR-AGT-005: `PUT .../agents/{id}` - editing overwrites the single current configuration wholesale. */
export function updateAgent(
  applicationId: string,
  agentId: string,
  request: AgentFormRequest,
): Promise<AgentResponse> {
  return apiClient.put<AgentResponse>(
    `/api/applications/${applicationId}/agents/${agentId}`,
    request,
  );
}

/** TASK-AGT-02: `POST .../agents/{id}/enable` - 409s if not currently Disabled. */
export function enableAgent(applicationId: string, agentId: string): Promise<AgentResponse> {
  return apiClient.post<AgentResponse>(
    `/api/applications/${applicationId}/agents/${agentId}/enable`,
  );
}

/** TASK-AGT-02: `POST .../agents/{id}/disable` - 409s if not currently Active. */
export function disableAgent(applicationId: string, agentId: string): Promise<AgentResponse> {
  return apiClient.post<AgentResponse>(
    `/api/applications/${applicationId}/agents/${agentId}/disable`,
  );
}

/** TASK-AGT-02: `DELETE .../agents/{id}` - idempotent soft-delete. */
export function deleteAgent(applicationId: string, agentId: string): Promise<AgentResponse> {
  return apiClient.delete<AgentResponse>(`/api/applications/${applicationId}/agents/${agentId}`);
}
