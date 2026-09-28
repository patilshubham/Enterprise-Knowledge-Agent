import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import * as agentsApi from '@/api/agents';
import type { AgentResponse } from '@/api/agents';
import * as deploymentsApi from '@/api/deployments';
import type { DeploymentInfo } from '@/api/deployments';
import { ApiError } from '@/api/problemDetails';
import ApplicationAgentsPage from '@/pages/applications/ApplicationAgentsPage';

vi.mock('@/api/agents', async () => {
  const actual = await vi.importActual<typeof import('@/api/agents')>('@/api/agents');
  return {
    ...actual,
    listAgents: vi.fn(),
    createAgent: vi.fn(),
    enableAgent: vi.fn(),
    disableAgent: vi.fn(),
    deleteAgent: vi.fn(),
  };
});

vi.mock('@/api/deployments', async () => {
  const actual = await vi.importActual<typeof import('@/api/deployments')>('@/api/deployments');
  return {
    ...actual,
    listDeployments: vi.fn(),
  };
});

function makeAgent(overrides: Partial<AgentResponse> = {}): AgentResponse {
  return {
    id: 'agent-1',
    applicationId: 'app-1',
    name: 'Contract Summarizer',
    description: 'Summarizes contracts',
    prompt: 'You are a helpful assistant.',
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

function makeDeployment(overrides: Partial<DeploymentInfo> = {}): DeploymentInfo {
  return {
    name: 'gpt-4o',
    purpose: 0,
    maxContextTokens: 128000,
    maxOutputTokens: 16384,
    embeddingDimensions: null,
    supportsTopP: true,
    ...overrides,
  };
}

function renderPage() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });

  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={['/applications/app-1/agents']}>
        <Routes>
          <Route path="/applications/:applicationId/agents" element={<ApplicationAgentsPage />} />
          <Route
            path="/applications/:applicationId/agents/:agentId"
            element={<div>Agent detail page</div>}
          />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe('ApplicationAgentsPage (TASK-AGT-04)', () => {
  afterEach(() => {
    vi.clearAllMocks();
  });

  it('renders agents for a SuperAdmin, including the create action', async () => {
    vi.mocked(agentsApi.listAgents).mockResolvedValue([makeAgent()]);
    vi.mocked(deploymentsApi.listDeployments).mockResolvedValue([makeDeployment()]);

    renderPage();

    const table = within(await screen.findByRole('table'));
    expect(await table.findByText('Contract Summarizer')).toBeInTheDocument();
    expect(table.getByText('Active')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /create agent/i })).toBeInTheDocument();
  });

  it('shows an error message when the list fails to load', async () => {
    vi.mocked(agentsApi.listAgents).mockRejectedValue(
      new ApiError({ title: 'Server error', status: 500 }),
    );
    vi.mocked(deploymentsApi.listDeployments).mockResolvedValue([makeDeployment()]);

    renderPage();

    expect(await screen.findByRole('alert')).toHaveTextContent(/server error/i);
  });

  it('creates an agent and navigates to its editor', async () => {
    vi.mocked(agentsApi.listAgents).mockResolvedValue([]);
    vi.mocked(deploymentsApi.listDeployments).mockResolvedValue([makeDeployment()]);
    vi.mocked(agentsApi.createAgent).mockResolvedValue(makeAgent({ id: 'agent-new' }));

    renderPage();

    fireEvent.click(await screen.findByRole('button', { name: /create agent/i }));
    fireEvent.change(await screen.findByLabelText(/name/i), {
      target: { value: 'New Agent' },
    });
    fireEvent.click(screen.getByRole('button', { name: /^create$/i }));

    await waitFor(() => {
      expect(agentsApi.createAgent).toHaveBeenCalledWith(
        'app-1',
        expect.objectContaining({ name: 'New Agent', generationDeployment: 'gpt-4o' }),
      );
    });
    expect(await screen.findByText('Agent detail page')).toBeInTheDocument();
  });
});
