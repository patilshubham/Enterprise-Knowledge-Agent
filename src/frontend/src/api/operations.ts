import { apiClient } from './apiClient';
import type { QueueDepth, WorkerHeartbeat } from './dashboards';

/** Mirrors `ComponentHealthResponse` (src/api/OperationsEndpoints.cs) - one dependency's readiness status. */
export interface ComponentHealth {
  name: string;
  status: 'Healthy' | 'Degraded' | 'Unhealthy';
  detail: string | null;
}

/** Shape of `OperationsHealthResponse` - see `GET /api/operations/health` (TASK-OPS-03). Deliberately excludes Azure OpenAI - fetched separately/on-demand via `getAoaiHealth`. */
export interface OperationsHealthResponse {
  components: ComponentHealth[];
  queues: QueueDepth[];
  workers: WorkerHeartbeat[];
  checkedAt: string;
}

/** Mirrors `DeploymentHealthStatus` (src/application/AzureOpenAI/IAoaiHealthService.cs). */
export interface DeploymentHealthStatus {
  deployment: string;
  purpose: 'Chat' | 'Embedding';
  reachable: boolean;
  checkedAt: string;
}

export const operationsQueryKeys = {
  all: ['operations'] as const,
  health: () => [...operationsQueryKeys.all, 'health'] as const,
  aoaiHealth: () => [...operationsQueryKeys.all, 'aoai-health'] as const,
};

/** TASK-OPS-03: `GET /api/operations/health` - requires `CanViewOperations` (SuperAdmin/SuperViewer only). */
export function getOperationsHealth(): Promise<OperationsHealthResponse> {
  return apiClient.get<OperationsHealthResponse>('/api/operations/health');
}

/**
 * TASK-AOI-05/OPS-03: `GET /api/operations/aoai-health` - a deliberately
 * user-initiated ("run deep check" button) probe of the Azure OpenAI
 * deployments, never auto-polled, to avoid burning model quota (ASDD §24).
 */
export function getAoaiHealth(): Promise<DeploymentHealthStatus[]> {
  return apiClient.get<DeploymentHealthStatus[]>('/api/operations/aoai-health');
}
