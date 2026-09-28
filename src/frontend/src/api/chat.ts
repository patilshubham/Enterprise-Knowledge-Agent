import { apiClient, apiFetch } from './apiClient';
import { API_BASE_URL } from './config';
import { ApiError, type ProblemDetails } from './problemDetails';

// -------------------------------------------------------------------------
// Response shapes (mirror src/application/Chat/ChatDtos.cs)
// -------------------------------------------------------------------------

/** Mirrors `ConversationSummaryResponse`. */
export interface ConversationSummaryResponse {
  id: string;
  title: string;
  createdBy: string;
  createdAt: string;
  lastMessageAt: string;
  messageCount: number;
}

/** Mirrors `ConversationSettingsResponse`. */
export interface ConversationSettingsResponse {
  knowledgeScope: string;
  selectedFileIds: string[];
  generationDeployment: string;
  temperature: number;
  retrievalMode: string;
  hasCustomSystemPrompt: boolean;
}

/** Mirrors `ChatEvidenceResponse` (shaped to align with the output citation drawer). */
export interface ChatEvidenceResponse {
  sourceId: string;
  fileId: string;
  chunkId: string;
  fileName: string;
  headingPath: string;
  passage: string;
  characterStart: number;
  characterEnd: number;
  sourceUnavailable: boolean;
}

/** Assistant/user message status - mirrors `MessageStatus` (src/shared/Chat/MessageStatus.cs). */
export type ChatMessageStatus = 'Streaming' | 'Completed' | 'NoResults' | 'Failed' | 'Cancelled';

/** Message author - mirrors `MessageRole` (src/shared/Chat/MessageRole.cs). */
export type ChatMessageRole = 'User' | 'Assistant';

/** Mirrors `ConversationMessageResponse`. */
export interface ConversationMessageResponse {
  id: string;
  role: ChatMessageRole;
  content: string;
  status: ChatMessageStatus;
  sequence: number;
  createdAt: string;
  completedAt: string | null;
  failureCategory: string | null;
  failureMessage: string | null;
  citedSourceIds: string[];
  evidence: ChatEvidenceResponse[];
}

/** Mirrors `ConversationDetailResponse`. */
export interface ConversationDetailResponse {
  id: string;
  title: string;
  createdBy: string;
  createdAt: string;
  lastMessageAt: string;
  settings: ConversationSettingsResponse;
  messages: ConversationMessageResponse[];
}

// -------------------------------------------------------------------------
// Request shapes
// -------------------------------------------------------------------------

/** Mirrors `ChatAdvancedConfigRequest` - every field optional (config-free defaults). */
export interface ChatAdvancedConfigRequest {
  knowledgeScope?: 'AllReadyEnabled' | 'Selected';
  selectedFileIds?: string[];
  generationDeployment?: string;
  temperature?: number;
  retrievalMode?: 'Semantic' | 'Hybrid';
  systemPrompt?: string;
}

export interface CreateConversationRequest {
  advanced?: ChatAdvancedConfigRequest;
}

// -------------------------------------------------------------------------
// Streaming events (parsed from the SSE `messages` endpoint)
// -------------------------------------------------------------------------

export interface ChatTokenStreamEvent {
  type: 'token';
  text: string;
}

export interface ChatDoneStreamEvent {
  type: 'done';
  messageId: string;
  status: ChatMessageStatus;
  citedSourceIds: string[];
  evidence: ChatEvidenceResponse[];
}

export interface ChatErrorStreamEvent {
  type: 'error';
  messageId: string;
  category: string;
  message: string;
}

export type ChatStreamEvent = ChatTokenStreamEvent | ChatDoneStreamEvent | ChatErrorStreamEvent;

// -------------------------------------------------------------------------
// Query keys
// -------------------------------------------------------------------------

export const chatQueryKeys = {
  all: ['chat'] as const,
  list: (applicationId: string) => [...chatQueryKeys.all, 'list', applicationId] as const,
  detail: (applicationId: string, conversationId: string) =>
    [...chatQueryKeys.all, 'detail', applicationId, conversationId] as const,
};

// -------------------------------------------------------------------------
// CRUD
// -------------------------------------------------------------------------

export function listConversations(applicationId: string): Promise<ConversationSummaryResponse[]> {
  return apiClient.get<ConversationSummaryResponse[]>(
    `/api/applications/${applicationId}/conversations`,
  );
}

