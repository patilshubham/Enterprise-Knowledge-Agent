import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, within } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import * as auditApi from '@/api/audit';
import type { AuditEventResponse, AuditEventSearchResponse } from '@/api/audit';
import { ApiError } from '@/api/problemDetails';
import ApplicationActivityPage from '@/pages/applications/ApplicationActivityPage';

vi.mock('@/api/audit', async () => {
  const actual = await vi.importActual<typeof import('@/api/audit')>('@/api/audit');
  return {
    ...actual,
    listApplicationActivity: vi.fn(),
  };
});

function makeEvent(overrides: Partial<AuditEventResponse> = {}): AuditEventResponse {
  return {
    id: 'event-1',
    occurredAt: '2024-01-01T00:00:00Z',
    action: 'KnowledgeFileUploaded',
    actorSubject: 'user-1',
    outcome: 'Success',
    applicationId: 'app-1',
    entityType: 'KnowledgeFile',
    entityId: 'file-1',
    entityDisplayName: 'handbook.pdf',
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
      <MemoryRouter initialEntries={['/applications/app-1/activity']}>
        <Routes>
          <Route
            path="/applications/:applicationId/activity"
            element={<ApplicationActivityPage />}
          />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe('ApplicationActivityPage (TASK-AUD-03)', () => {
  afterEach(() => {
    vi.clearAllMocks();
  });

  it('lists audit events scoped to the application', async () => {
    vi.mocked(auditApi.listApplicationActivity).mockResolvedValue(
      makeSearchResponse([makeEvent()]),
    );

    renderPage();

    const table = within(await screen.findByRole('table'));
    expect(await table.findByText('handbook.pdf')).toBeInTheDocument();
    expect(auditApi.listApplicationActivity).toHaveBeenCalledWith(
      'app-1',
      expect.objectContaining({ page: 1, pageSize: 25 }),
    );
  });

  it('shows an error state when the search request fails', async () => {
    vi.mocked(auditApi.listApplicationActivity).mockRejectedValue(
      new ApiError({ title: 'Server error', status: 500 }),
    );

    renderPage();

    expect(await screen.findByRole('alert')).toHaveTextContent('Server error');
  });
});
