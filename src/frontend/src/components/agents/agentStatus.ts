import type { AgentStatus } from '@/api/agents';
import type { StatusKind } from '@/components/status/status-tokens';

/** Maps `AgentStatus` onto the shared `StatusKind` badge vocabulary (TASK-UX-09). */
export const STATUS_KIND_BY_AGENT_STATUS: Record<AgentStatus, StatusKind> = {
  Active: 'ready',
  Disabled: 'disabled',
  Deleted: 'failed',
};
