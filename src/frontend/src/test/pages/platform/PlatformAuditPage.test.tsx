import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, within } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import * as auditApi from '@/api/audit';
import type { AuditEventResponse, AuditEventSearchResponse } from '@/api/audit';
import { ApiError } from '@/api/problemDetails';
import PlatformAuditPage from '@/pages/platform/PlatformAuditPage';

vi.mock('@/api/audit', async () => {
  const actual = await vi.importActual<typeof import('@/api/audit')>('@/api/audit');
  return {
    ...actual,
    listPlatformAudit: vi.fn(),
  };
});

function makeEvent(overrides: Partial<AuditEventResponse> = {}): AuditEventResponse {
  return {
    id: 'event-1',
    occurredAt: '2024-01-01T00:00:00Z',
    action: 'ApplicationCreated',
    actorSubject: 'user-1',
    outcome: 'Success',
    applicationId: 'app-1',
    entityType: 'Application',
    entityId: 'app-1',
    entityDisplayName: 'Contoso Onboarding',
    failureReason: null,
    correlationId: 'corr-1',
    ...overrides,
  };
}

function makeSearchResponse(
  items: AuditEventResponse[],
  overrides: Partial<AuditEventSearchResponse> = {},
): AuditEventSearchResponse {
  return {
    items,
    totalCount: items.length,
    page: 1,
    pageSize: 25,
    ...overrides,
  };
}

function renderPage() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });

  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={['/platform/audit']}>
        <Routes>
          <Route path="/platform/audit" element={<PlatformAuditPage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe('PlatformAuditPage (TASK-AUD-03)', () => {
  afterEach(() => {
    vi.clearAllMocks();
  });

  it('lists audit events across applications', async () => {
    vi.mocked(auditApi.listPlatformAudit).mockResolvedValue(
      makeSearchResponse([
        makeEvent(),
        makeEvent({ id: 'event-2', action: 'AgentCreated', entityDisplayName: 'Support agent' }),
      ]),
    );

    renderPage();

    const table = within(await screen.findByRole('table'));
    expect(await table.findByText('ApplicationCreated')).toBeInTheDocument();
    expect(table.getByText('AgentCreated')).toBeInTheDocument();
  });

  it('shows an empty state when there are no audit events', async () => {
    vi.mocked(auditApi.listPlatformAudit).mockResolvedValue(makeSearchResponse([]));

    renderPage();

    const table = within(await screen.findByRole('table'));
    expect(await table.findByText('No audit events to show.')).toBeInTheDocument();
  });

  it('shows an error state when the search request fails', async () => {
    vi.mocked(auditApi.listPlatformAudit).mockRejectedValue(
      new ApiError({ title: 'Server error', status: 500 }),
    );

    renderPage();

    expect(await screen.findByRole('alert')).toHaveTextContent('Server error');
  });
});
