import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, within } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import * as agentsApi from '@/api/agents';
import type { AgentResponse } from '@/api/agents';
import * as runsApi from '@/api/runs';
import type { AgentRunSummaryResponse } from '@/api/runs';
import { ApiError } from '@/api/problemDetails';
import ApplicationRunsPage from '@/pages/applications/ApplicationRunsPage';

vi.mock('@/api/runs', async () => {
  const actual = await vi.importActual<typeof import('@/api/runs')>('@/api/runs');
  return {
    ...actual,
    listRuns: vi.fn(),
  };
});

vi.mock('@/api/agents', async () => {
  const actual = await vi.importActual<typeof import('@/api/agents')>('@/api/agents');
  return {
    ...actual,
    listAgents: vi.fn(),
  };
});

function makeRun(overrides: Partial<AgentRunSummaryResponse> = {}): AgentRunSummaryResponse {
  return {
    id: 'run-1',
    applicationId: 'app-1',
    agentId: 'agent-1',
    agentName: 'Contract Summarizer',
    requestedBy: 'user-1',
    status: 'Completed',
    stage: 'Completed',
    progress: 100,
    createdAt: '2024-01-01T00:00:00Z',
    startedAt: '2024-01-01T00:01:00Z',
    completedAt: '2024-01-01T00:02:00Z',
    failureCategory: null,
    failureMessage: null,
    retryEligible: false,
    ...overrides,
  };
}

function makeAgent(overrides: Partial<AgentResponse> = {}): AgentResponse {
  return {
    id: 'agent-1',
    applicationId: 'app-1',
    name: 'Contract Summarizer',
    description: null,
    prompt: 'Summarize.',
    status: 'Active',
    knowledgeScopeType: 'AllReadyEnabled',
    selectedFileIds: [],
    generationDeployment: 'gpt-4o',
    temperature: 0.7,
    maximumOutputTokens: 2048,
    topP: null,
    retrievalMode: 'Hybrid',
    retrievalCount: 8,
    minimumSimilarity: 0.7,
    maximumEvidenceTokens: 4000,
    includeNeighbors: true,
    strictGrounding: true,
    citationsRequired: true,
    createdBy: 'user-1',
    createdAt: '2024-01-01T00:00:00Z',
    updatedBy: null,
    updatedAt: null,
    concurrencyToken: 1,
    ...overrides,
  };
}

function renderPage() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });

  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={['/applications/app-1/runs']}>
        <Routes>
          <Route path="/applications/:applicationId/runs" element={<ApplicationRunsPage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe('ApplicationRunsPage (TASK-RDSH-01)', () => {
  afterEach(() => {
    vi.clearAllMocks();
  });

  it('lists agent runs with agent name, status and requester', async () => {
    vi.mocked(runsApi.listRuns).mockResolvedValue([
      makeRun(),
      makeRun({ id: 'run-2', requestedBy: 'user-2', status: 'Failed', stage: 'Generating' }),
    ]);
    vi.mocked(agentsApi.listAgents).mockResolvedValue([makeAgent()]);

    renderPage();

    const table = within(await screen.findByRole('table'));
    expect(await table.findByText('user-1')).toBeInTheDocument();
    expect(table.getByText('user-2')).toBeInTheDocument();
    expect(table.getAllByText('Contract Summarizer')).toHaveLength(2);
  });

  it('shows an empty state when there are no runs', async () => {
    vi.mocked(runsApi.listRuns).mockResolvedValue([]);
    vi.mocked(agentsApi.listAgents).mockResolvedValue([]);

    renderPage();

    const table = within(await screen.findByRole('table'));
    expect(await table.findByText('No agent runs to show.')).toBeInTheDocument();
  });

  it('shows an error state when the list request fails', async () => {
    vi.mocked(runsApi.listRuns).mockRejectedValue(
      new ApiError({ title: 'Server error', status: 500 }),
    );
    vi.mocked(agentsApi.listAgents).mockResolvedValue([]);

    renderPage();

    expect(await screen.findByRole('alert')).toHaveTextContent('Server error');
  });

  it('re-queries the list when the status filter changes', async () => {
    vi.mocked(runsApi.listRuns).mockResolvedValue([makeRun()]);
    vi.mocked(agentsApi.listAgents).mockResolvedValue([makeAgent()]);

    renderPage();

    await screen.findByRole('table');
    expect(runsApi.listRuns).toHaveBeenCalledWith(
      'app-1',
      expect.objectContaining({ agentId: undefined, status: undefined }),
    );
  });
});
