import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, within } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import * as agentsApi from '@/api/agents';
import type { AgentResponse } from '@/api/agents';
import * as outputsApi from '@/api/outputs';
import type { OutputSummaryResponse } from '@/api/outputs';
import { ApiError } from '@/api/problemDetails';
import ApplicationOutputsPage from '@/pages/applications/ApplicationOutputsPage';

vi.mock('@/api/outputs', async () => {
  const actual = await vi.importActual<typeof import('@/api/outputs')>('@/api/outputs');
  return {
    ...actual,
    listOutputs: vi.fn(),
  };
});

vi.mock('@/api/agents', async () => {
  const actual = await vi.importActual<typeof import('@/api/agents')>('@/api/agents');
  return {
    ...actual,
    listAgents: vi.fn(),
  };
});

function makeOutput(overrides: Partial<OutputSummaryResponse> = {}): OutputSummaryResponse {
  return {
    id: 'output-1',
    title: 'Q1 Summary',
    agentId: 'agent-1',
    agentName: 'Contract Summarizer',
    createdBy: 'user-1',
    createdAt: '2024-01-01T00:00:00Z',
    isEdited: false,
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
      <MemoryRouter initialEntries={['/applications/app-1/outputs']}>
        <Routes>
          <Route
            path="/applications/:applicationId/outputs"
            element={<ApplicationOutputsPage />}
          />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe('ApplicationOutputsPage (TASK-OUT-05)', () => {
  afterEach(() => {
    vi.clearAllMocks();
  });

  it('lists outputs with agent name, creator and edited badge', async () => {
    vi.mocked(outputsApi.listOutputs).mockResolvedValue([
      makeOutput(),
      makeOutput({ id: 'output-2', title: 'Q2 Summary', isEdited: true }),
    ]);
    vi.mocked(agentsApi.listAgents).mockResolvedValue([makeAgent()]);

    renderPage();

    const table = within(await screen.findByRole('table'));
    expect(await table.findByText('Q1 Summary')).toBeInTheDocument();
    expect(table.getByText('Q2 Summary')).toBeInTheDocument();
    expect(table.getAllByText('Contract Summarizer')).toHaveLength(2);
    expect(table.getByText('Edited', { selector: 'span' })).toBeInTheDocument();
  });

  it('shows an empty state when there are no outputs', async () => {
    vi.mocked(outputsApi.listOutputs).mockResolvedValue([]);
    vi.mocked(agentsApi.listAgents).mockResolvedValue([]);

    renderPage();

    const table = within(await screen.findByRole('table'));
    expect(await table.findByText('No outputs to show.')).toBeInTheDocument();
  });

  it('shows an error state when the list request fails', async () => {
    vi.mocked(outputsApi.listOutputs).mockRejectedValue(
      new ApiError({ title: 'Server error', status: 500 }),
    );
    vi.mocked(agentsApi.listAgents).mockResolvedValue([]);

    renderPage();

    expect(await screen.findByRole('alert')).toHaveTextContent('Server error');
  });

  it('re-queries the list when the agent filter changes', async () => {
    vi.mocked(outputsApi.listOutputs).mockResolvedValue([makeOutput()]);
    vi.mocked(agentsApi.listAgents).mockResolvedValue([makeAgent()]);

    renderPage();

    await screen.findByRole('table');
    expect(outputsApi.listOutputs).toHaveBeenCalledWith(
      'app-1',
      expect.objectContaining({ agentId: undefined, status: undefined }),
    );
  });
});
