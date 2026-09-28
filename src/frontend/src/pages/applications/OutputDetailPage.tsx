import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { format } from 'date-fns';
import { DownloadIcon, FileOutput, PencilIcon, RefreshCwIcon, TrashIcon } from 'lucide-react';
import { useNavigate, useParams } from 'react-router-dom';
import { toast } from 'sonner';

import {
  deleteOutput,
  editOutput,
  getOutputDetail,
  outputsQueryKeys,
  regenerateOutput,
} from '@/api/outputs';
import { ApiError } from '@/api/problemDetails';
import { useCapabilities } from '@/app/authz/useCapabilities';
import { ConfirmDialog } from '@/components/dialogs/ConfirmDialog';
import { CodeEditor } from '@/components/editor/CodeEditor';
import { OutputWorkspace } from '@/components/output/OutputWorkspace';
import { PageHeader } from '@/components/patterns/PageHeader';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { buildCitationMarkdown } from '@/lib/citationMarkdown';

function errorMessage(error: unknown, fallback: string): string {
  return error instanceof ApiError ? (error.problem.detail ?? error.problem.title) : fallback;
}

function downloadMarkdown(title: string, markdown: string): void {
  const blob = new Blob([markdown], { type: 'text/markdown;charset=utf-8' });
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = `${title.trim() || 'output'}.md`;
  document.body.appendChild(anchor);
  anchor.click();
  anchor.remove();
  URL.revokeObjectURL(url);
}

/**
 * TASK-OUT-02/03/04 + TASK-CIT-04: `/applications/:applicationId/outputs/:outputId` -
 * the generated-output detail view (evidence-first Markdown workspace,
 * citation drawer, admin edit/regenerate/delete, and copy/download for every
 * viewer). Read access is `canViewOutput`; edit/regenerate/delete require
 * `canManageOutput` (server-enforced regardless of what the UI shows).
 */
