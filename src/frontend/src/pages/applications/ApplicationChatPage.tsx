import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useNavigate, useParams } from 'react-router-dom';
import { toast } from 'sonner';

import { applicationsQueryKeys, getApplication } from '@/api/applications';
import {
  chatQueryKeys,
  createConversation,
  deleteConversation,
  getConversation,
  listConversations,
  renameConversation,
  sendMessageStream,
  type ChatAdvancedConfigRequest,
  type ChatMessageStatus,
  type ConversationMessageResponse,
} from '@/api/chat';
import { ApiError } from '@/api/problemDetails';
import { useCapabilities } from '@/app/authz/useCapabilities';
import { ChatAdvancedConfig } from '@/components/chat/ChatAdvancedConfig';
import { ChatComposer } from '@/components/chat/ChatComposer';
import { ChatThread } from '@/components/chat/ChatThread';
import { ConversationSidebar } from '@/components/chat/ConversationSidebar';
import type { ChatBubbleMessage } from '@/components/chat/ChatMessageBubble';

interface LiveTurn {
  userContent: string;
  assistantText: string;
  status: ChatMessageStatus;
  evidence: ChatBubbleMessage['evidence'];
  failureMessage: string | null;
}

function toBubble(message: ConversationMessageResponse): ChatBubbleMessage {
  return {
    id: message.id,
    role: message.role,
    content: message.content,
    status: message.status,
    evidence: message.evidence,
    failureMessage: message.failureMessage,
  };
}

function errorMessage(error: unknown, fallback: string): string {
  return error instanceof ApiError ? (error.problem.detail ?? error.problem.title) : fallback;
}

/**
 * TASK-CHT: full-page, ChatGPT-style knowledge-base assistant
 * (`/applications/:applicationId/chat[/:conversationId]`). Conversations are
 * shared across the application; answers are grounded in the app's knowledge
 * files and stream back token-by-token over SSE with citations you can open in
 * the evidence drawer.
 */
