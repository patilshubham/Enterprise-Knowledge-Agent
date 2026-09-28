import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import * as applicationsApi from '@/api/applications';
import type { ApplicationResponse } from '@/api/applications';
import { ApiError } from '@/api/problemDetails';
import PlatformApplicationsPage from '@/pages/platform/PlatformApplicationsPage';

vi.mock('@/api/applications', async () => {
  const actual = await vi.importActual<typeof import('@/api/applications')>('@/api/applications');
  return {
    ...actual,
    listApplications: vi.fn(),
    createApplication: vi.fn(),
    updateApplication: vi.fn(),
    archiveApplication: vi.fn(),
    restoreApplication: vi.fn(),
    deleteApplication: vi.fn(),
  };
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

function renderPage() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });

  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={['/platform/applications']}>
        <PlatformApplicationsPage />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe('PlatformApplicationsPage (TASK-APP-03)', () => {
  afterEach(() => {
    vi.clearAllMocks();
  });

  it('lists applications returned by the API for a SuperAdmin', async () => {
    vi.mocked(applicationsApi.listApplications).mockResolvedValue([makeApplication()]);

    renderPage();

    // DataTable renders both a desktop `<table>` and a CSS-hidden mobile
    // card fallback with the same content (see DataTable.test.tsx) - scope
    // to the desktop table to avoid "multiple elements" ambiguity.
    const table = within(await screen.findByRole('table'));
    expect(await table.findByText('Contract Review')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /new application/i })).toBeInTheDocument();
  });

  it('shows an empty state when there are no applications', async () => {
    vi.mocked(applicationsApi.listApplications).mockResolvedValue([]);

    renderPage();

    const table = within(await screen.findByRole('table'));
    expect(await table.findByText(/create one to get started/i)).toBeInTheDocument();
  });

  it('shows an error state when the list request fails', async () => {
    vi.mocked(applicationsApi.listApplications).mockRejectedValue(
      new ApiError({ title: 'Server error', status: 500 }),
    );

    renderPage();

    expect(await screen.findByRole('alert')).toHaveTextContent('Server error');
  });

  it('creates an application via the create dialog', async () => {
    vi.mocked(applicationsApi.listApplications).mockResolvedValue([]);
    vi.mocked(applicationsApi.createApplication).mockResolvedValue(makeApplication());

    renderPage();

    const table = within(await screen.findByRole('table'));
    await table.findByText(/create one to get started/i);
    fireEvent.click(screen.getByRole('button', { name: /new application/i }));
    fireEvent.change(screen.getByLabelText('Name'), { target: { value: 'Contract Review' } });
    fireEvent.click(screen.getByRole('button', { name: 'Create' }));

    await waitFor(() => {
      expect(applicationsApi.createApplication).toHaveBeenCalledWith(
        expect.objectContaining({ name: 'Contract Review' }),
        expect.anything(),
      );
    });
  });

  it('archives an application after confirming', async () => {
    vi.mocked(applicationsApi.listApplications).mockResolvedValue([makeApplication()]);
    vi.mocked(applicationsApi.archiveApplication).mockResolvedValue(
      makeApplication({ status: 'Archived' }),
    );

    renderPage();

    const table = within(await screen.findByRole('table'));
    await table.findByText('Contract Review');
    const trigger = table.getByLabelText('Row actions');
    // Radix's dropdown trigger opens on `pointerdown`, not `click` - jsdom's
    // fireEvent.click alone never opens it.
    fireEvent.pointerDown(trigger, { pointerId: 1, button: 0, ctrlKey: false });
    fireEvent.click(trigger);
    fireEvent.click(await screen.findByRole('menuitem', { name: /archive/i }));
    const dialog = await screen.findByRole('dialog');
    fireEvent.click(within(dialog).getByRole('button', { name: 'Archive' }));

    await waitFor(() => {
      expect(applicationsApi.archiveApplication).toHaveBeenCalledWith('app-1', expect.anything());
    });
  });
});
