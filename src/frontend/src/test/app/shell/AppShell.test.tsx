import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, within } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import * as healthApi from '@/api/health';
import { AppShell } from '@/app/shell/AppShell';
import { ThemeProvider } from '@/app/theme/ThemeProvider';

vi.mock('@/api/health', () => ({ fetchReadiness: vi.fn() }));

function renderShell(initialPath = '/platform/dashboard') {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });

  return render(
    <QueryClientProvider client={queryClient}>
      <ThemeProvider>
        <MemoryRouter initialEntries={[initialPath]}>
          <Routes>
            <Route element={<AppShell />}>
              <Route path="/platform/dashboard" element={<div>Dashboard content</div>} />
              <Route
                path="/applications/:applicationId/overview"
                element={<div>Overview content</div>}
              />
            </Route>
          </Routes>
        </MemoryRouter>
      </ThemeProvider>
    </QueryClientProvider>,
  );
}

describe('AppShell nav visibility (TASK-UX-04)', () => {
  beforeEach(() => {
    vi.mocked(healthApi.fetchReadiness).mockResolvedValue('healthy');
  });

  it('shows every platform destination - there is no login/user management in this app', async () => {
    renderShell();

    expect(await screen.findByText('Dashboard content')).toBeInTheDocument();
    const nav = screen.getByRole('navigation', { name: 'Primary' });
    expect(within(nav).getByText('Processing')).toBeInTheDocument();
    expect(within(nav).getByText('Audit')).toBeInTheDocument();
    expect(within(nav).getByText('Operations')).toBeInTheDocument();
  });

  it('shows the current-application nav when viewing an application', async () => {
    renderShell('/applications/app-a/overview');

    expect(await screen.findByText('Overview content')).toBeInTheDocument();
    const nav = screen.getByRole('navigation', { name: 'Primary' });
    expect(within(nav).getByText('Overview')).toBeInTheDocument();
  });
});

describe('AppShell responsive mobile nav (TASK-UX-04)', () => {
  beforeEach(() => {
    vi.mocked(healthApi.fetchReadiness).mockResolvedValue('healthy');
  });

  it('opens an accessible drawer with the same nav when the menu button is clicked', async () => {
    renderShell();

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();

    screen.getByRole('button', { name: 'Open navigation' }).click();

    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByText('Dashboard')).toBeInTheDocument();
  });
});

describe('AppShell health indicator (TASK-UX-04)', () => {
  it.each([
    ['healthy', 'bg-success'],
    ['degraded', 'bg-warning'],
    ['unhealthy', 'bg-danger'],
    ['unknown', 'bg-text-muted'],
  ] as const)('reflects a %s readiness state', async (readiness, expectedClass) => {
    vi.mocked(healthApi.fetchReadiness).mockResolvedValue(readiness);

    renderShell();

    const dot = await screen.findByTestId('health-indicator-dot');
    await vi.waitFor(() => expect(dot).toHaveAttribute('data-readiness', readiness));
    expect(dot.className).toContain(expectedClass);
  });
});

