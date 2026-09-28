import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import * as knowledgeFilesApi from '@/api/knowledgeFiles';
import type { KnowledgeFileResponse } from '@/api/knowledgeFiles';
import { ApiError } from '@/api/problemDetails';
import ApplicationKnowledgePage from '@/pages/applications/ApplicationKnowledgePage';

vi.mock('@/api/knowledgeFiles', async () => {
  const actual =
    await vi.importActual<typeof import('@/api/knowledgeFiles')>('@/api/knowledgeFiles');
  return {
    ...actual,
    listKnowledgeFiles: vi.fn(),
    uploadKnowledgeFiles: vi.fn(),
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
      <MemoryRouter initialEntries={['/applications/app-1/knowledge']}>
        <Routes>
          <Route
            path="/applications/:applicationId/knowledge"
            element={<ApplicationKnowledgePage />}
          />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe('ApplicationKnowledgePage (TASK-KNW-06)', () => {
  afterEach(() => {
    vi.clearAllMocks();
  });

  it('lists knowledge files for an ApplicationAdmin, including manage actions', async () => {
    vi.mocked(knowledgeFilesApi.listKnowledgeFiles).mockResolvedValue([makeFile()]);

    renderPage();

    const table = within(await screen.findByRole('table'));
    expect(await table.findByText('policy.md')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /^upload$/i })).toBeInTheDocument();
  });

  it('shows an empty state when there are no files', async () => {
    vi.mocked(knowledgeFilesApi.listKnowledgeFiles).mockResolvedValue([]);

    renderPage();

    const table = within(await screen.findByRole('table'));
    expect(await table.findByText(/upload one to get started/i)).toBeInTheDocument();
  });

  it('shows an error state when the list request fails', async () => {
    vi.mocked(knowledgeFilesApi.listKnowledgeFiles).mockRejectedValue(
      new ApiError({ title: 'Server error', status: 500 }),
    );

    renderPage();

    expect(await screen.findByRole('alert')).toHaveTextContent('Server error');
  });

  it('uploads files via the upload dialog', async () => {
    vi.mocked(knowledgeFilesApi.listKnowledgeFiles).mockResolvedValue([]);
    vi.mocked(knowledgeFilesApi.uploadKnowledgeFiles).mockResolvedValue([
      { fileName: 'new.md', outcome: 0, fileId: 'file-2', reason: null },
    ]);

    renderPage();

    const table = within(await screen.findByRole('table'));
    await table.findByText(/upload one to get started/i);
    fireEvent.click(screen.getByRole('button', { name: /^upload$/i }));

    const dialog = await screen.findByRole('dialog');
    const input = dialog.querySelector('input[type="file"]') as HTMLInputElement;
    const file = new File(['content'], 'new.md', { type: 'text/markdown' });
    fireEvent.change(input, { target: { files: [file] } });

    fireEvent.click(within(dialog).getByRole('button', { name: /^upload \(1\)$/i }));

    await waitFor(() => {
      expect(knowledgeFilesApi.uploadKnowledgeFiles).toHaveBeenCalledWith('app-1', [file]);
    });
  });

  it('disables a file after confirming', async () => {
    vi.mocked(knowledgeFilesApi.listKnowledgeFiles).mockResolvedValue([makeFile()]);
    vi.mocked(knowledgeFilesApi.disableKnowledgeFile).mockResolvedValue(
      makeFile({ isEnabled: false, status: 4 }),
    );

    renderPage();

    const table = within(await screen.findByRole('table'));
    await table.findByText('policy.md');
    const trigger = table.getByLabelText('Row actions');
    fireEvent.pointerDown(trigger, { pointerId: 1, button: 0, ctrlKey: false });
    fireEvent.click(trigger);
    fireEvent.click(await screen.findByRole('menuitem', { name: /disable/i }));
    const dialog = await screen.findByRole('dialog');
    fireEvent.click(within(dialog).getByRole('button', { name: 'Disable' }));

    await waitFor(() => {
      expect(knowledgeFilesApi.disableKnowledgeFile).toHaveBeenCalledWith('app-1', 'file-1');
    });
  });

  it('deletes a file after confirming', async () => {
    vi.mocked(knowledgeFilesApi.listKnowledgeFiles).mockResolvedValue([makeFile()]);
    vi.mocked(knowledgeFilesApi.deleteKnowledgeFile).mockResolvedValue(undefined);

    renderPage();

    const table = within(await screen.findByRole('table'));
    await table.findByText('policy.md');
    const trigger = table.getByLabelText('Row actions');
    fireEvent.pointerDown(trigger, { pointerId: 1, button: 0, ctrlKey: false });
    fireEvent.click(trigger);
    fireEvent.click(await screen.findByRole('menuitem', { name: /delete/i }));
    const dialog = await screen.findByRole('dialog');
    fireEvent.click(within(dialog).getByRole('button', { name: 'Delete' }));

    await waitFor(() => {
      expect(knowledgeFilesApi.deleteKnowledgeFile).toHaveBeenCalledWith('app-1', 'file-1');
    });
  });
});
