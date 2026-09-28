import { useLayoutEffect, useRef, useState } from 'react';
import { ArrowUp, Square } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';

/** Client-side cap; the domain allows more (8000) but chat questions stay short. */
export const MAX_MESSAGE_LENGTH = 4000;

export interface ChatComposerProps {
  onSend: (content: string) => void;
  onStop?: () => void;
  isStreaming?: boolean;
  disabled?: boolean;
  disabledReason?: string;
  placeholder?: string;
}

export function ChatComposer({
  onSend,
  onStop,
  isStreaming,
  disabled,
  disabledReason,
  placeholder,
}: ChatComposerProps) {
  const [value, setValue] = useState('');
  const textareaRef = useRef<HTMLTextAreaElement>(null);

  // Auto-grow the textarea up to a max height, then scroll internally.
  useLayoutEffect(() => {
    const textarea = textareaRef.current;
    if (!textarea) {
      return;
    }
    textarea.style.height = 'auto';
    textarea.style.height = `${Math.min(textarea.scrollHeight, 200)}px`;
  }, [value]);

  const trimmed = value.trim();
  const canSend = trimmed.length > 0 && !disabled && !isStreaming;

  const submit = () => {
    if (!canSend) {
      return;
    }
    onSend(trimmed);
    setValue('');
  };

  const handleKeyDown = (event: React.KeyboardEvent<HTMLTextAreaElement>) => {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      submit();
    }
  };

  return (
    <div className="border-t border-border bg-background p-4">
      {disabled && disabledReason && (
        <p className="mb-2 text-xs text-text-muted" role="note">
          {disabledReason}
        </p>
      )}
      <div
        className={cn(
          'flex items-end gap-2 rounded-2xl border border-border bg-surface px-3 py-2 focus-within:ring-2 focus-within:ring-ring',
          disabled && 'opacity-60',
        )}
      >
        <textarea
          ref={textareaRef}
          value={value}
          onChange={(event) => setValue(event.target.value.slice(0, MAX_MESSAGE_LENGTH))}
          onKeyDown={handleKeyDown}
          disabled={disabled}
          rows={1}
          aria-label="Message"
          placeholder={placeholder ?? 'Ask a question about the knowledge base…'}
          className="max-h-[200px] min-h-[24px] flex-1 resize-none bg-transparent text-sm text-text-primary outline-none placeholder:text-text-muted disabled:cursor-not-allowed"
        />

        {isStreaming ? (
          <Button
            type="button"
            size="sm"
            variant="outline"
            onClick={onStop}
            aria-label="Stop generating"
          >
            <Square className="h-4 w-4" />
          </Button>
        ) : (
          <Button
            type="button"
            size="sm"
            onClick={submit}
            disabled={!canSend}
            aria-label="Send message"
          >
            <ArrowUp className="h-4 w-4" />
          </Button>
        )}
      </div>
      <div className="mt-1 flex justify-between px-1">
        <span className="text-[11px] text-text-muted">
          Enter to send · Shift+Enter for a new line
        </span>
        {value.length > MAX_MESSAGE_LENGTH * 0.75 && (
          <span className="text-[11px] text-text-muted">
            {value.length}/{MAX_MESSAGE_LENGTH}
          </span>
        )}
      </div>
    </div>
  );
}
