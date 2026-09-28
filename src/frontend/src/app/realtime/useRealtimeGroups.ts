import { useEffect } from 'react';
import { useRealtimeConnection } from './realtime-context';

/**
 * TASK-SIG-01/05: joins the `application:{id}` SignalR group for as long as
 * the calling page is mounted (server-side authorized in
 * `RealtimeHub.JoinApplication` - never trust this hook alone). Re-joins
 * are handled centrally by `SignalRProvider` after a reconnect, so pages
 * only need to declare "I'm viewing this application" once.
 */
export function useJoinApplicationGroup(applicationId: string | undefined): void {
  const { joinApplicationGroup, leaveApplicationGroup } = useRealtimeConnection();

  useEffect(() => {
    if (!applicationId) {
      return;
    }
    joinApplicationGroup(applicationId);
    return () => leaveApplicationGroup(applicationId);
  }, [applicationId, joinApplicationGroup, leaveApplicationGroup]);
}

/** TASK-SIG-01/05: joins the `run:{id}` SignalR group - see `useJoinApplicationGroup` above. */
export function useJoinRunGroup(runId: string | undefined): void {
  const { joinRunGroup, leaveRunGroup } = useRealtimeConnection();

  useEffect(() => {
    if (!runId) {
      return;
    }
    joinRunGroup(runId);
    return () => leaveRunGroup(runId);
  }, [runId, joinRunGroup, leaveRunGroup]);
}

/**
 * TASK-PDSH-02: joins the `platform:operations` SignalR group for as long as
 * the Platform Dashboard is mounted (server-side restricted to SuperAdmin/
 * SuperViewer in `RealtimeHub.JoinOperations` - never trust this hook
 * alone).
 */
export function useJoinOperationsGroup(): void {
  const { joinOperationsGroup, leaveOperationsGroup } = useRealtimeConnection();

  useEffect(() => {
    joinOperationsGroup();
    return () => leaveOperationsGroup();
  }, [joinOperationsGroup, leaveOperationsGroup]);
}
