import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import * as operationsApi from '@/api/operations';
import type { OperationsHealthResponse } from '@/api/operations';
import { ApiError } from '@/api/problemDetails';
import { RealtimeContext, type RealtimeContextValue } from '@/app/realtime/realtime-context';
import PlatformOperationsPage from '@/pages/platform/PlatformOperationsPage';

vi.mock('@/api/operations', async () => {
  const actual = await vi.importActual<typeof import('@/api/operations')>('@/api/operations');
  return { ...actual, getOperationsHealth: vi.fn(), getAoaiHealth: vi.fn() };
});

function makeHealth(overrides: Partial<OperationsHealthResponse> = {}): OperationsHealthResponse {
  return {
    components: [
      { name: 'Postgres + pgvector', status: 'Healthy', detail: null },
      { name: 'Object Storage (S3)', status: 'Healthy', detail: null },
    ],
    queues: [
      {
        queueName: 'document-processing',
        approximateMessageCount: 1,
        deadLetterQueueMessageCount: 0,
      },
    ],
    workers: [
      {
        workerId: 'worker-1',
        workerKind: 'document-worker',
        lastHeartbeatAt: '2024-01-01T00:00:00Z',
        isStale: false,
      },
    ],
    checkedAt: '2024-01-01T00:00:00Z',
    ...overrides,
  };
}

const REALTIME_VALUE: RealtimeContextValue = {
  status: 'connected',
  joinApplicationGroup: () => {},
  leaveApplicationGroup: () => {},
  joinRunGroup: () => {},
  leaveRunGroup: () => {},
  joinOperationsGroup: () => {},
  leaveOperationsGroup: () => {},
};

function renderPage() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });

  return render(
    <QueryClientProvider client={queryClient}>
      <RealtimeContext.Provider value={REALTIME_VALUE}>
        <MemoryRouter initialEntries={['/platform/operations']}>
          <PlatformOperationsPage />
        </MemoryRouter>
      </RealtimeContext.Provider>
    </QueryClientProvider>,
  );
}

describe('PlatformOperationsPage (TASK-OPS-03)', () => {
  afterEach(() => {
    vi.clearAllMocks();
  });

  it('renders component health, workers, and queues for a SuperAdmin', async () => {
    vi.mocked(operationsApi.getOperationsHealth).mockResolvedValue(makeHealth());

    renderPage();

    expect(await screen.findByRole('heading', { name: 'Operations' })).toBeInTheDocument();
    expect(await screen.findByText('Postgres + pgvector')).toBeInTheDocument();
    expect(screen.getByText('worker-1')).toBeInTheDocument();
    expect(screen.getByText('document-processing')).toBeInTheDocument();
  });

  it('does not auto-load the Azure OpenAI check, and runs it only on button click', async () => {
    vi.mocked(operationsApi.getOperationsHealth).mockResolvedValue(makeHealth());
    vi.mocked(operationsApi.getAoaiHealth).mockResolvedValue([
      { deployment: 'gpt-4o', purpose: 'Chat', reachable: true, checkedAt: '2024-01-01T00:00:00Z' },
    ]);

    renderPage();

    const runCheckButton = await screen.findByRole('button', { name: /run deep check/i });
    expect(operationsApi.getAoaiHealth).not.toHaveBeenCalled();

    fireEvent.click(runCheckButton);

    expect(await screen.findByText('gpt-4o')).toBeInTheDocument();
    expect(operationsApi.getAoaiHealth).toHaveBeenCalledTimes(1);
  });

  it('shows an error message when operations health fails to load', async () => {
    vi.mocked(operationsApi.getOperationsHealth).mockRejectedValue(
      new ApiError({ title: 'Server error', status: 500 }),
    );

    renderPage();

    expect(await screen.findByRole('alert')).toHaveTextContent('Server error');
  });
});
