import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';

import * as applicationsApi from '@/api/applications';
import type { ApplicationResponse } from '@/api/applications';
import * as chatApi from '@/api/chat';
import type {
  ChatStreamEvent,
  ConversationDetailResponse,
  ConversationMessageResponse,
  ConversationSummaryResponse,
} from '@/api/chat';
import * as deploymentsApi from '@/api/deployments';
import * as knowledgeFilesApi from '@/api/knowledgeFiles';
import ApplicationChatPage from '@/pages/applications/ApplicationChatPage';

vi.mock('@/api/chat', async () => {
  const actual = await vi.importActual<typeof import('@/api/chat')>('@/api/chat');
  return {
    ...actual,
    listConversations: vi.fn(),
    getConversation: vi.fn(),
    createConversation: vi.fn(),
    renameConversation: vi.fn(),
    deleteConversation: vi.fn(),
    sendMessageStream: vi.fn(),
  };
});

vi.mock('@/api/applications', async () => {
  const actual = await vi.importActual<typeof import('@/api/applications')>('@/api/applications');
  return { ...actual, getApplication: vi.fn() };
});

vi.mock('@/api/deployments', async () => {
  const actual = await vi.importActual<typeof import('@/api/deployments')>('@/api/deployments');
  return { ...actual, listDeployments: vi.fn() };
});

vi.mock('@/api/knowledgeFiles', async () => {
  const actual =
    await vi.importActual<typeof import('@/api/knowledgeFiles')>('@/api/knowledgeFiles');
  return { ...actual, listKnowledgeFiles: vi.fn() };
});