export default function ApplicationChatPage() {
  const { applicationId, conversationId } = useParams<{
    applicationId: string;
    conversationId?: string;
  }>();
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const applicationQuery = useQuery({
    queryKey: applicationsQueryKeys.detail(applicationId ?? ''),
    queryFn: () => getApplication(applicationId ?? ''),
    enabled: Boolean(applicationId),
  });

  const capabilities = useCapabilities(applicationId, {
    viewerExecutionEnabled: applicationQuery.data?.allowViewerExecution,
  });

  const [liveTurn, setLiveTurn] = useState<LiveTurn | null>(null);
  const [advancedConfig, setAdvancedConfig] = useState<ChatAdvancedConfigRequest>({});
  const abortRef = useRef<AbortController | null>(null);
  const streaming = liveTurn !== null;

  // The first message in a new chat creates a conversation, after which we
  // navigate to `/chat/:conversationId`. React Router's navigation runs inside
  // a transition that gets starved by the rapid token-by-token `setLiveTurn`
  // updates while the answer streams, so the `:conversationId` route param can
  // fail to commit until well after the turn finishes. Tracking the id in state
  // (seeded from the URL, set the moment the conversation is created) keeps the
  // same conversation in view and reuses it for follow-up messages instead of
  // spawning a brand-new conversation per message. The URL is still updated for
  // shareability and back/forward, but correctness no longer depends on it.
  const [createdConversationId, setCreatedConversationId] = useState<string>();
  const activeConversationId = conversationId ?? createdConversationId;

  // Drop the local bridge only when the route settles on a *different* explicit
  // conversation (deep link, sidebar selection, browser back/forward) so
  // switching conversations follows the URL. Crucially we must NOT clear it
  // when the param commits to the id we just created, nor when it flickers back
  // to empty: React Router's navigation runs in a low-priority transition that
  // the streaming `setLiveTurn` updates can starve, so the param may briefly
  // commit and then revert. Clearing on those transitions would erase the
  // bridge and make the freshly-created conversation vanish after streaming.
  useEffect(() => {
    if (conversationId && conversationId !== createdConversationId) {
      // Intentional: synchronizes local bridge state with the URL param per the comment above.
      // eslint-disable-next-line react-hooks/set-state-in-effect
      setCreatedConversationId(undefined);
    }
  }, [conversationId, createdConversationId]);

  const conversationsQuery = useQuery({
    queryKey: chatQueryKeys.list(applicationId ?? ''),
    queryFn: () => listConversations(applicationId ?? ''),
    enabled: Boolean(applicationId),
  });

  const detailQuery = useQuery({
    queryKey: chatQueryKeys.detail(applicationId ?? '', activeConversationId ?? ''),
    queryFn: () => getConversation(applicationId ?? '', activeConversationId ?? ''),
    enabled: Boolean(applicationId) && Boolean(activeConversationId) && !streaming,
  });

  const renameMutation = useMutation({
    mutationFn: ({ id, title }: { id: string; title: string }) =>
      renameConversation(applicationId ?? '', id, title),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: chatQueryKeys.list(applicationId ?? '') });
      toast.success('Conversation renamed');
    },
    onError: (error) => toast.error(errorMessage(error, 'Could not rename the conversation.')),
  });

  const deleteMutation = useMutation({
    mutationFn: (id: string) => deleteConversation(applicationId ?? '', id),
    onSuccess: (_data, id) => {
      void queryClient.invalidateQueries({ queryKey: chatQueryKeys.list(applicationId ?? '') });
      if (id === activeConversationId) {
        setCreatedConversationId(undefined);
        navigate(`/applications/${applicationId}/chat`, { replace: true });
      }
      toast.success('Conversation deleted');
    },
    onError: (error) => toast.error(errorMessage(error, 'Could not delete the conversation.')),
  });

  const serverMessages = useMemo(
    () => detailQuery.data?.messages ?? [],
    [detailQuery.data?.messages],
  );

  const messages: ChatBubbleMessage[] = useMemo(() => {
    const base = serverMessages.map(toBubble);
    if (!liveTurn) {
      return base;
    }
    return [
      ...base,
      {
        id: 'live-user',
        role: 'User',
        content: liveTurn.userContent,
        status: 'Completed',
        evidence: [],
      },
      {
        id: 'live-assistant',
        role: 'Assistant',
        content: liveTurn.assistantText,
        status: liveTurn.status,
        evidence: liveTurn.evidence,
        failureMessage: liveTurn.failureMessage,
      },
    ];
  }, [serverMessages, liveTurn]);

  const lastUserContent = useMemo(() => {
    for (let i = serverMessages.length - 1; i >= 0; i -= 1) {
      if (serverMessages[i].role === 'User') {
        return serverMessages[i].content;
      }
    }
    return null;
  }, [serverMessages]);

  const runTurn = useCallback(
    async (targetConversationId: string, content: string) => {
      const controller = new AbortController();
      abortRef.current = controller;
      try {
        for await (const event of sendMessageStream(
          applicationId ?? '',
          targetConversationId,
          content,
          controller.signal,
        )) {
          if (event.type === 'token') {
            setLiveTurn((prev) =>
              prev ? { ...prev, assistantText: prev.assistantText + event.text } : prev,
            );
          } else if (event.type === 'done') {
            setLiveTurn((prev) =>
              prev ? { ...prev, status: event.status, evidence: event.evidence } : prev,
            );
          } else {
            setLiveTurn((prev) =>
              prev ? { ...prev, status: 'Failed', failureMessage: event.message } : prev,
            );
          }
        }
      } catch (error) {
        if (!controller.signal.aborted) {
          const message = errorMessage(error, 'The assistant could not respond. Please try again.');
          setLiveTurn((prev) =>
            prev ? { ...prev, status: 'Failed', failureMessage: message } : prev,
          );
          toast.error(message);
        }
      } finally {
        abortRef.current = null;
        // Pull the persisted turn into cache *before* clearing the live overlay
        // so the finalized message renders without a flash of stale content.
        // `staleTime: 0` is required here: for a brand-new conversation this
        // query key was just seeded (via `setQueryData`) with the placeholder
        // `{ title: "New chat", messages: [] }` returned by conversation
        // creation. The global QueryClient default staleTime is 30s, so
        // without this override `fetchQuery` treats that placeholder as still
        // fresh and skips the network call entirely - leaving the header
        // title stuck on "New chat" and the thread showing the empty-state
        // card for up to 30 seconds after the first turn finishes.
        await queryClient
          .fetchQuery({
            queryKey: chatQueryKeys.detail(applicationId ?? '', targetConversationId),
            queryFn: () => getConversation(applicationId ?? '', targetConversationId),
            staleTime: 0,
          })
          .catch(() => undefined);
        void queryClient.invalidateQueries({ queryKey: chatQueryKeys.list(applicationId ?? '') });
        setLiveTurn(null);
      }
    },
    [applicationId, queryClient],
  );

  const handleSend = useCallback(
    async (content: string) => {
      if (!applicationId || streaming) {
        return;
      }
      setLiveTurn({
        userContent: content,
        assistantText: '',
        status: 'Streaming',
        evidence: [],
        failureMessage: null,
      });

      let targetConversationId = activeConversationId;
      if (!targetConversationId) {
        try {
          const hasAdvanced = Object.keys(advancedConfig).length > 0;
          const created = await createConversation(
            applicationId,
            hasAdvanced ? { advanced: advancedConfig } : {},
          );
          targetConversationId = created.id;
          setCreatedConversationId(created.id);
          queryClient.setQueryData(chatQueryKeys.detail(applicationId, created.id), created);
          // Deliberately bypass react-router's `navigate()` here: it schedules the
          // location update inside a React transition, and the rapid token-by-token
          // `setLiveTurn` updates that follow (as the answer streams in) keep
          // preempting/restarting that low-priority render before it ever commits -
          // in practice the transition is starved indefinitely, not just delayed, so
          // `useParams().conversationId` never reflects the new id and the page gets
          // stuck showing "New chat" (verified with live render-log instrumentation).
          // `createdConversationId` (state, set synchronously above) is already the
          // single source of truth for `activeConversationId`, so all we need from the
          // URL here is to *look* right and support copy/paste + refresh - a direct,
          // untransitioned History API write achieves that without racing the stream.
          window.history.replaceState(
            null,
            '',
            `/applications/${applicationId}/chat/${created.id}`,
          );
        } catch (error) {
          const message = errorMessage(error, 'Could not start a new conversation.');
          toast.error(message);
          setLiveTurn(null);
          return;
        }
      }

      await runTurn(targetConversationId, content);
    },
    [applicationId, activeConversationId, advancedConfig, streaming, queryClient, runTurn],
  );

  const handleRetry = useCallback(() => {
    if (lastUserContent && activeConversationId) {
      void runTurn(activeConversationId, lastUserContent);
      setLiveTurn({
        userContent: lastUserContent,
        assistantText: '',
        status: 'Streaming',
        evidence: [],
        failureMessage: null,
      });
    }
  }, [lastUserContent, activeConversationId, runTurn]);

  const handleStop = useCallback(() => {
    abortRef.current?.abort();
  }, []);

  const handleNewChat = useCallback(() => {
    if (streaming) {
      return;
    }
    setAdvancedConfig({});
    setCreatedConversationId(undefined);
    navigate(`/applications/${applicationId}/chat`);
  }, [applicationId, navigate, streaming]);

  if (!applicationId) {
    return null;
  }

  const showNewChatIntro = !activeConversationId && !streaming;
  const composerDisabled = !capabilities.canExecuteAgent;

  return (
    <div className="flex h-full min-h-0 overflow-hidden rounded-xl border border-border bg-background">
      <ConversationSidebar
        conversations={conversationsQuery.data ?? []}
        activeConversationId={activeConversationId}
        isLoading={conversationsQuery.isLoading}
        canManageAll={capabilities.canManageAgents}
        onSelect={(id) => navigate(`/applications/${applicationId}/chat/${id}`)}
        onNewChat={handleNewChat}
        onRename={(id, title) => renameMutation.mutate({ id, title })}
        onDelete={(id) => deleteMutation.mutate(id)}
        renamePending={renameMutation.isPending}
        deletePending={deleteMutation.isPending}
      />

      <div className="flex min-w-0 flex-1 flex-col">
        <header className="border-b border-border px-6 py-4">
          <h1 className="truncate text-lg font-semibold text-text-primary">
            {activeConversationId ? (detailQuery.data?.title ?? 'Conversation') : 'New chat'}
          </h1>
          <p className="text-sm text-text-muted">
            Answers are grounded in this application&apos;s knowledge base.
          </p>
        </header>

        <div className="min-h-0 flex-1 overflow-y-auto">
          <div className="mx-auto max-w-3xl">
            <ChatThread
              messages={messages}
              isLoading={Boolean(activeConversationId) && detailQuery.isLoading && !streaming}
              isStreaming={streaming}
              onRetry={handleRetry}
            />
          </div>
        </div>

        <div className="mx-auto w-full max-w-3xl">
          {showNewChatIntro ? (
            <div className="px-4 pt-4">
              <ChatAdvancedConfig
                applicationId={applicationId}
                value={advancedConfig}
                onChange={setAdvancedConfig}
                disabled={streaming}
              />
            </div>
          ) : activeConversationId && detailQuery.data ? (
            <div className="px-4 pt-4">
              <ChatAdvancedConfig
                applicationId={applicationId}
                readOnly
                hasCustomSystemPrompt={detailQuery.data.settings.hasCustomSystemPrompt}
                value={{
                  knowledgeScope: detailQuery.data.settings
                    .knowledgeScope as ChatAdvancedConfigRequest['knowledgeScope'],
                  selectedFileIds: detailQuery.data.settings.selectedFileIds,
                  generationDeployment: detailQuery.data.settings.generationDeployment,
                  temperature: detailQuery.data.settings.temperature,
                  retrievalMode: detailQuery.data.settings
                    .retrievalMode as ChatAdvancedConfigRequest['retrievalMode'],
                }}
                onChange={() => {}}
              />
            </div>
          ) : null}
          <ChatComposer
            onSend={handleSend}
            onStop={handleStop}
            isStreaming={streaming}
            disabled={composerDisabled}
            disabledReason={
              composerDisabled
                ? 'You do not have permission to send messages in this application.'
                : undefined
            }
          />
        </div>
      </div>
    </div>
  );
}
