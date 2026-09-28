import { act, render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { knowledgeFilesQueryKeys } from '@/api/knowledgeFiles';
import { outputsQueryKeys } from '@/api/outputs';
import { processingJobsQueryKeys } from '@/api/processingJobs';
import { runsQueryKeys } from '@/api/runs';
import { RealtimeEventTypes, useRealtimeConnection } from '@/app/realtime/realtime-context';
import { SignalRProvider } from '@/app/realtime/SignalRProvider';

/**
 * TASK-SIG-05/06: exercises the provider's connection lifecycle, group
 * re-join on reconnect, and per-entity stale-event rejection without a real
 * network connection - `@microsoft/signalr` is mocked with a small fake
 * `HubConnection` whose lifecycle the test drives directly.
 */

type Handler = (...args: unknown[]) => void;

class FakeHubConnection {
  handlers = new Map<string, Handler>();
  reconnectingCb: (() => void) | null = null;
  reconnectedCb: (() => void) | null = null;
  closeCb: (() => void) | null = null;
  state = 'Disconnected';
  invocations: Array<{ method: string; args: unknown[] }> = [];
  startResult: 'resolve' | 'reject' = 'resolve';

  on(eventType: string, handler: Handler) {
    this.handlers.set(eventType, handler);
  }

  off(eventType: string) {
    this.handlers.delete(eventType);
  }

  onreconnecting(cb: () => void) {
    this.reconnectingCb = cb;
  }

  onreconnected(cb: () => void) {
    this.reconnectedCb = cb;
  }

  onclose(cb: () => void) {
    this.closeCb = cb;
  }

  start() {
    if (this.startResult === 'reject') {
      this.state = 'Disconnected';
      return Promise.reject(new Error('connect failed'));
    }
    this.state = 'Connected';
    return Promise.resolve();
  }

  stop() {
    this.state = 'Disconnected';
    return Promise.resolve();
  }

  invoke(method: string, ...args: unknown[]) {
    this.invocations.push({ method, args });
    return Promise.resolve();
  }

  emit(eventType: string, payload: unknown) {
    this.handlers.get(eventType)?.(payload);
  }
}

let lastConnection: FakeHubConnection;

vi.mock('@microsoft/signalr', () => {
  return {
    HubConnectionBuilder: class {
      withUrl() {
        return this;
      }
      withAutomaticReconnect() {
        return this;
      }
      configureLogging() {
        return this;
      }
      build() {
        lastConnection = new FakeHubConnection();
        return lastConnection;
      }
    },
    HubConnectionState: {
      Disconnected: 'Disconnected',
      Connecting: 'Connecting',
      Connected: 'Connected',
      Disconnecting: 'Disconnecting',
      Reconnecting: 'Reconnecting',
    },
    LogLevel: { Warning: 3 },
  };
});

function StatusProbe() {
  const { status } = useRealtimeConnection();
  return <div data-testid="status">{status}</div>;
}

function renderProvider(queryClient: QueryClient) {
  return render(
    <QueryClientProvider client={queryClient}>
      <SignalRProvider>
        <StatusProbe />
      </SignalRProvider>
    </QueryClientProvider>,
  );
}

describe('SignalRProvider (TASK-SIG-05/06)', () => {
  let queryClient: QueryClient;

  beforeEach(() => {
    queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  });

  afterEach(() => {
    vi.clearAllMocks();
  });

  it('transitions connecting -> connected once the hub connection starts', async () => {
    renderProvider(queryClient);

    await waitFor(() => expect(screen.getByTestId('status')).toHaveTextContent('connected'));
  });

  it('shows reconnecting then connected again on onreconnecting/onreconnected, and refreshes REST state', async () => {
    renderProvider(queryClient);
    await waitFor(() => expect(screen.getByTestId('status')).toHaveTextContent('connected'));

    const invalidateSpy = vi.spyOn(queryClient, 'invalidateQueries');

    act(() => {
      lastConnection.reconnectingCb?.();
    });
    expect(screen.getByTestId('status')).toHaveTextContent('reconnecting');

    act(() => {
      lastConnection.reconnectedCb?.();
    });
    expect(screen.getByTestId('status')).toHaveTextContent('connected');
    // TASK-SIG-06: reconnect forces a full REST refresh (no query key filter).
    expect(invalidateSpy).toHaveBeenCalledWith();
  });

  it('shows disconnected when the connection closes', async () => {
    renderProvider(queryClient);
    await waitFor(() => expect(screen.getByTestId('status')).toHaveTextContent('connected'));

    act(() => {
      lastConnection.closeCb?.();
    });
    expect(screen.getByTestId('status')).toHaveTextContent('disconnected');
  });

  it('invalidates the knowledge files list on a FileStageChanged event scoped to its applicationId', async () => {
    renderProvider(queryClient);
    await waitFor(() => expect(screen.getByTestId('status')).toHaveTextContent('connected'));

    const invalidateSpy = vi.spyOn(queryClient, 'invalidateQueries');

    act(() => {
      lastConnection.emit(RealtimeEventTypes.FileStageChanged, {
        eventId: 'e1',
        eventType: RealtimeEventTypes.FileStageChanged,
        applicationId: 'app-1',
        entityId: 'file-1',
        occurredAtUtc: new Date().toISOString(),
        sequence: 1,
        payload: { knowledgeFileId: 'file-1', stage: 4, progress: 60 },
      });
    });

    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: knowledgeFilesQueryKeys.list('app-1') });
    expect(invalidateSpy).toHaveBeenCalledWith({
      queryKey: processingJobsQueryKeys.list({ applicationId: 'app-1' }),
    });
  });

  it('invalidates the outputs list on a RunCompleted event scoped to its applicationId', async () => {
    renderProvider(queryClient);
    await waitFor(() => expect(screen.getByTestId('status')).toHaveTextContent('connected'));

    const invalidateSpy = vi.spyOn(queryClient, 'invalidateQueries');

    act(() => {
      lastConnection.emit(RealtimeEventTypes.RunCompleted, {
        eventId: 'e2',
        eventType: RealtimeEventTypes.RunCompleted,
        applicationId: 'app-2',
        entityId: 'run-1',
        occurredAtUtc: new Date().toISOString(),
        sequence: 1,
        payload: { agentRunId: 'run-1' },
      });
    });

    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: outputsQueryKeys.list('app-2') });
    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: runsQueryKeys.list('app-2') });
    expect(invalidateSpy).toHaveBeenCalledWith({
      queryKey: runsQueryKeys.detail('app-2', 'run-1'),
    });
  });

  it('ignores a stale/duplicate event whose sequence is not greater than the last seen sequence for that entity', async () => {
    renderProvider(queryClient);
    await waitFor(() => expect(screen.getByTestId('status')).toHaveTextContent('connected'));

    const envelope = (sequence: number) => ({
      eventId: `e-${sequence}`,
      eventType: RealtimeEventTypes.FileStageChanged,
      applicationId: 'app-1',
      entityId: 'file-1',
      occurredAtUtc: new Date().toISOString(),
      sequence,
      payload: { knowledgeFileId: 'file-1', stage: 4, progress: 60 },
    });

    act(() => {
      lastConnection.emit(RealtimeEventTypes.FileStageChanged, envelope(5));
    });

    const invalidateSpy = vi.spyOn(queryClient, 'invalidateQueries');

    act(() => {
      // Stale replay (same sequence) - must be ignored.
      lastConnection.emit(RealtimeEventTypes.FileStageChanged, envelope(5));
    });
    expect(invalidateSpy).not.toHaveBeenCalled();

    act(() => {
      // Older sequence (out-of-order delivery) - must also be ignored.
      lastConnection.emit(RealtimeEventTypes.FileStageChanged, envelope(3));
    });
    expect(invalidateSpy).not.toHaveBeenCalled();

    act(() => {
      // Newer sequence - applied.
      lastConnection.emit(RealtimeEventTypes.FileStageChanged, envelope(6));
    });
    // FileStageChanged now invalidates both the knowledge files list and the
    // (application-scoped) processing jobs list - see SignalRProvider.tsx.
    expect(invalidateSpy).toHaveBeenCalledTimes(2);
  });

  it('re-joins previously joined application/run groups after a reconnect', async () => {
    function Consumer() {
      const { joinApplicationGroup, joinRunGroup } = useRealtimeConnection();
      return (
        <button
          onClick={() => {
            joinApplicationGroup('app-1');
            joinRunGroup('run-1');
          }}
        >
          join
        </button>
      );
    }

    render(
      <QueryClientProvider client={queryClient}>
        <SignalRProvider>
          <Consumer />
        </SignalRProvider>
      </QueryClientProvider>,
    );

    await waitFor(() => expect(lastConnection.state).toBe('Connected'));

    act(() => {
      screen.getByRole('button', { name: 'join' }).click();
    });

    expect(lastConnection.invocations).toContainEqual({
      method: 'JoinApplication',
      args: ['app-1'],
    });
    expect(lastConnection.invocations).toContainEqual({ method: 'JoinRun', args: ['run-1'] });

    lastConnection.invocations = [];

    act(() => {
      lastConnection.reconnectedCb?.();
    });

    expect(lastConnection.invocations).toContainEqual({
      method: 'JoinApplication',
      args: ['app-1'],
    });
    expect(lastConnection.invocations).toContainEqual({ method: 'JoinRun', args: ['run-1'] });
  });
});