export function getConversation(
  applicationId: string,
  conversationId: string,
): Promise<ConversationDetailResponse> {
  return apiClient.get<ConversationDetailResponse>(
    `/api/applications/${applicationId}/conversations/${conversationId}`,
  );
}

export function createConversation(
  applicationId: string,
  request: CreateConversationRequest = {},
): Promise<ConversationDetailResponse> {
  return apiClient.post<ConversationDetailResponse>(
    `/api/applications/${applicationId}/conversations`,
    request,
  );
}

export function renameConversation(
  applicationId: string,
  conversationId: string,
  title: string,
): Promise<ConversationSummaryResponse> {
  return apiFetch<ConversationSummaryResponse>(
    `/api/applications/${applicationId}/conversations/${conversationId}`,
    { method: 'PATCH', body: { title } },
  );
}

export function deleteConversation(applicationId: string, conversationId: string): Promise<void> {
  return apiClient.delete<void>(
    `/api/applications/${applicationId}/conversations/${conversationId}`,
  );
}

// -------------------------------------------------------------------------
// Streaming send (Server-Sent Events)
// -------------------------------------------------------------------------

/**
 * Sends a user message and yields the assistant's answer as it streams back
 * over Server-Sent Events. `EventSource` cannot be used here because the
 * request must be a POST that carries a JSON body - so we drive `fetch` + a
 * `ReadableStream` reader directly and parse the `event:`/`data:` frames
 * ourselves.
 *
 * Validation/authorization failures (before the stream begins) surface as a
 * thrown `ApiError`; once the stream is flowing, transport-level failures are
 * delivered as an `error` event by the server.
 */
export async function* sendMessageStream(
  applicationId: string,
  conversationId: string,
  content: string,
  signal?: AbortSignal,
): AsyncGenerator<ChatStreamEvent, void, unknown> {
  const response = await fetch(
    `${API_BASE_URL}/api/applications/${applicationId}/conversations/${conversationId}/messages`,
    {
      method: 'POST',
      signal,
      headers: {
        Accept: 'text/event-stream',
        'Content-Type': 'application/json',
        'X-Correlation-Id': crypto.randomUUID(),
      },
      body: JSON.stringify({ content }),
    },
  );

  if (!response.ok || !response.body) {
    const contentType = response.headers.get('content-type') ?? '';
    const isJson =
      contentType.includes('application/problem+json') || contentType.includes('application/json');
    const payload = isJson ? await response.json().catch(() => undefined) : undefined;
    const problem: ProblemDetails = (payload as ProblemDetails) ?? {
      title: response.statusText || 'Failed to send message',
      status: response.status,
    };
    throw new ApiError(problem);
  }

  const reader = response.body.getReader();
  const decoder = new TextDecoder();
  let buffer = '';

  try {
    for (;;) {
      const { done, value } = await reader.read();
      if (done) {
        break;
      }

      buffer += decoder.decode(value, { stream: true });

      // SSE frames are separated by a blank line.
      let separatorIndex = buffer.indexOf('\n\n');
      while (separatorIndex !== -1) {
        const rawFrame = buffer.slice(0, separatorIndex);
        buffer = buffer.slice(separatorIndex + 2);
        const parsed = parseFrame(rawFrame);
        if (parsed) {
          yield parsed;
        }
        separatorIndex = buffer.indexOf('\n\n');
      }
    }
  } finally {
    reader.releaseLock();
  }
}

function parseFrame(rawFrame: string): ChatStreamEvent | null {
  let eventName = 'message';
  const dataLines: string[] = [];

  for (const line of rawFrame.split('\n')) {
    if (line.startsWith('event:')) {
      eventName = line.slice('event:'.length).trim();
    } else if (line.startsWith('data:')) {
      dataLines.push(line.slice('data:'.length).trimStart());
    }
  }

  if (dataLines.length === 0) {
    return null;
  }

  let data: unknown;
  try {
    data = JSON.parse(dataLines.join('\n'));
  } catch {
    return null;
  }

  switch (eventName) {
    case 'token':
      return { type: 'token', text: (data as { text: string }).text };
    case 'done':
      return {
        type: 'done',
        ...(data as Omit<ChatDoneStreamEvent, 'type'>),
      };
    case 'error':
      return {
        type: 'error',
        ...(data as Omit<ChatErrorStreamEvent, 'type'>),
      };
    default:
      return null;
  }
}
