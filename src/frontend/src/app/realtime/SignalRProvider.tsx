import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import {
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
  type HubConnection,
} from '@microsoft/signalr';
import { useQueryClient } from '@tanstack/react-query';
import { API_BASE_URL } from '@/api/config';
import { dashboardsQueryKeys } from '@/api/dashboards';
import { knowledgeFilesQueryKeys } from '@/api/knowledgeFiles';
import { outputsQueryKeys } from '@/api/outputs';
import { processingJobsQueryKeys } from '@/api/processingJobs';
import { runsQueryKeys } from '@/api/runs';
import {
  RealtimeContext,
  RealtimeEventTypes,
  type RealtimeConnectionStatus,
  type RealtimeContextValue,
  type RealtimeEnvelope,
} from './realtime-context';

const HUB_URL = `${API_BASE_URL}/hubs/realtime`;

/** Backoff schedule handed to `withAutomaticReconnect` - matches `[0, 2s, 5s, 10s, 15s, 30s]` then holds at 30s. */
const RECONNECT_DELAYS_MS = [0, 2000, 5000, 10000, 15000, 30000];

/** How long to wait before retrying an initial connect (or a fully-given-up reconnect) that failed outright. */
const MANUAL_RESTART_DELAY_MS = 5000;

/**
 * TASK-SIG-02/06: events carrying a per-entity `EventSequence` - out-of-
 * order/duplicate deliveries (retries, at-least-once relay from the
 * workers) must be ignored rather than reapplied.
 */
const ENTITY_SEQUENCED_EVENT_TYPES = new Set<string>([
  RealtimeEventTypes.FileQueued,
  RealtimeEventTypes.FileStageChanged,
  RealtimeEventTypes.FileCompleted,
  RealtimeEventTypes.FileFailed,
  RealtimeEventTypes.RunQueued,
  RealtimeEventTypes.RunStageChanged,
  RealtimeEventTypes.RunCompleted,
  RealtimeEventTypes.RunFailed,
  RealtimeEventTypes.RunCancelled,
]);

const FILE_EVENT_TYPES = new Set<string>([
  RealtimeEventTypes.FileQueued,
  RealtimeEventTypes.FileStageChanged,
  RealtimeEventTypes.FileCompleted,
  RealtimeEventTypes.FileFailed,
]);

const RUN_EVENT_TYPES = new Set<string>([
  RealtimeEventTypes.RunQueued,
  RealtimeEventTypes.RunStageChanged,
  RealtimeEventTypes.RunCompleted,
  RealtimeEventTypes.RunFailed,
  RealtimeEventTypes.RunCancelled,
]);

/**
 * TASK-SIG-05/06 (Module 25): owns the single SignalR connection to
 * `/hubs/realtime` for the whole app - lifecycle (connect/reconnect/give-up
 * and retry), per-entity stale-event rejection, and TanStack Query
 * invalidation for the live-updating list/dashboard views (knowledge files,
 * outputs, and - as of Modules 26/27 - the platform and application
 * dashboards).
 *
 * `DashboardChanged` (Modules 26/27) carries no per-entity sequence (it's a
 * coarse "something changed, refetch counts" signal, not a per-entity
 * state machine) - it always invalidates the platform dashboard query plus
 * the specific application's dashboard query.
 *
 * Mounted inside `QueryClientProvider` (needs `useQueryClient`) - see `App.tsx`.
 */
