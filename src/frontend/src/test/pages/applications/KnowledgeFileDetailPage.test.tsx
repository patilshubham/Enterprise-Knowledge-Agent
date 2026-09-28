import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import * as knowledgeFilesApi from '@/api/knowledgeFiles';
import type { KnowledgeFileResponse } from '@/api/knowledgeFiles';
import { ApiError } from '@/api/problemDetails';
import KnowledgeFileDetailPage from '@/pages/applications/KnowledgeFileDetailPage';

vi.mock('@/api/knowledgeFiles', async () => {
  const actual =
    await vi.importActual<typeof import('@/api/knowledgeFiles')>('@/api/knowledgeFiles');
  return {
    ...actual,
    listKnowledgeFiles: vi.fn(),
    previewKnowledgeFile: vi.fn(),
    disableKnowledgeFile: vi.fn(),
    enableKnowledgeFile: vi.fn(),
    reprocessKnowledgeFile: vi.fn(),
    deleteKnowledgeFile: vi.fn(),
  };
});

function makeFile(overrides: Partial<KnowledgeFileResponse> = {}): KnowledgeFileResponse {
  return {
    id: 'file-1',
    applicationId: 'app-1',
    displayName: 'policy.md',
    fileSize: 2048,
    mimeType: 'text/markdown',
    encoding: 'utf-8',
    status: 2,
    isEnabled: true,
    uploadedBy: 'user-1',
    uploadedAt: '2024-01-01T00:00:00Z',
    lastProcessedAt: '2024-01-01T00:05:00Z',
    stage: 8,
    progress: 100,
    ...overrides,
  };
}

function renderPage() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });

  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={['/applications/app-1/knowledge/file-1']}>
        <Routes>
          <Route
            path="/applications/:applicationId/knowledge/:fileId"
            element={<KnowledgeFileDetailPage />}
          />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe('KnowledgeFileDetailPage (TASK-KNW-06)', () => {
  afterEach(() => {
    vi.clearAllMocks();
  });

  it('renders file details and a preview for a Ready file, including manage actions', async () => {
    vi.mocked(knowledgeFilesApi.listKnowledgeFiles).mockResolvedValue([makeFile()]);
    vi.mocked(knowledgeFilesApi.previewKnowledgeFile).mockResolvedValue('# Policy\n\nContent.');

    renderPage();

    expect(await screen.findByRole('heading', { name: 'policy.md' })).toBeInTheDocument();
    expect(await screen.findByText('Policy')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /disable/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /reprocess/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /delete/i })).toBeInTheDocument();
  });

  it('does not fetch a preview for a file still processing', async () => {
    vi.mocked(knowledgeFilesApi.listKnowledgeFiles).mockResolvedValue([
      makeFile({ status: 1, stage: 3, progress: 40 }),
    ]);

    renderPage();

    await screen.findByRole('heading', { name: 'policy.md' });
    expect(
      screen.getByText(/preview is available once the file has finished processing/i),
    ).toBeInTheDocument();
    expect(knowledgeFilesApi.previewKnowledgeFile).not.toHaveBeenCalled();
  });

  it('shows a not-found message when the file id has no match', async () => {
    vi.mocked(knowledgeFilesApi.listKnowledgeFiles).mockResolvedValue([]);

    renderPage();

    expect(await screen.findByRole('alert')).toHaveTextContent(/don't have access/i);
  });

  it('reprocesses a file after confirming', async () => {
    vi.mocked(knowledgeFilesApi.listKnowledgeFiles).mockResolvedValue([makeFile({ status: 3 })]);
    vi.mocked(knowledgeFilesApi.reprocessKnowledgeFile).mockResolvedValue(
      makeFile({ status: 0, stage: 0, progress: 0 }),
    );

    renderPage();

    await screen.findByRole('heading', { name: 'policy.md' });
    fireEvent.click(screen.getByRole('button', { name: /reprocess/i }));
    const dialog = await screen.findByRole('dialog');
    fireEvent.click(within(dialog).getByRole('button', { name: 'Reprocess' }));

    await waitFor(() => {
      expect(knowledgeFilesApi.reprocessKnowledgeFile).toHaveBeenCalledWith('app-1', 'file-1');
    });
  });

  it('shows an error state when the list request fails', async () => {
    vi.mocked(knowledgeFilesApi.listKnowledgeFiles).mockRejectedValue(
      new ApiError({ title: 'Server error', status: 500 }),
    );

    renderPage();

    expect(await screen.findByRole('alert')).toHaveTextContent('Server error');
  });
});
