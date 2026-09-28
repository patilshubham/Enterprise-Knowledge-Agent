import { useState } from 'react';
import { MessageSquarePlus, MoreHorizontal, Pencil, Trash2 } from 'lucide-react';

import type { ConversationSummaryResponse } from '@/api/chat';
import { ConfirmDialog } from '@/components/dialogs/ConfirmDialog';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import { cn } from '@/lib/utils';

export interface ConversationSidebarProps {
  conversations: ConversationSummaryResponse[];
  activeConversationId?: string;
  isLoading?: boolean;
  currentUserId?: string | null;
  /** Admins may rename/delete any conversation; other users only their own. */
  canManageAll?: boolean;
  onSelect: (conversationId: string) => void;
  onNewChat: () => void;
  onRename: (conversationId: string, title: string) => void;
  onDelete: (conversationId: string) => void;
  renamePending?: boolean;
  deletePending?: boolean;
}

function formatRelative(iso: string): string {
  const date = new Date(iso);
  const diffMs = Date.now() - date.getTime();
  const diffMinutes = Math.round(diffMs / 60_000);
  if (diffMinutes < 1) {
    return 'just now';
  }
  if (diffMinutes < 60) {
    return `${diffMinutes}m ago`;
  }
  const diffHours = Math.round(diffMinutes / 60);
  if (diffHours < 24) {
    return `${diffHours}h ago`;
  }
  const diffDays = Math.round(diffHours / 24);
  if (diffDays < 7) {
    return `${diffDays}d ago`;
  }
  return date.toLocaleDateString();
}

export function ConversationSidebar({
  conversations,
  activeConversationId,
  isLoading,
  currentUserId,
  canManageAll = false,
  onSelect,
  onNewChat,
  onRename,
  onDelete,
  renamePending,
  deletePending,
}: ConversationSidebarProps) {
  const [renameTarget, setRenameTarget] = useState<ConversationSummaryResponse | null>(null);
  const [renameValue, setRenameValue] = useState('');
  const [deleteTarget, setDeleteTarget] = useState<ConversationSummaryResponse | null>(null);

  const canManage = (conversation: ConversationSummaryResponse) =>
    canManageAll || conversation.createdBy === currentUserId;

  const openRename = (conversation: ConversationSummaryResponse) => {
    setRenameTarget(conversation);
    setRenameValue(conversation.title);
  };

  const submitRename = () => {
    const title = renameValue.trim();
    if (renameTarget && title.length > 0) {
      onRename(renameTarget.id, title);
      setRenameTarget(null);
    }
  };

  return (
    <aside className="flex h-full w-72 shrink-0 flex-col border-r border-border bg-surface">
      <div className="p-3">
        <Button className="w-full justify-start" onClick={onNewChat}>
          <MessageSquarePlus className="h-4 w-4" />
          New chat
        </Button>
      </div>

      <nav className="min-h-0 flex-1 overflow-y-auto px-2 pb-3" aria-label="Conversations">
        {isLoading ? (
          <div className="space-y-2 p-1">
            <Skeleton className="h-12 w-full" />
            <Skeleton className="h-12 w-full" />
            <Skeleton className="h-12 w-full" />
          </div>
        ) : conversations.length === 0 ? (
          <p className="px-3 py-6 text-center text-sm text-text-muted">
            No conversations yet. Start a new chat to ask about the knowledge base.
          </p>
        ) : (
          <ul className="space-y-1">
            {conversations.map((conversation) => {
              const isActive = conversation.id === activeConversationId;
              return (
                <li key={conversation.id}>
                  <div
                    className={cn(
                      'group flex items-center gap-1 rounded-lg px-2 py-2 transition-colors',
                      isActive ? 'bg-accent/10' : 'hover:bg-surface-raised',
                    )}
                  >
                    <button
                      type="button"
                      onClick={() => onSelect(conversation.id)}
                      className="min-w-0 flex-1 text-left"
                      aria-current={isActive ? 'true' : undefined}
                    >
                      <span className="block truncate text-sm font-medium text-text-primary">
                        {conversation.title}
                      </span>
                      <span className="block truncate text-xs text-text-muted">
                        {formatRelative(conversation.lastMessageAt)} · {conversation.messageCount}{' '}
                        {conversation.messageCount === 1 ? 'message' : 'messages'}
                      </span>
                    </button>

                    {canManage(conversation) && (
                      <DropdownMenu>
                        <DropdownMenuTrigger asChild>
                          <Button
                            variant="ghost"
                            size="sm"
                            className="h-7 w-7 shrink-0 p-0 opacity-0 group-hover:opacity-100 data-[state=open]:opacity-100"
                            aria-label={`Conversation options for ${conversation.title}`}
                          >
                            <MoreHorizontal className="h-4 w-4" />
                          </Button>
                        </DropdownMenuTrigger>
                        <DropdownMenuContent align="end">
                          <DropdownMenuItem onSelect={() => openRename(conversation)}>
                            <Pencil className="h-4 w-4" />
                            Rename
                          </DropdownMenuItem>
                          <DropdownMenuItem
                            className="text-danger focus:text-danger"
                            onSelect={() => setDeleteTarget(conversation)}
                          >
                            <Trash2 className="h-4 w-4" />
                            Delete
                          </DropdownMenuItem>
                        </DropdownMenuContent>
                      </DropdownMenu>
                    )}
                  </div>
                </li>
              );
            })}
          </ul>
        )}
      </nav>

      <Dialog open={renameTarget !== null} onOpenChange={(open) => !open && setRenameTarget(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Rename conversation</DialogTitle>
            <DialogDescription>Give this conversation a clearer title.</DialogDescription>
          </DialogHeader>
          <Input
            value={renameValue}
            onChange={(event) => setRenameValue(event.target.value)}
            maxLength={200}
            aria-label="Conversation title"
            onKeyDown={(event) => {
              if (event.key === 'Enter') {
                event.preventDefault();
                submitRename();
              }
            }}
          />
          <DialogFooter>
            <Button
              variant="outline"
              onClick={() => setRenameTarget(null)}
              disabled={renamePending}
            >
              Cancel
            </Button>
            <Button
              onClick={submitRename}
              disabled={renamePending || renameValue.trim().length === 0}
            >
              Save
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <ConfirmDialog
        open={deleteTarget !== null}
        onOpenChange={(open) => !open && setDeleteTarget(null)}
        title="Delete conversation"
        description="This conversation will be removed for everyone. This action cannot be undone."
        confirmLabel="Delete"
        destructive
        isPending={deletePending}
        onConfirm={() => {
          if (deleteTarget) {
            onDelete(deleteTarget.id);
            setDeleteTarget(null);
          }
        }}
      />
    </aside>
  );
}
