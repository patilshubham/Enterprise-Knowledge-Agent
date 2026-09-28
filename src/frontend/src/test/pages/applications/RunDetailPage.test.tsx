import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import * as runsApi from '@/api/runs';
import type { AgentRunDetailResponse } from '@/api/runs';
import { ApiError } from '@/api/problemDetails';
import RunDetailPage from '@/pages/applications/RunDetailPage';

vi.mock('@/api/runs', async () => {
  const actual = await vi.importActual<typeof import('@/api/runs')>('@/api/runs');
  return {
    ...actual,
    getRunDetail: vi.fn(),
    retryRun: vi.fn(),
    cancelRun: vi.fn(),
  };
});

function makeRun(overrides: Partial<AgentRunDetailResponse> = {}): AgentRunDetailResponse {
  return {
    id: 'run-1',
    applicationId: 'app-1',
    agentId: 'agent-1',
    agentName: 'Contract Summarizer',
    agentPromptSnapshot: 'You are a helpful assistant.',
    knowledgeScopeSnapshotJson: null,
    deploymentSnapshot: 'gpt-4o',
    requestedParametersJson: null,
    effectiveParametersJson: null,
    retrievalSettingsJson: null,
    requestedBy: 'user-1',
    userRequest: 'Summarize the Q1 report.',
    outputTitle: 'Q1 Summary',
    status: 'Completed',
    stage: 'Completed',
    progress: 100,
    workerId: null,
    createdAt: '2024-01-01T00:00:00Z',
    startedAt: '2024-01-01T00:01:00Z',
    heartbeatAt: null,
    completedAt: '2024-01-01T00:02:00Z',
    failureCategory: null,
    failureMessage: null,
    correlationId: 'corr-123',
    cancellationRequested: false,
    retryEligible: false,
    generatedOutputId: 'output-1',
    evidence: [
      {
        id: 'evidence-1',
        sourceId: 'SOURCE-001',
        fileName: 'handbook.pdf',
        headingPath: 'Section 1 > Policy',
        passage: 'The relevant passage.',
        characterStart: 0,
        characterEnd: 20,
        processedAt: '2024-01-01T00:00:00Z',
      },
    ],
    ...overrides,
  };
}

function renderPage() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });

  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={['/applications/app-1/runs/run-1']}>
        <Routes>
          <Route path="/applications/:applicationId/runs/:runId" element={<RunDetailPage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe('RunDetailPage (TASK-RDSH-02)', () => {
  afterEach(() => {
    vi.clearAllMocks();
  });

  it('renders the agent name, request, status and evidence', async () => {
    vi.mocked(runsApi.getRunDetail).mockResolvedValue(makeRun());

    renderPage();

    expect(await screen.findByRole('heading', { name: 'Contract Summarizer' })).toBeInTheDocument();
    expect(screen.getByText('Summarize the Q1 report.')).toBeInTheDocument();
    expect(screen.getByText('SOURCE-001')).toBeInTheDocument();
    expect(screen.getByText('The relevant passage.')).toBeInTheDocument();
  });

  it('shows the agent prompt snapshot', async () => {
    vi.mocked(runsApi.getRunDetail).mockResolvedValue(makeRun());

    renderPage();

    expect(await screen.findByText('You are a helpful assistant.')).toBeInTheDocument();
  });

  it('shows an error state when the detail request fails', async () => {
    vi.mocked(runsApi.getRunDetail).mockRejectedValue(
      new ApiError({ title: 'Server error', status: 500 }),
    );

    renderPage();

    expect(await screen.findByRole('alert')).toHaveTextContent('Server error');
  });

  it('retries a failed run and calls the retry endpoint', async () => {
    vi.mocked(runsApi.getRunDetail).mockResolvedValue(
      makeRun({
        status: 'Failed',
        stage: 'Generating',
        retryEligible: true,
        failureMessage: 'boom',
      }),
    );
    vi.mocked(runsApi.retryRun).mockResolvedValue({
      id: 'run-1',
      status: 'Queued',
      attemptNumber: 2,
    });

    renderPage();

    const retryButton = await screen.findByRole('button', { name: /Retry/ });
    fireEvent.click(retryButton);

    await waitFor(() => expect(runsApi.retryRun).toHaveBeenCalledWith('app-1', 'run-1'));
  });

  it('cancels a running run after confirmation, calling the agent-nested cancel endpoint', async () => {
    vi.mocked(runsApi.getRunDetail).mockResolvedValue(
      makeRun({ status: 'Running', stage: 'Retrieving' }),
    );
    vi.mocked(runsApi.cancelRun).mockResolvedValue(undefined);

    renderPage();

    const cancelButton = await screen.findByRole('button', { name: /Cancel/ });
    fireEvent.click(cancelButton);

    const confirmButton = await screen.findByRole('button', { name: 'Cancel run' });
    fireEvent.click(confirmButton);

    await waitFor(() =>
      expect(runsApi.cancelRun).toHaveBeenCalledWith('app-1', 'agent-1', 'run-1'),
    );
  });

  it('does not show cancel for a completed run', async () => {
    vi.mocked(runsApi.getRunDetail).mockResolvedValue(makeRun({ status: 'Completed' }));

    renderPage();

    await screen.findByRole('heading', { name: 'Contract Summarizer' });
    expect(screen.queryByRole('button', { name: /^Cancel$/ })).not.toBeInTheDocument();
  });
});
