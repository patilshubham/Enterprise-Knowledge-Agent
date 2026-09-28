import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import * as agentsApi from '@/api/agents';
import type { AgentResponse } from '@/api/agents';
import * as deploymentsApi from '@/api/deployments';
import type { DeploymentInfo } from '@/api/deployments';
import * as knowledgeFilesApi from '@/api/knowledgeFiles';
import type { KnowledgeFileResponse } from '@/api/knowledgeFiles';
import { ApiError } from '@/api/problemDetails';
import { ThemeContext } from '@/app/theme/theme-context';
import AgentDetailPage from '@/pages/applications/AgentDetailPage';

// Monaco cannot mount in jsdom (see CodeEditor.test.tsx) - mock it with a
// plain textarea that mirrors the same value/onChange/options contract.
vi.mock('monaco-editor', () => ({}));
vi.mock('@monaco-editor/react', () => ({
  loader: { config: vi.fn() },
  default: ({
    value,
    onChange,
    options,
  }: {
    value: string;
    onChange: (value: string | undefined) => void;
    options: { readOnly?: boolean; ariaLabel?: string };
  }) => (
    <textarea
      aria-label={options.ariaLabel}
      readOnly={options.readOnly}
      value={value}
      onChange={(event) => onChange(event.target.value)}
    />
  ),
}));

vi.mock('@/api/agents', async () => {
  const actual = await vi.importActual<typeof import('@/api/agents')>('@/api/agents');
  return {
    ...actual,
    getAgent: vi.fn(),
    updateAgent: vi.fn(),
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

vi.mock('@/api/knowledgeFiles', async () => {
  const actual =
    await vi.importActual<typeof import('@/api/knowledgeFiles')>('@/api/knowledgeFiles');
  return {
    ...actual,
    listKnowledgeFiles: vi.fn(),
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

function makeFile(overrides: Partial<KnowledgeFileResponse> = {}): KnowledgeFileResponse {
  return {
    id: 'file-1',
    applicationId: 'app-1',
    displayName: 'Master Services Agreement.pdf',
    fileSize: 1024,
    mimeType: 'application/pdf',
    encoding: 'utf-8',
    status: 2,
    isEnabled: true,
    uploadedBy: 'user-1',
    uploadedAt: '2024-01-01T00:00:00Z',
    lastProcessedAt: '2024-01-01T00:05:00Z',
    stage: null,
    progress: null,
    ...overrides,
  };
}

function renderPage() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });

  return render(
    <QueryClientProvider client={queryClient}>
      <ThemeContext.Provider value={{ theme: 'dark', setTheme: vi.fn(), toggleTheme: vi.fn() }}>
        <MemoryRouter initialEntries={['/applications/app-1/agents/agent-1']}>
          <Routes>
            <Route
              path="/applications/:applicationId/agents/:agentId"
              element={<AgentDetailPage />}
            />
          </Routes>
        </MemoryRouter>
      </ThemeContext.Provider>
    </QueryClientProvider>,
  );
}

describe('AgentDetailPage (TASK-AGT-04)', () => {
  afterEach(() => {
    vi.clearAllMocks();
  });

  it('shows the prompt, char/token counts and a Save action for a SuperAdmin', async () => {
    vi.mocked(agentsApi.getAgent).mockResolvedValue(makeAgent());
    vi.mocked(deploymentsApi.listDeployments).mockResolvedValue([makeDeployment()]);

    renderPage();

    const editor = await screen.findByLabelText('Agent prompt');
    expect(editor).toHaveValue('You are a helpful assistant.');
    expect(screen.getByText(/28 characters/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /^save$/i })).toBeInTheDocument();
  });

  it('warns when the prompt likely exceeds the selected deployment context limit', async () => {
    vi.mocked(agentsApi.getAgent).mockResolvedValue(makeAgent());
    vi.mocked(deploymentsApi.listDeployments).mockResolvedValue([
      makeDeployment({ maxContextTokens: 5 }),
    ]);

    renderPage();

    expect(await screen.findByRole('alert')).toHaveTextContent(/exceeds/i);
  });

  it('setting a preset updates the temperature field', async () => {
    vi.mocked(agentsApi.getAgent).mockResolvedValue(makeAgent({ temperature: 0.7 }));
    vi.mocked(deploymentsApi.listDeployments).mockResolvedValue([makeDeployment()]);

    renderPage();

    await screen.findByLabelText('Agent prompt');
    fireEvent.click(screen.getByRole('button', { name: 'Precise' }));

    expect(screen.getByLabelText(/temperature/i)).toHaveValue(0.2);
  });

  it('only lists ready and enabled files when selecting a specific knowledge scope', async () => {
    vi.mocked(agentsApi.getAgent).mockResolvedValue(makeAgent());
    vi.mocked(deploymentsApi.listDeployments).mockResolvedValue([makeDeployment()]);
    vi.mocked(knowledgeFilesApi.listKnowledgeFiles).mockResolvedValue([
      makeFile({
        id: 'file-ready',
        displayName: 'Ready And Enabled.pdf',
        status: 2,
        isEnabled: true,
      }),
      makeFile({
        id: 'file-disabled',
        displayName: 'Ready But Disabled.pdf',
        status: 2,
        isEnabled: false,
      }),
      makeFile({
        id: 'file-processing',
        displayName: 'Still Processing.pdf',
        status: 1,
        isEnabled: true,
      }),
    ]);

    renderPage();

    await screen.findByLabelText('Agent prompt');
    fireEvent.click(screen.getByRole('radio', { name: /selected files only/i }));

    expect(await screen.findByText('Ready And Enabled.pdf')).toBeInTheDocument();
    expect(screen.queryByText('Ready But Disabled.pdf')).not.toBeInTheDocument();
    expect(screen.queryByText('Still Processing.pdf')).not.toBeInTheDocument();
  });

  it('saves the agent with edited values', async () => {
    vi.mocked(agentsApi.getAgent).mockResolvedValue(makeAgent());
    vi.mocked(deploymentsApi.listDeployments).mockResolvedValue([makeDeployment()]);
    vi.mocked(agentsApi.updateAgent).mockResolvedValue(makeAgent({ name: 'Renamed Agent' }));

    renderPage();

    const nameInput = await screen.findByLabelText(/^name$/i);
    fireEvent.change(nameInput, { target: { value: 'Renamed Agent' } });
    fireEvent.click(screen.getByRole('button', { name: /^save$/i }));

    await waitFor(() => {
      expect(agentsApi.updateAgent).toHaveBeenCalledWith(
        'app-1',
        'agent-1',
        expect.objectContaining({ name: 'Renamed Agent' }),
      );
    });
  });

  it('shows a not-found message when the agent does not exist', async () => {
    vi.mocked(agentsApi.getAgent).mockRejectedValue(
      new ApiError({ title: 'Not Found', status: 404 }),
    );
    vi.mocked(deploymentsApi.listDeployments).mockResolvedValue([makeDeployment()]);

    renderPage();

    expect(await screen.findByRole('alert')).toHaveTextContent(/don't have access/i);
  });
});
