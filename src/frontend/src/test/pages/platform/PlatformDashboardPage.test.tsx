import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import * as dashboardsApi from '@/api/dashboards';
import type { PlatformDashboardResponse } from '@/api/dashboards';
import { ApiError } from '@/api/problemDetails';
import { RealtimeContext, type RealtimeContextValue } from '@/app/realtime/realtime-context';
import PlatformDashboardPage from '@/pages/platform/PlatformDashboardPage';

vi.mock('@/api/dashboards', async () => {
  const actual = await vi.importActual<typeof import('@/api/dashboards')>('@/api/dashboards');
  return { ...actual, getPlatformDashboard: vi.fn() };
});

function makeDashboard(
  overrides: Partial<PlatformDashboardResponse> = {},
): PlatformDashboardResponse {
  return {
    applications: { active: 3, archived: 1, deleted: 0 },
    knowledgeFiles: {
      queued: 1,
      processing: 2,
      ready: 10,
      failed: 0,
      disabled: 0,
      deleting: 0,
      deleted: 0,
    },
    processingJobs: { queued: 1, running: 2, completed: 20, failed: 0, cancelled: 0 },
    agentRuns: { queued: 0, running: 1, completed: 5, failed: 0, cancelled: 0 },
    workers: [
      {
        workerId: 'worker-1',
        workerKind: 'document-worker',
        lastHeartbeatAt: '2024-01-01T00:00:00Z',
        isStale: false,
      },
    ],
    aoaiAvailable: true,
    queues: [
      {
        queueName: 'document-processing',
        approximateMessageCount: 1,
        deadLetterQueueMessageCount: 0,
      },
    ],
    attentionItems: [],
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
        <MemoryRouter initialEntries={['/platform/dashboard']}>
          <PlatformDashboardPage />
        </MemoryRouter>
      </RealtimeContext.Provider>
    </QueryClientProvider>,
  );
}

describe('PlatformDashboardPage (TASK-PDSH-02)', () => {
  afterEach(() => {
    vi.clearAllMocks();
  });

  it('renders aggregated counts for a SuperAdmin', async () => {
    vi.mocked(dashboardsApi.getPlatformDashboard).mockResolvedValue(makeDashboard());

    renderPage();

    expect(await screen.findByRole('heading', { name: 'Dashboard' })).toBeInTheDocument();
    expect(await screen.findByText('Applications')).toBeInTheDocument();
    expect(screen.getByText('worker-1')).toBeInTheDocument();
    expect(screen.getByText('document-processing')).toBeInTheDocument();
    expect(screen.getByText('Online')).toBeInTheDocument();
  });

  it('renders attention items when present', async () => {
    vi.mocked(dashboardsApi.getPlatformDashboard).mockResolvedValue(
      makeDashboard({
        attentionItems: [
          { kind: 'ProcessingJobsFailed', message: '2 processing jobs failed', count: 2 },
        ],
      }),
    );

    renderPage();

    const attentionItem = await screen.findByText('Failed processing jobs');
    expect(
      within(attentionItem.closest('li') ?? attentionItem.parentElement!).getByText('2'),
    ).toBeInTheDocument();
  });

  it('shows an error message when the dashboard fails to load', async () => {
    vi.mocked(dashboardsApi.getPlatformDashboard).mockRejectedValue(
      new ApiError({ title: 'Server error', status: 500 }),
    );

    renderPage();

    expect(await screen.findByRole('alert')).toHaveTextContent('Server error');
  });
});
