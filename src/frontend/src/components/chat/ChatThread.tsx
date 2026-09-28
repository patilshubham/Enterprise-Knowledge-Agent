import { useEffect, useRef } from 'react';
import { MessagesSquare } from 'lucide-react';

import { EmptyState } from '@/components/patterns/EmptyState';
import { Skeleton } from '@/components/ui/skeleton';
import { ChatMessageBubble, type ChatBubbleMessage } from './ChatMessageBubble';

export interface ChatThreadProps {
  messages: ChatBubbleMessage[];
  isLoading?: boolean;
  /** True while the assistant is actively streaming a token response. */
  isStreaming?: boolean;
  onRetry?: () => void;
}

export function ChatThread({ messages, isLoading, isStreaming, onRetry }: ChatThreadProps) {
  const bottomRef = useRef<HTMLDivElement>(null);

  // Keep the latest message (and streaming tokens) in view.
  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: 'smooth', block: 'end' });
  }, [messages, isStreaming]);

  if (isLoading) {
    return (
      <div className="space-y-4 p-4" aria-label="Loading conversation">
        <Skeleton className="h-16 w-2/3" />
        <Skeleton className="ml-auto h-16 w-1/2" />
        <Skeleton className="h-24 w-3/4" />
      </div>
    );
  }

  if (messages.length === 0) {
    return (
      <div className="flex h-full items-center justify-center p-4">
        <EmptyState
          icon={MessagesSquare}
          title="Ask about your knowledge base"
          description="Answers are grounded in this application's knowledge files. Ask a question below to get started."
        />
      </div>
    );
  }

  return (
    <div
      className="flex flex-col gap-6 p-4"
      aria-live="polite"
      aria-busy={isStreaming ? 'true' : 'false'}
    >
      {messages.map((message) => (
        <ChatMessageBubble
          key={message.id}
          message={message}
          onRetry={message.status === 'Failed' ? onRetry : undefined}
          retryDisabled={isStreaming}
        />
      ))}
      <div ref={bottomRef} />
    </div>
  );
}
