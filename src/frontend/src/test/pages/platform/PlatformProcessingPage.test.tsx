import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import * as processingJobsApi from '@/api/processingJobs';
import type {
  ProcessingJobDetailResponse,
  ProcessingJobSummaryResponse,
} from '@/api/processingJobs';
import { ApiError } from '@/api/problemDetails';
import PlatformProcessingPage from '@/pages/platform/PlatformProcessingPage';

vi.mock('@/api/processingJobs', async () => {
  const actual =
    await vi.importActual<typeof import('@/api/processingJobs')>('@/api/processingJobs');
  return {
    ...actual,
    listPlatformProcessing: vi.fn(),
    getPlatformProcessingDetail: vi.fn(),
    retryProcessingJob: vi.fn(),
  };
});

function makeJob(
  overrides: Partial<ProcessingJobSummaryResponse> = {},
): ProcessingJobSummaryResponse {
  return {
    id: 'job-1',
    applicationId: 'app-1',
    fileId: 'file-1',
    fileName: 'handbook.pdf',
    status: 'Running',
    stage: 'Chunking',
    progress: 40,
    attemptNumber: 1,
    createdAt: '2024-01-01T00:00:00Z',
    startedAt: '2024-01-01T00:01:00Z',
    completedAt: null,
    failureCategory: null,
    failureMessage: null,
    retryEligible: false,
    ...overrides,
  };
}

function makeDetail(
  overrides: Partial<ProcessingJobDetailResponse> = {},
): ProcessingJobDetailResponse {
  return {
    ...makeJob(),
    workerId: 'worker-1',
    heartbeatAt: '2024-01-01T00:02:00Z',
    correlationId: 'corr-123',
    ...overrides,
  };
}

function renderPage() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });

  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={['/platform/processing']}>
        <Routes>
          <Route path="/platform/processing" element={<PlatformProcessingPage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe('PlatformProcessingPage (TASK-PRDSH-01)', () => {
  afterEach(() => {
    vi.clearAllMocks();
  });

  it('lists processing jobs across applications with status and stage', async () => {
    vi.mocked(processingJobsApi.listPlatformProcessing).mockResolvedValue([
      makeJob(),
      makeJob({ id: 'job-2', fileName: 'contract.pdf', status: 'Completed', stage: 'Completed' }),
    ]);

    renderPage();

    const table = within(await screen.findByRole('table'));
    expect(await table.findByText('handbook.pdf')).toBeInTheDocument();
    expect(table.getByText('contract.pdf')).toBeInTheDocument();
  });

  it('shows an empty state when there are no jobs', async () => {
    vi.mocked(processingJobsApi.listPlatformProcessing).mockResolvedValue([]);

    renderPage();

    const table = within(await screen.findByRole('table'));
    expect(await table.findByText('No processing jobs to show.')).toBeInTheDocument();
  });

  it('shows an error state when the list request fails', async () => {
    vi.mocked(processingJobsApi.listPlatformProcessing).mockRejectedValue(
      new ApiError({ title: 'Server error', status: 500 }),
    );

    renderPage();

    expect(await screen.findByRole('alert')).toHaveTextContent('Server error');
  });

  it('opens the detail drawer on row click and shows the correlation id and retry action', async () => {
    vi.mocked(processingJobsApi.listPlatformProcessing).mockResolvedValue([
      makeJob({ status: 'Failed', stage: 'Parsing', retryEligible: true }),
    ]);
    vi.mocked(processingJobsApi.getPlatformProcessingDetail).mockResolvedValue(
      makeDetail({
        status: 'Failed',
        stage: 'Parsing',
        retryEligible: true,
        failureMessage: 'boom',
      }),
    );
    vi.mocked(processingJobsApi.retryProcessingJob).mockResolvedValue({
      id: 'job-1',
      status: 'Queued',
      attemptNumber: 2,
    });

    renderPage();

    const table = within(await screen.findByRole('table'));
    const row = await table.findByText('handbook.pdf');
    fireEvent.click(row);

    expect(await screen.findByText('corr-123')).toBeInTheDocument();
    expect(screen.getByText('boom')).toBeInTheDocument();

    const retryButton = screen.getByRole('button', { name: 'Retry' });
    fireEvent.click(retryButton);

    await waitFor(() =>
      expect(processingJobsApi.retryProcessingJob).toHaveBeenCalledWith('app-1', 'job-1'),
    );
  });
});
