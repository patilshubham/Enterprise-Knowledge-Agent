import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import App from '../App';
import * as dashboardsApi from '../api/dashboards';
import * as healthApi from '../api/health';

// TASK-UX-04: the shell's health indicator polls `/health/ready` over a
// real `fetch` (deliberately bypassing apiClient) - mock it here so the
// smoke test never attempts a real network call.
vi.mock('../api/health', () => ({ fetchReadiness: vi.fn() }));

// TASK-PDSH-02: the landing route renders the real Platform Dashboard page,
// which fetches `/api/platform/dashboard` - mock it so the smoke test never
// attempts a real network call.
vi.mock('../api/dashboards', async () => {
  const actual = await vi.importActual<typeof import('../api/dashboards')>('../api/dashboards');
  return { ...actual, getPlatformDashboard: vi.fn() };
});

// TASK-SIG-05: `SignalRProvider` opens a real SignalR connection on mount -
// mock the client library so the smoke test never attempts a real
// negotiate/WebSocket handshake (jsdom also can't resolve a relative hub
// URL without a real page origin).
vi.mock('@microsoft/signalr', () => ({
  HubConnectionBuilder: class {
    withUrl() {
      return this;
    }
    withAutomaticReconnect() {
      return this;
    }
    configureLogging() {
      return this;
    }
    build() {
      return {
        on: () => {},
        off: () => {},
        onreconnecting: () => {},
        onreconnected: () => {},
        onclose: () => {},
        start: () => Promise.resolve(),
        stop: () => Promise.resolve(),
        invoke: () => Promise.resolve(),
        state: 'Connected',
      };
    }
  },
  HubConnectionState: { Connected: 'Connected' },
  LogLevel: { Warning: 3 },
}));

describe('App', () => {
  it('renders the app shell without crashing and redirects to a placeholder route', async () => {
    vi.mocked(healthApi.fetchReadiness).mockResolvedValue('healthy');
    vi.mocked(dashboardsApi.getPlatformDashboard).mockResolvedValue({
      applications: { active: 0, archived: 0, deleted: 0 },
      knowledgeFiles: {
        queued: 0,
        processing: 0,
        ready: 0,
        failed: 0,
        disabled: 0,
        deleting: 0,
        deleted: 0,
      },
      processingJobs: { queued: 0, running: 0, completed: 0, failed: 0, cancelled: 0 },
      agentRuns: { queued: 0, running: 0, completed: 0, failed: 0, cancelled: 0 },
      workers: [],
      aoaiAvailable: true,
      queues: [],
      attentionItems: [],
    });

    render(<App />);

    expect(await screen.findByRole('heading', { name: 'Dashboard' })).toBeInTheDocument();
  });
});
