import { apiClient } from './apiClient';

/**
 * Mirrors `DeploymentPurpose` (src/application/AzureOpenAI/IDeploymentRegistry.cs);
 * serialized as a number - no global string-enum JSON converter is registered.
 */
export type DeploymentPurpose = 0 | 1;
export const DEPLOYMENT_PURPOSE_CHAT: DeploymentPurpose = 0;
export const DEPLOYMENT_PURPOSE_EMBEDDING: DeploymentPurpose = 1;

/** Shape of `DeploymentInfo` - see src/application/AzureOpenAI/IDeploymentRegistry.cs. Never includes secrets. */
export interface DeploymentInfo {
  name: string;
  purpose: DeploymentPurpose;
  maxContextTokens: number | null;
  maxOutputTokens: number | null;
  embeddingDimensions: number | null;
  supportsTopP: boolean;
  /**
   * Whether the deployment accepts a non-default `temperature`. Reasoning-style
   * deployments (e.g. gpt-5.5) reject any other value with a hard 400, so the
   * UI hides the temperature control when this is `false`. Optional for
   * backward-compat with fixtures: absent is treated as supported.
   */
  supportsTemperature?: boolean;
}

export const deploymentsQueryKeys = {
  all: ['deployments'] as const,
  list: () => [...deploymentsQueryKeys.all, 'list'] as const,
};

/** TASK-AOI-01: `GET /api/deployments` - available to any authenticated caller; feeds the agent editor's deployment picker (TASK-AGT-04). */
export function listDeployments(): Promise<DeploymentInfo[]> {
  return apiClient.get<DeploymentInfo[]>('/api/deployments');
}

/** Convenience filter: only deployments usable as an agent's `generationDeployment`. */
export function chatDeployments(deployments: DeploymentInfo[]): DeploymentInfo[] {
  return deployments.filter((deployment) => deployment.purpose === DEPLOYMENT_PURPOSE_CHAT);
}
