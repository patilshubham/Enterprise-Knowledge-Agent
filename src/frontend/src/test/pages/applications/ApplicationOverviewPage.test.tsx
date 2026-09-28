import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import * as applicationsApi from '@/api/applications';
import type { ApplicationResponse } from '@/api/applications';
import * as dashboardsApi from '@/api/dashboards';
import type { ApplicationDashboardResponse } from '@/api/dashboards';
import { ApiError } from '@/api/problemDetails';
import ApplicationOverviewPage from '@/pages/applications/ApplicationOverviewPage';

vi.mock('@/api/applications', async () => {
  const actual = await vi.importActual<typeof import('@/api/applications')>('@/api/applications');
  return {
    ...actual,
    getApplication: vi.fn(),
    updateApplication: vi.fn(),
    archiveApplication: vi.fn(),
    restoreApplication: vi.fn(),
    deleteApplication: vi.fn(),
  };
});

vi.mock('@/api/dashboards', async () => {
  const actual = await vi.importActual<typeof import('@/api/dashboards')>('@/api/dashboards');
  return { ...actual, getApplicationDashboard: vi.fn() };
});

function makeApplication(overrides: Partial<ApplicationResponse> = {}): ApplicationResponse {
  return {
    id: 'app-1',
    name: 'Contract Review',
    description: 'Reviews vendor contracts',
    status: 'Active',
    allowViewerExecution: false,
    createdBy: 'user-1',
    createdAt: '2024-01-01T00:00:00Z',
    updatedAt: null,
    archivedAt: null,
    deletedAt: null,
    ...overrides,
  };
}

function makeDashboard(
  overrides: Partial<ApplicationDashboardResponse> = {},
): ApplicationDashboardResponse {
  return {
    applicationId: 'app-1',
    applicationName: 'Contract Review',
    knowledgeFiles: {
      queued: 0,
      processing: 0,
      ready: 4,
      failed: 0,
      disabled: 0,
      deleting: 0,
      deleted: 0,
    },
    processingJobs: { queued: 0, running: 0, completed: 4, failed: 0, cancelled: 0 },
    agentRuns: { queued: 0, running: 0, completed: 2, failed: 0, cancelled: 0 },
    activeAgents: 1,
    totalAgents: 2,
    generatedOutputs: 3,
    attentionItems: [],
    ...overrides,
  };
}

function renderPage() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });

  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={['/applications/app-1/overview']}>
        <Routes>
          <Route
            path="/applications/:applicationId/overview"
            element={<ApplicationOverviewPage />}
          />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe('ApplicationOverviewPage (TASK-APP-03)', () => {
  beforeEach(() => {
    vi.mocked(dashboardsApi.getApplicationDashboard).mockResolvedValue(makeDashboard());
  });

  afterEach(() => {
    vi.clearAllMocks();
  });

  it('renders application details for a SuperAdmin, including manage actions', async () => {
    vi.mocked(applicationsApi.getApplication).mockResolvedValue(makeApplication());

    renderPage();

    expect(await screen.findByRole('heading', { name: 'Contract Review' })).toBeInTheDocument();
    expect(screen.getByText('Reviews vendor contracts')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /edit/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /archive/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /delete/i })).toBeInTheDocument();
  });

  it('shows the Activity quick link for a SuperAdmin (canViewAudit)', async () => {
    vi.mocked(applicationsApi.getApplication).mockResolvedValue(makeApplication());

    renderPage();

    await screen.findByRole('heading', { name: 'Contract Review' });
    expect(screen.getByRole('link', { name: 'Activity' })).toBeInTheDocument();
  });

  it('shows a not-found message on a 404 response', async () => {
    vi.mocked(applicationsApi.getApplication).mockRejectedValue(
      new ApiError({ title: 'Not Found', status: 404 }),
    );

    renderPage();

    expect(await screen.findByRole('alert')).toHaveTextContent(/don't have access/i);
  });

  it('archives the application after confirming', async () => {
    vi.mocked(applicationsApi.getApplication).mockResolvedValue(makeApplication());
    vi.mocked(applicationsApi.archiveApplication).mockResolvedValue(
      makeApplication({ status: 'Archived' }),
    );

    renderPage();

    await screen.findByRole('heading', { name: 'Contract Review' });
    fireEvent.click(screen.getByRole('button', { name: /^archive$/i }));
    const dialog = await screen.findByRole('dialog');
    fireEvent.click(within(dialog).getByRole('button', { name: 'Archive' }));

    await waitFor(() => {
      expect(applicationsApi.archiveApplication).toHaveBeenCalledWith('app-1');
    });
  });

  it('renders the application dashboard counts', async () => {
    vi.mocked(applicationsApi.getApplication).mockResolvedValue(makeApplication());
    vi.mocked(dashboardsApi.getApplicationDashboard).mockResolvedValue(
      makeDashboard({ activeAgents: 1, totalAgents: 2, generatedOutputs: 3 }),
    );

    renderPage();

    await screen.findByRole('heading', { name: 'Contract Review' });
    expect(await screen.findByText('Agents & outputs')).toBeInTheDocument();
    expect(screen.getByText('Active agents')).toBeInTheDocument();
    expect(dashboardsApi.getApplicationDashboard).toHaveBeenCalledWith('app-1');
  });

  it('renders dashboard attention items when present', async () => {
    vi.mocked(applicationsApi.getApplication).mockResolvedValue(makeApplication());
    vi.mocked(dashboardsApi.getApplicationDashboard).mockResolvedValue(
      makeDashboard({
        attentionItems: [{ kind: 'AgentRunsFailed', message: '1 agent run failed', count: 1 }],
      }),
    );

    renderPage();

    expect(await screen.findByText('Failed agent runs')).toBeInTheDocument();
  });
});