export default function OutputDetailPage() {
  const { applicationId, outputId } = useParams<{ applicationId: string; outputId: string }>();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const capabilities = useCapabilities(applicationId);

  const [isEditing, setIsEditing] = useState(false);
  const [draft, setDraft] = useState('');
  const [pendingAction, setPendingAction] = useState<'regenerate' | 'delete' | null>(null);

  const outputQuery = useQuery({
    queryKey: outputsQueryKeys.detail(applicationId ?? '', outputId ?? ''),
    queryFn: () => getOutputDetail(applicationId ?? '', outputId ?? ''),
    enabled: Boolean(applicationId) && Boolean(outputId),
  });

  function invalidate() {
    return Promise.all([
      queryClient.invalidateQueries({
        queryKey: outputsQueryKeys.detail(applicationId ?? '', outputId ?? ''),
      }),
      queryClient.invalidateQueries({ queryKey: outputsQueryKeys.list(applicationId ?? '') }),
    ]);
  }

  const editMutation = useMutation({
    mutationFn: (currentMarkdown: string) =>
      editOutput(applicationId ?? '', outputId ?? '', currentMarkdown),
    onSuccess: async () => {
      await invalidate();
      setIsEditing(false);
      toast.success('Output saved');
    },
    onError: (error) => toast.error(errorMessage(error, 'Failed to save output')),
  });

  const regenerateMutation = useMutation({
    mutationFn: () => regenerateOutput(applicationId ?? '', outputId ?? ''),
    onSuccess: (result) => {
      setPendingAction(null);
      toast.success('Regeneration started');
      navigate(`/applications/${applicationId}/runs/${result.runId}`);
    },
    onError: (error) => {
      setPendingAction(null);
      toast.error(errorMessage(error, 'Failed to start regeneration'));
    },
  });

  const deleteMutation = useMutation({
    mutationFn: () => deleteOutput(applicationId ?? '', outputId ?? ''),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: outputsQueryKeys.list(applicationId ?? '') });
      toast.success('Output deleted');
      navigate(`/applications/${applicationId}/outputs`);
    },
    onError: (error) => {
      setPendingAction(null);
      toast.error(errorMessage(error, 'Failed to delete output'));
    },
  });

  const output = outputQuery.data;

  const workspaceContent = useMemo(() => {
    if (!output) {
      return { markdown: '', sources: [] };
    }
    return buildCitationMarkdown(output.currentMarkdown, output.citations);
  }, [output]);

  if (outputQuery.isLoading) {
    return (
      <section aria-label="Output detail" className="space-y-4">
        <Skeleton className="h-8 w-64" />
        <Skeleton className="h-96 w-full" />
      </section>
    );
  }

  if (outputQuery.isError || !output) {
    const notFound = outputQuery.error instanceof ApiError && outputQuery.error.status === 404;
    return (
      <section aria-label="Output detail">
        <p role="alert" className="text-sm text-danger">
          {notFound
            ? "This output doesn't exist or you don't have access to it."
            : errorMessage(outputQuery.error, 'Failed to load output')}
        </p>
      </section>
    );
  }

  const isMutating =
    editMutation.isPending || regenerateMutation.isPending || deleteMutation.isPending;

  return (
    <section aria-labelledby="output-heading" className="flex h-full flex-col gap-4">
      <PageHeader
        eyebrow="Output"
        icon={FileOutput}
        title={output.title}
        meta={
          <>
            {output.isEdited && <Badge variant="outline">Edited</Badge>}
            <span className="text-sm text-text-muted">
              {output.agentName} · Created by {output.createdBy} on{' '}
              {format(new Date(output.createdAt), 'PP')}
              {output.lastEditedAt &&
                ` · Last edited by ${output.lastEditedBy} on ${format(new Date(output.lastEditedAt), 'PP')}`}
            </span>
          </>
        }
        actions={
          <>
            <Button
              variant="outline"
              size="sm"
              onClick={async () => {
                await navigator.clipboard.writeText(output.currentMarkdown);
                toast.success('Copied to clipboard');
              }}
            >
              Copy
            </Button>
            <Button
              variant="outline"
              size="sm"
              onClick={() => downloadMarkdown(output.title, output.currentMarkdown)}
            >
              <DownloadIcon />
              Download
            </Button>
            {capabilities.canManageOutput && !isEditing && (
              <Button
                variant="outline"
                size="sm"
                onClick={() => {
                  setDraft(output.currentMarkdown);
                  setIsEditing(true);
                }}
              >
                <PencilIcon />
                Edit
              </Button>
            )}
            {capabilities.canManageOutput && (
              <Button
                variant="outline"
                size="sm"
                onClick={() => setPendingAction('regenerate')}
                disabled={isMutating}
              >
                <RefreshCwIcon />
                Regenerate
              </Button>
            )}
            {capabilities.canManageOutput && (
              <Button
                variant="destructive"
                size="sm"
                onClick={() => setPendingAction('delete')}
                disabled={isMutating}
              >
                <TrashIcon />
                Delete
              </Button>
            )}
          </>
        }
      />

      {isEditing ? (
        <div className="flex h-96 flex-col gap-3">
          <CodeEditor
            value={draft}
            onChange={setDraft}
            language="markdown"
            ariaLabel="Edit output Markdown"
          />
          <div className="flex justify-end gap-2">
            <Button
              variant="outline"
              size="sm"
              onClick={() => {
                setDraft(output.currentMarkdown);
                setIsEditing(false);
              }}
              disabled={editMutation.isPending}
            >
              Cancel
            </Button>
            <Button
              size="sm"
              onClick={() => editMutation.mutate(draft)}
              disabled={editMutation.isPending || draft.trim().length === 0}
            >
              Save
            </Button>
          </div>
        </div>
      ) : (
        <OutputWorkspace content={workspaceContent.markdown} sources={workspaceContent.sources} />
      )}

      <ConfirmDialog
        open={pendingAction !== null}
        onOpenChange={(open) => !open && setPendingAction(null)}
        title={pendingAction === 'regenerate' ? 'Regenerate output?' : 'Delete output?'}
        description={
          pendingAction === 'regenerate'
            ? "A new run is queued using the original request against the agent's current configuration. This output is left untouched."
            : 'This output will be deleted. This cannot be undone from the UI.'
        }
        confirmLabel={pendingAction === 'regenerate' ? 'Regenerate' : 'Delete'}
        destructive={pendingAction === 'delete'}
        isPending={isMutating}
        onConfirm={() => {
          if (pendingAction === 'regenerate') {
            regenerateMutation.mutate();
          } else if (pendingAction === 'delete') {
            deleteMutation.mutate();
          }
        }}
      />
    </section>
  );
}
