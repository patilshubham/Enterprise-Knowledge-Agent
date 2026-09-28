import { createContext, useContext } from 'react';

/**
 * TASK-SIG-05/06: context/hook split into its own module so
 * `SignalRProvider.tsx` only exports the component (react-refresh's
 * only-export-components rule requires this for Fast Refresh to work).
 */

export type RealtimeConnectionStatus = 'connecting' | 'connected' | 'reconnecting' | 'disconnected';

/**
 * Mirrors `EnterpriseKnowledgeAgent.Shared.Realtime.RealtimeEventTypes`
 * (src/shared/Realtime/RealtimeEventTypes.cs) - the fixed SignalR
 * client-method/event-type catalog (ASDD §18.1). Values are the exact
 * strings the hub relay invokes via `connection.on(eventType, ...)`.
 */
export const RealtimeEventTypes = {
  FileQueued: 'FileQueued',
  FileStageChanged: 'FileStageChanged',
  FileCompleted: 'FileCompleted',
  FileFailed: 'FileFailed',
  RunQueued: 'RunQueued',
  RunStageChanged: 'RunStageChanged',
  RunCompleted: 'RunCompleted',
  RunFailed: 'RunFailed',
  RunCancelled: 'RunCancelled',
  DashboardChanged: 'DashboardChanged',
} as const;

export type RealtimeEventType = (typeof RealtimeEventTypes)[keyof typeof RealtimeEventTypes];

/**
 * Mirrors `SignalREvent<TPayload>` (src/shared/Realtime/SignalREvent.cs) as
 * it arrives over the wire (camelCase - System.Text.Json's default naming
 * policy for this API, verified against every other DTO in `src/api`).
 */
export interface RealtimeEnvelope<TPayload = unknown> {
  eventId: string;
  eventType: string;
  applicationId: string;
  entityId: string;
  occurredAtUtc: string;
  sequence: number;
  payload: TPayload;
}

export interface RealtimeContextValue {
  status: RealtimeConnectionStatus;
  joinApplicationGroup: (applicationId: string) => void;
  leaveApplicationGroup: (applicationId: string) => void;
  joinRunGroup: (runId: string) => void;
  leaveRunGroup: (runId: string) => void;
  joinOperationsGroup: () => void;
  leaveOperationsGroup: () => void;
}

/**
 * Default value used whenever no `SignalRProvider` is mounted (e.g. shell
 * tests that don't need live updates) - degrades gracefully to a neutral
 * disconnected state with no-op group management, mirroring
 * `useReadiness`'s "unknown" fallback rather than throwing.
 */
const DEFAULT_CONTEXT_VALUE: RealtimeContextValue = {
  status: 'disconnected',
  joinApplicationGroup: () => {},
  leaveApplicationGroup: () => {},
  joinRunGroup: () => {},
  leaveRunGroup: () => {},
  joinOperationsGroup: () => {},
  leaveOperationsGroup: () => {},
};

export const RealtimeContext = createContext<RealtimeContextValue>(DEFAULT_CONTEXT_VALUE);

export function useRealtimeConnection(): RealtimeContextValue {
  return useContext(RealtimeContext);
}
