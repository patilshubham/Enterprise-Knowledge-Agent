import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen, within } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import * as processingJobsApi from '@/api/processingJobs';
import type {
  ProcessingJobDetailResponse,
  ProcessingJobSummaryResponse,
} from '@/api/processingJobs';
import { ApiError } from '@/api/problemDetails';
import ApplicationProcessingPage from '@/pages/applications/ApplicationProcessingPage';

vi.mock('@/api/processingJobs', async () => {
  const actual =
    await vi.importActual<typeof import('@/api/processingJobs')>('@/api/processingJobs');
  return {
    ...actual,
    listApplicationProcessing: vi.fn(),
    getApplicationProcessingDetail: vi.fn(),
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
      <MemoryRouter initialEntries={['/applications/app-1/processing']}>
        <Routes>
          <Route
            path="/applications/:applicationId/processing"
            element={<ApplicationProcessingPage />}
          />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe('ApplicationProcessingPage (TASK-PRDSH-02)', () => {
  afterEach(() => {
    vi.clearAllMocks();
  });

  it('lists processing jobs scoped to the application', async () => {
    vi.mocked(processingJobsApi.listApplicationProcessing).mockResolvedValue([makeJob()]);

    renderPage();

    const table = within(await screen.findByRole('table'));
    expect(await table.findByText('handbook.pdf')).toBeInTheDocument();
    expect(processingJobsApi.listApplicationProcessing).toHaveBeenCalledWith(
      'app-1',
      expect.objectContaining({ status: undefined }),
    );
  });

  it('shows an error state when the list request fails', async () => {
    vi.mocked(processingJobsApi.listApplicationProcessing).mockRejectedValue(
      new ApiError({ title: 'Server error', status: 500 }),
    );

    renderPage();

    expect(await screen.findByRole('alert')).toHaveTextContent('Server error');
  });

  it('opens the detail drawer with the job stage timeline', async () => {
    vi.mocked(processingJobsApi.listApplicationProcessing).mockResolvedValue([makeJob()]);
    vi.mocked(processingJobsApi.getApplicationProcessingDetail).mockResolvedValue(makeDetail());

    renderPage();

    const table = within(await screen.findByRole('table'));
    const row = await table.findByText('handbook.pdf');
    fireEvent.click(row);

    expect(await screen.findByText('corr-123')).toBeInTheDocument();
    expect(processingJobsApi.getApplicationProcessingDetail).toHaveBeenCalledWith('app-1', 'job-1');
  });
});