export function SignalRProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient();
  const [status, setStatus] = useState<RealtimeConnectionStatus>('connecting');

  const connectionRef = useRef<HubConnection | null>(null);
  const lastSequenceRef = useRef(new Map<string, number>());
  const applicationGroupsRef = useRef(new Set<string>());
  const runGroupsRef = useRef(new Set<string>());
  const operationsGroupJoinedRef = useRef(false);

  const handleEnvelope = useCallback(
    (envelope: RealtimeEnvelope) => {
      if (ENTITY_SEQUENCED_EVENT_TYPES.has(envelope.eventType)) {
        const lastSeen = lastSequenceRef.current.get(envelope.entityId) ?? -1;
        if (envelope.sequence <= lastSeen) {
          return; // TASK-SIG-06: stale/duplicate delivery - ignore.
        }
        lastSequenceRef.current.set(envelope.entityId, envelope.sequence);
      }

      if (FILE_EVENT_TYPES.has(envelope.eventType)) {
        void queryClient.invalidateQueries({
          queryKey: knowledgeFilesQueryKeys.list(envelope.applicationId),
        });
        // TASK-PRDSH-02: the application-scoped processing dashboard also
        // reflects file-driven job stage transitions - platform-wide
        // processing isn't reachable from these events (only DashboardChanged
        // reaches `platform:operations`, handled below).
        void queryClient.invalidateQueries({
          queryKey: processingJobsQueryKeys.list({ applicationId: envelope.applicationId }),
        });
      } else if (RUN_EVENT_TYPES.has(envelope.eventType)) {
        void queryClient.invalidateQueries({
          queryKey: outputsQueryKeys.list(envelope.applicationId),
        });
        // TASK-RDSH-01/02: the run entityId is the AgentRunId for every Run*
        // event, so both the run-dashboard list and this specific run's
        // detail view (if open) can be precisely invalidated.
        void queryClient.invalidateQueries({
          queryKey: runsQueryKeys.list(envelope.applicationId),
        });
        void queryClient.invalidateQueries({
          queryKey: runsQueryKeys.detail(envelope.applicationId, envelope.entityId),
        });
      } else if (envelope.eventType === RealtimeEventTypes.DashboardChanged) {
        void queryClient.invalidateQueries({ queryKey: dashboardsQueryKeys.platform() });
        void queryClient.invalidateQueries({
          queryKey: dashboardsQueryKeys.application(envelope.applicationId),
        });
        // TASK-PRDSH-01/TASK-RDSH-01: DashboardChanged is the only event that
        // reaches `platform:operations` - use it as an eventually-consistent
        // refresh signal for the platform-wide processing view.
        void queryClient.invalidateQueries({
          queryKey: processingJobsQueryKeys.list({ platform: true }),
        });
      }
    },
    [queryClient],
  );

  const rejoinAllGroups = useCallback((connection: HubConnection) => {
    for (const applicationId of applicationGroupsRef.current) {
      void connection.invoke('JoinApplication', applicationId).catch(() => {});
    }
    for (const runId of runGroupsRef.current) {
      void connection.invoke('JoinRun', runId).catch(() => {});
    }
    if (operationsGroupJoinedRef.current) {
      void connection.invoke('JoinOperations').catch(() => {});
    }
  }, []);

  useEffect(() => {
    let cancelled = false;
    let restartTimer: ReturnType<typeof setTimeout> | undefined;
    let connection: HubConnection | null = null;

    function scheduleManualRestart() {
      restartTimer = setTimeout(() => {
        void start();
      }, MANUAL_RESTART_DELAY_MS);
    }

    // Everything that can update `status` lives inside this async function
    // (invoked only via `void start()`, never inline in the effect body) so
    // a synchronous build failure can't cascade-render during the effect
    // itself (react-hooks/set-state-in-effect).
    async function start() {
      if (cancelled) {
        return;
      }
      setStatus((current) => (current === 'connected' ? current : 'connecting'));

      if (!connection) {
        try {
          connection = new HubConnectionBuilder()
            .withUrl(HUB_URL, { withCredentials: true })
            .withAutomaticReconnect(RECONNECT_DELAYS_MS)
            .configureLogging(LogLevel.Warning)
            .build();
        } catch {
          // Building the connection itself should never crash the app (e.g.
          // an environment where the relative hub URL can't be resolved) -
          // degrade to a disconnected indicator instead of throwing.
          if (!cancelled) {
            setStatus('disconnected');
            scheduleManualRestart();
          }
          return;
        }

        for (const eventType of Object.values(RealtimeEventTypes)) {
          connection.on(eventType, handleEnvelope);
        }

        connection.onreconnecting(() => {
          if (!cancelled) {
            setStatus('reconnecting');
          }
        });

        connection.onreconnected(() => {
          if (cancelled || !connection) {
            return;
          }
          setStatus('connected');
          rejoinAllGroups(connection);
          // TASK-SIG-06: REST remains authoritative after any connection gap
          // - force every cached query to refetch rather than trusting that
          // no events were missed while disconnected.
          void queryClient.invalidateQueries();
        });

        connection.onclose(() => {
          if (cancelled) {
            return;
          }
          // `withAutomaticReconnect` gives up permanently after the last
          // delay in `RECONNECT_DELAYS_MS` - restart manually so the
          // connection is never abandoned for the lifetime of the session.
          setStatus('disconnected');
          scheduleManualRestart();
        });

        connectionRef.current = connection;
      }

      try {
        await connection.start();
        if (cancelled) {
          return;
        }
        setStatus('connected');
        rejoinAllGroups(connection);
      } catch {
        if (!cancelled) {
          setStatus('disconnected');
          scheduleManualRestart();
        }
      }
    }

    void start();

    return () => {
      cancelled = true;
      if (restartTimer) {
        clearTimeout(restartTimer);
      }
      connectionRef.current = null;
      void connection?.stop();
    };
  }, [handleEnvelope, rejoinAllGroups, queryClient]);

  const joinApplicationGroup = useCallback((applicationId: string) => {
    applicationGroupsRef.current.add(applicationId);
    const connection = connectionRef.current;
    if (connection?.state === HubConnectionState.Connected) {
      void connection.invoke('JoinApplication', applicationId).catch(() => {});
    }
  }, []);

  const leaveApplicationGroup = useCallback((applicationId: string) => {
    applicationGroupsRef.current.delete(applicationId);
    const connection = connectionRef.current;
    if (connection?.state === HubConnectionState.Connected) {
      void connection.invoke('LeaveApplication', applicationId).catch(() => {});
    }
  }, []);

  const joinRunGroup = useCallback((runId: string) => {
    runGroupsRef.current.add(runId);
    const connection = connectionRef.current;
    if (connection?.state === HubConnectionState.Connected) {
      void connection.invoke('JoinRun', runId).catch(() => {});
    }
  }, []);

  const leaveRunGroup = useCallback((runId: string) => {
    runGroupsRef.current.delete(runId);
    const connection = connectionRef.current;
    if (connection?.state === HubConnectionState.Connected) {
      void connection.invoke('LeaveRun', runId).catch(() => {});
    }
  }, []);

  const joinOperationsGroup = useCallback(() => {
    operationsGroupJoinedRef.current = true;
    const connection = connectionRef.current;
    if (connection?.state === HubConnectionState.Connected) {
      void connection.invoke('JoinOperations').catch(() => {});
    }
  }, []);

  const leaveOperationsGroup = useCallback(() => {
    operationsGroupJoinedRef.current = false;
    const connection = connectionRef.current;
    if (connection?.state === HubConnectionState.Connected) {
      void connection.invoke('LeaveOperations').catch(() => {});
    }
  }, []);

  const value = useMemo<RealtimeContextValue>(
    () => ({
      status,
      joinApplicationGroup,
      leaveApplicationGroup,
      joinRunGroup,
      leaveRunGroup,
      joinOperationsGroup,
      leaveOperationsGroup,
    }),
    [
      status,
      joinApplicationGroup,
      leaveApplicationGroup,
      joinRunGroup,
      leaveRunGroup,
      joinOperationsGroup,
      leaveOperationsGroup,
    ],
  );

  return <RealtimeContext.Provider value={value}>{children}</RealtimeContext.Provider>;
}