function makeApplication(overrides: Partial<ApplicationResponse> = {}): ApplicationResponse {
  return {
    id: 'app-1',
    name: 'Regression Smoke Test',
    description: null,
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

function makeConversation(
  overrides: Partial<ConversationSummaryResponse> = {},
): ConversationSummaryResponse {
  return {
    id: 'conv-1',
    title: 'Vacation policy',
    createdBy: 'user-1',
    createdAt: '2024-01-01T00:00:00Z',
    lastMessageAt: '2024-01-01T00:00:00Z',
    messageCount: 2,
    ...overrides,
  };
}

function makeDetail(
  overrides: Partial<ConversationDetailResponse> = {},
): ConversationDetailResponse {
  return {
    id: 'conv-1',
    title: 'Vacation policy',
    createdBy: 'user-1',
    createdAt: '2024-01-01T00:00:00Z',
    lastMessageAt: '2024-01-01T00:00:00Z',
    settings: {
      knowledgeScope: 'AllReadyEnabled',
      selectedFileIds: [],
      generationDeployment: 'gpt-4o',
      temperature: 0.2,
      retrievalMode: 'Semantic',
      hasCustomSystemPrompt: false,
    },
    messages: [],
    ...overrides,
  };
}

async function* streamOf(events: ChatStreamEvent[]): AsyncGenerator<ChatStreamEvent> {
  for (const event of events) {
    yield event;
  }
}

function makeMessage(
  overrides: Partial<ConversationMessageResponse> = {},
): ConversationMessageResponse {
  return {
    id: 'msg-1',
    role: 'Assistant',
    content: '',
    status: 'Completed',
    sequence: 1,
    createdAt: '2024-01-01T00:00:00Z',
    completedAt: '2024-01-01T00:00:01Z',
    failureCategory: null,
    failureMessage: null,
    citedSourceIds: [],
    evidence: [],
    ...overrides,
  };
}

function renderPage(initialEntry: string) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });

  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={[initialEntry]}>
        <Routes>
          <Route path="/applications/:applicationId/chat" element={<ApplicationChatPage />} />
          <Route
            path="/applications/:applicationId/chat/:conversationId"
            element={<ApplicationChatPage />}
          />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe('ApplicationChatPage (TASK-CHT)', () => {
  afterEach(() => {
    vi.clearAllMocks();
  });

  it('lists shared conversations in the sidebar', async () => {
    vi.mocked(applicationsApi.getApplication).mockResolvedValue(makeApplication());
    vi.mocked(deploymentsApi.listDeployments).mockResolvedValue([]);
    vi.mocked(knowledgeFilesApi.listKnowledgeFiles).mockResolvedValue([]);
    vi.mocked(chatApi.listConversations).mockResolvedValue([
      makeConversation(),
      makeConversation({ id: 'conv-2', title: 'Benefits overview' }),
    ]);

    renderPage('/applications/app-1/chat');

    expect(await screen.findByText('Vacation policy')).toBeInTheDocument();
    expect(screen.getByText('Benefits overview')).toBeInTheDocument();
  });

  it('streams a grounded answer with citations when a message is sent', async () => {
    vi.mocked(applicationsApi.getApplication).mockResolvedValue(makeApplication());
    vi.mocked(chatApi.listConversations).mockResolvedValue([makeConversation()]);
    const answerEvidence = [
      {
        sourceId: 'SOURCE-001',
        fileId: 'file-1',
        chunkId: 'chunk-1',
        fileName: 'company-handbook.txt',
        headingPath: 'Leave',
        passage: '20 days vacation',
        characterStart: 0,
        characterEnd: 16,
        sourceUnavailable: false,
      },
    ];
    // First load is empty; after the stream persists, the refetch returns the turn.
    vi.mocked(chatApi.getConversation).mockResolvedValueOnce(makeDetail());
    vi.mocked(chatApi.getConversation).mockResolvedValue(
      makeDetail({
        messages: [
          makeMessage({
            id: 'msg-1',
            role: 'User',
            content: 'How many vacation days?',
            sequence: 1,
          }),
          makeMessage({
            id: 'msg-2',
            role: 'Assistant',
            content: 'Vacation policy: 20 days [SOURCE-001].',
            sequence: 2,
            citedSourceIds: ['SOURCE-001'],
            evidence: answerEvidence,
          }),
        ],
      }),
    );
    vi.mocked(chatApi.sendMessageStream).mockReturnValue(
      streamOf([
        { type: 'token', text: 'Vacation policy: ' },
        { type: 'token', text: '20 days [SOURCE-001].' },
        {
          type: 'done',
          messageId: 'msg-2',
          status: 'Completed',
          citedSourceIds: ['SOURCE-001'],
          evidence: answerEvidence,
        },
      ]),
    );

    renderPage('/applications/app-1/chat/conv-1');

    const textbox = await screen.findByLabelText('Message');
    fireEvent.change(textbox, { target: { value: 'How many vacation days?' } });
    fireEvent.click(screen.getByLabelText('Send message'));

    await waitFor(() => {
      const answerParagraphs = screen.getAllByText(
        (_content, element) =>
          element?.tagName.toLowerCase() === 'p' &&
          element.textContent?.startsWith('Vacation policy: 20 days') === true,
      );
      expect(answerParagraphs.length).toBeGreaterThan(0);
    });
    await waitFor(() => {
      expect(chatApi.sendMessageStream).toHaveBeenCalledWith(
        'app-1',
        'conv-1',
        'How many vacation days?',
        expect.any(AbortSignal),
      );
    });
  });

  it('reuses the same conversation for follow-up messages instead of starting a new chat each time', async () => {
    vi.mocked(applicationsApi.getApplication).mockResolvedValue(makeApplication());
    vi.mocked(deploymentsApi.listDeployments).mockResolvedValue([]);
    vi.mocked(knowledgeFilesApi.listKnowledgeFiles).mockResolvedValue([]);
    vi.mocked(chatApi.listConversations).mockResolvedValue([]);
    vi.mocked(chatApi.createConversation).mockResolvedValue(
      makeDetail({ id: 'conv-new', title: 'New chat', messages: [] }),
    );
    vi.mocked(chatApi.getConversation).mockResolvedValue(
      makeDetail({ id: 'conv-new', title: 'New chat', messages: [] }),
    );
    vi.mocked(chatApi.sendMessageStream).mockImplementation(() =>
      streamOf([
        { type: 'token', text: 'ok' },
        { type: 'done', messageId: 'msg', status: 'Completed', citedSourceIds: [], evidence: [] },
      ]),
    );

    // Start from a brand-new chat (no conversation id in the URL).
    renderPage('/applications/app-1/chat');

    const textbox = await screen.findByLabelText('Message');
    fireEvent.change(textbox, { target: { value: 'first question' } });
    fireEvent.click(screen.getByLabelText('Send message'));

    // The first message creates exactly one conversation and sends to it.
    await waitFor(() => {
      expect(chatApi.createConversation).toHaveBeenCalledTimes(1);
    });
    await waitFor(() => {
      expect(chatApi.sendMessageStream).toHaveBeenNthCalledWith(
        1,
        'app-1',
        'conv-new',
        'first question',
        expect.any(AbortSignal),
      );
    });

    // Wait for the stream to settle (the Stop control disappears).
    await waitFor(() => {
      expect(screen.queryByLabelText('Stop generating')).not.toBeInTheDocument();
    });

    // Send a follow-up message.
    fireEvent.change(textbox, { target: { value: 'second question' } });
    fireEvent.click(screen.getByLabelText('Send message'));

    // The follow-up must go to the SAME conversation - not spawn a new one.
    await waitFor(() => {
      expect(chatApi.sendMessageStream).toHaveBeenNthCalledWith(
        2,
        'app-1',
        'conv-new',
        'second question',
        expect.any(AbortSignal),
      );
    });
    expect(chatApi.createConversation).toHaveBeenCalledTimes(1);
  });

  it('shows a retry action when the answer stream reports an error', async () => {
    vi.mocked(applicationsApi.getApplication).mockResolvedValue(makeApplication());
    vi.mocked(chatApi.listConversations).mockResolvedValue([makeConversation()]);
    vi.mocked(chatApi.getConversation).mockResolvedValueOnce(makeDetail());
    vi.mocked(chatApi.getConversation).mockResolvedValue(
      makeDetail({
        messages: [
          makeMessage({ id: 'msg-1', role: 'User', content: 'Anything?', sequence: 1 }),
          makeMessage({
            id: 'msg-2',
            role: 'Assistant',
            content: '',
            status: 'Failed',
            sequence: 2,
            failureCategory: 'ServiceUnavailable',
            failureMessage: 'The assistant is temporarily unavailable. Please try again.',
          }),
        ],
      }),
    );
    vi.mocked(chatApi.sendMessageStream).mockReturnValue(
      streamOf([
        {
          type: 'error',
          messageId: 'msg-2',
          category: 'ServiceUnavailable',
          message: 'The assistant is temporarily unavailable. Please try again.',
        },
      ]),
    );

    renderPage('/applications/app-1/chat/conv-1');

    const textbox = await screen.findByLabelText('Message');
    fireEvent.change(textbox, { target: { value: 'Anything?' } });
    fireEvent.click(screen.getByLabelText('Send message'));

    expect(await screen.findByText(/temporarily unavailable/)).toBeInTheDocument();
    expect(await screen.findByRole('button', { name: 'Retry' })).toBeInTheDocument();
  });
});
