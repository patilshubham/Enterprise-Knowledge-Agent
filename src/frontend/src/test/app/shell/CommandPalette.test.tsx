import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import * as healthApi from '@/api/health';
import { AppShell } from '@/app/shell/AppShell';
import { ThemeProvider } from '@/app/theme/ThemeProvider';

vi.mock('@/api/health', () => ({ fetchReadiness: vi.fn() }));

function renderShell(initialPath = '/applications/app-a/overview') {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });

  return render(
    <QueryClientProvider client={queryClient}>
      <ThemeProvider>
        <MemoryRouter initialEntries={[initialPath]}>
          <Routes>
            <Route element={<AppShell />}>
              <Route
                path="/applications/:applicationId/overview"
                element={<div>Overview content</div>}
              />
              <Route path="/applications/:applicationId/agents" element={<div>Agents page</div>} />
              <Route path="/platform/applications" element={<div>Applications list page</div>} />
            </Route>
          </Routes>
        </MemoryRouter>
      </ThemeProvider>
    </QueryClientProvider>,
  );
}

describe('Command palette (TASK-UX-05)', () => {
  beforeEach(() => {
    vi.mocked(healthApi.fetchReadiness).mockResolvedValue('healthy');
  });

  it('opens via the Ctrl/Cmd+K shortcut', async () => {
    renderShell();

    expect(screen.queryByPlaceholderText('Type a command or search...')).not.toBeInTheDocument();

    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'k', ctrlKey: true }));

    expect(await screen.findByPlaceholderText('Type a command or search...')).toBeInTheDocument();
  });

  it('opens via the header search button and shows every command - there is no login/user management in this app', async () => {
    renderShell();

    screen.getByRole('button', { name: 'Open command palette' }).click();

    expect(await screen.findByText('Search outputs')).toBeInTheDocument();
    expect(await screen.findByText('Create agent')).toBeInTheDocument();
    expect(await screen.findByText('Upload knowledge')).toBeInTheDocument();
  });

  it('navigates when a command is run', async () => {
    renderShell();

    screen.getByRole('button', { name: 'Open command palette' }).click();

    const createAgent = await screen.findByText('Create agent');
    createAgent.click();

    expect(await screen.findByText('Agents page')).toBeInTheDocument();
    expect(screen.queryByPlaceholderText('Type a command or search...')).not.toBeInTheDocument();
  });
});

