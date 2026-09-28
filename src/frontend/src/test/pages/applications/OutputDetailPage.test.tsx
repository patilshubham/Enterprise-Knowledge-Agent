import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import * as outputsApi from '@/api/outputs';
import type { OutputDetailResponse } from '@/api/outputs';
import { ApiError } from '@/api/problemDetails';
import { ThemeContext } from '@/app/theme/theme-context';
import OutputDetailPage from '@/pages/applications/OutputDetailPage';

// Monaco cannot mount in jsdom - mock it with a plain textarea (see AgentDetailPage.test.tsx).
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

vi.mock('@/api/outputs', async () => {
  const actual = await vi.importActual<typeof import('@/api/outputs')>('@/api/outputs');
  return {
    ...actual,
    getOutputDetail: vi.fn(),
    editOutput: vi.fn(),
    regenerateOutput: vi.fn(),
    deleteOutput: vi.fn(),
  };
});

function makeOutput(overrides: Partial<OutputDetailResponse> = {}): OutputDetailResponse {
  return {
    id: 'output-1',
    applicationId: 'app-1',
    agentRunId: 'run-1',
    title: 'Q1 Summary',
    originalMarkdown: 'Approved quickly [SOURCE-001].',
    currentMarkdown: 'Approved quickly [SOURCE-001].',
    isEdited: false,
    createdBy: 'user-1',
    createdAt: '2024-01-01T00:00:00Z',
    lastEditedBy: null,
    lastEditedAt: null,
    agentId: 'agent-1',
    agentName: 'Contract Summarizer',
    deploymentSnapshot: 'gpt-4o',
    requestedParametersJson: null,
    effectiveParametersJson: null,
    retrievalSettingsJson: null,
    citations: [
      {
        id: 'citation-1',
        sourceId: 'SOURCE-001',
        fileName: 'policy.md',
        headingPath: 'Onboarding > Access',
        passage: 'Requests are approved within two business days.',
        characterStart: 0,
        characterEnd: 10,
        processedAt: '2024-01-01T00:00:00Z',
        sourceUnavailable: false,
      },
    ],
    ...overrides,
  };
}

function renderPage() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });

  return render(
    <QueryClientProvider client={queryClient}>
      <ThemeContext.Provider value={{ theme: 'dark', setTheme: vi.fn(), toggleTheme: vi.fn() }}>
        <MemoryRouter initialEntries={['/applications/app-1/outputs/output-1']}>
          <Routes>
            <Route
              path="/applications/:applicationId/outputs/:outputId"
              element={<OutputDetailPage />}
            />
          </Routes>
        </MemoryRouter>
      </ThemeContext.Provider>
    </QueryClientProvider>,
  );
}

describe('OutputDetailPage (TASK-OUT-02/03/04 + TASK-CIT-04)', () => {
  afterEach(() => {
    vi.clearAllMocks();
  });

  it('renders the output title, metadata and Markdown', async () => {
    vi.mocked(outputsApi.getOutputDetail).mockResolvedValue(makeOutput());

    renderPage();

    expect(await screen.findByRole('heading', { name: 'Q1 Summary' })).toBeInTheDocument();
    expect(screen.getByText(/Contract Summarizer/)).toBeInTheDocument();
    expect(screen.getByText(/Approved quickly/)).toBeInTheDocument();
  });

  it('shows edit/regenerate/delete and copy/download actions', async () => {
    vi.mocked(outputsApi.getOutputDetail).mockResolvedValue(makeOutput());

    renderPage();

    await screen.findByRole('heading', { name: 'Q1 Summary' });
    expect(screen.getByRole('button', { name: /^edit$/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /^regenerate$/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /^delete$/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /^copy$/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /download/i })).toBeInTheDocument();
  });

  it('shows an "Edited" badge once the output has been edited', async () => {
    vi.mocked(outputsApi.getOutputDetail).mockResolvedValue(
      makeOutput({ isEdited: true, lastEditedBy: 'user-2', lastEditedAt: '2024-02-01T00:00:00Z' }),
    );

    renderPage();

    await screen.findByRole('heading', { name: 'Q1 Summary' });
    expect(screen.getByText('Edited')).toBeInTheDocument();
    expect(screen.getByText(/Last edited by user-2/)).toBeInTheDocument();
  });

  it('saves an edit via the Monaco-backed editor', async () => {
    vi.mocked(outputsApi.getOutputDetail).mockResolvedValue(makeOutput());
    vi.mocked(outputsApi.editOutput).mockResolvedValue(
      makeOutput({ currentMarkdown: 'Updated content.', isEdited: true }),
    );

    renderPage();

    await screen.findByRole('heading', { name: 'Q1 Summary' });
    fireEvent.click(screen.getByRole('button', { name: /^edit$/i }));

    const editor = await screen.findByLabelText('Edit output Markdown');
    fireEvent.change(editor, { target: { value: 'Updated content.' } });
    fireEvent.click(screen.getByRole('button', { name: /^save$/i }));

    await waitFor(() => {
      expect(outputsApi.editOutput).toHaveBeenCalledWith('app-1', 'output-1', 'Updated content.');
    });
  });

  it('regenerates the output and navigates to the new run', async () => {
    vi.mocked(outputsApi.getOutputDetail).mockResolvedValue(makeOutput());
    vi.mocked(outputsApi.regenerateOutput).mockResolvedValue({ runId: 'run-2' });

    renderPage();

    await screen.findByRole('heading', { name: 'Q1 Summary' });
    fireEvent.click(screen.getByRole('button', { name: /^regenerate$/i }));

    const dialog = await screen.findByRole('dialog');
    fireEvent.click(within(dialog).getByRole('button', { name: /^regenerate$/i }));

    await waitFor(() => {
      expect(outputsApi.regenerateOutput).toHaveBeenCalledWith('app-1', 'output-1');
    });
  });

  it('deletes the output after confirming', async () => {
    vi.mocked(outputsApi.getOutputDetail).mockResolvedValue(makeOutput());
    vi.mocked(outputsApi.deleteOutput).mockResolvedValue(undefined);

    renderPage();

    await screen.findByRole('heading', { name: 'Q1 Summary' });
    fireEvent.click(screen.getByRole('button', { name: /^delete$/i }));

    const dialog = await screen.findByRole('dialog');
    fireEvent.click(within(dialog).getByRole('button', { name: /^delete$/i }));

    await waitFor(() => {
      expect(outputsApi.deleteOutput).toHaveBeenCalledWith('app-1', 'output-1');
    });
  });

  it('shows a not-found message for a deleted/cross-app output', async () => {
    vi.mocked(outputsApi.getOutputDetail).mockRejectedValue(
      new ApiError({ title: 'Not Found', status: 404 }),
    );

    renderPage();

    expect(await screen.findByRole('alert')).toHaveTextContent(/don't have access/i);
  });
});
