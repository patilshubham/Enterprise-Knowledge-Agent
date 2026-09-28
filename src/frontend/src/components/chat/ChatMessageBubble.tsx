import { useMemo } from 'react';
import { AlertTriangle, Bot, RotateCcw, User } from 'lucide-react';

import type { ChatEvidenceResponse, ChatMessageRole, ChatMessageStatus } from '@/api/chat';
import { SafeMarkdown } from '@/components/markdown/SafeMarkdown';
import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';

/** Matches module 22's guaranteed `[SOURCE-NNN]` citation markers so they can be stripped from chat answers (sources are not surfaced in chat). */
const SOURCE_MARKER_PATTERN = /\s*\[SOURCE-\d{3}\]/g;

/** Normalized message shape rendered by the thread (server message or in-flight live turn). */
export interface ChatBubbleMessage {
  id: string;
  role: ChatMessageRole;
  content: string;
  status: ChatMessageStatus;
  evidence: ChatEvidenceResponse[];
  failureMessage?: string | null;
}

export interface ChatMessageBubbleProps {
  message: ChatBubbleMessage;
  /** Re-asks the question behind a failed answer. Only wired for failed assistant messages. */
  onRetry?: () => void;
  retryDisabled?: boolean;
}

export function ChatMessageBubble({
  message,
  onRetry,
  retryDisabled,
}: ChatMessageBubbleProps) {
  const isUser = message.role === 'User';
  const isStreaming = message.status === 'Streaming';
  const isFailed = message.status === 'Failed';

  const markdown = useMemo(() => {
    if (isUser) {
      return message.content;
    }
    // Drop the raw `[SOURCE-NNN]` markers so assistant answers read as plain
    // prose without any citation references or a sources list.
    return message.content.replace(SOURCE_MARKER_PATTERN, '');
  }, [isUser, message.content]);

  return (
    <div
      className={cn('flex w-full gap-3', isUser ? 'flex-row-reverse' : 'flex-row')}
      data-role={message.role}
    >
      <div
        className={cn(
          'flex h-8 w-8 shrink-0 items-center justify-center rounded-full',
          isUser ? 'bg-accent/15 text-accent' : 'bg-surface-raised text-text-secondary',
        )}
        aria-hidden="true"
      >
        {isUser ? <User className="h-4 w-4" /> : <Bot className="h-4 w-4" />}
      </div>

      <div
        className={cn(
          'min-w-0 max-w-[calc(100%-3rem)] rounded-2xl px-4 py-3 text-sm',
          isUser
            ? 'bg-accent/10 text-text-primary'
            : 'bg-surface text-text-primary ring-1 ring-border',
        )}
      >
        {isFailed ? (
          <div className="space-y-3">
            <p className="flex items-center gap-2 text-danger" role="alert">
              <AlertTriangle className="h-4 w-4 shrink-0" />
              <span>{message.failureMessage ?? 'Something went wrong generating the answer.'}</span>
            </p>
            {onRetry && (
              <Button variant="outline" size="sm" onClick={onRetry} disabled={retryDisabled}>
                <RotateCcw className="h-4 w-4" />
                Retry
              </Button>
            )}
          </div>
        ) : message.content.length === 0 && isStreaming ? (
          <span
            className="inline-flex items-center gap-1 text-text-muted"
            aria-label="Assistant is typing"
          >
            <span className="h-2 w-2 animate-pulse rounded-full bg-current" />
            <span className="h-2 w-2 animate-pulse rounded-full bg-current [animation-delay:150ms]" />
            <span className="h-2 w-2 animate-pulse rounded-full bg-current [animation-delay:300ms]" />
          </span>
        ) : (
          <SafeMarkdown content={markdown} />
        )}
      </div>
    </div>
  );
}
