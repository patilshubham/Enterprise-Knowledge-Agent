import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { format } from 'date-fns';
import { BanIcon, CheckIcon, FileText, RefreshCwIcon, TrashIcon } from 'lucide-react';
import { useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { toast } from 'sonner';
import {
  deleteKnowledgeFile,
  disableKnowledgeFile,
  enableKnowledgeFile,
  knowledgeFilesQueryKeys,
  listKnowledgeFiles,
  previewKnowledgeFile,
  processingStageName,
  reprocessKnowledgeFile,
  type KnowledgeFileStatus,
} from '@/api/knowledgeFiles';
import { ApiError } from '@/api/problemDetails';
import { useCapabilities } from '@/app/authz/useCapabilities';
import { ConfirmDialog } from '@/components/dialogs/ConfirmDialog';
import { SafeMarkdown } from '@/components/markdown/SafeMarkdown';
import { PageHeader } from '@/components/patterns/PageHeader';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Progress } from '@/components/ui/progress';
import { Skeleton } from '@/components/ui/skeleton';
import { StatusBadge } from '@/components/status/StatusBadge';
import type { StatusKind } from '@/components/status/status-tokens';
import { formatFileSize } from '@/lib/utils';

const STATUS_KIND_BY_FILE_STATUS: Record<KnowledgeFileStatus, StatusKind> = {
  0: 'queued',
  1: 'processing',
  2: 'ready',
  3: 'failed',
  4: 'disabled',
  5: 'processing',
  6: 'disabled',
};

const STATUS_LABEL_BY_FILE_STATUS: Record<KnowledgeFileStatus, string> = {
  0: 'Queued',
  1: 'Processing',
  2: 'Ready',
  3: 'Failed',
  4: 'Disabled',
  5: 'Deleting',
  6: 'Deleted',
};

const PREVIEWABLE_STATUSES: KnowledgeFileStatus[] = [2, 4];
const ACTIVE_STATUSES: KnowledgeFileStatus[] = [0, 1, 5];

type PendingAction = 'disable' | 'enable' | 'reprocess' | 'delete' | null;

function errorMessage(error: unknown, fallback: string): string {
  return error instanceof ApiError ? (error.problem.detail ?? error.problem.title) : fallback;
}

/**
 * TASK-KNW-06: single knowledge file detail
 * (`/applications/:applicationId/knowledge/:fileId`). There is no dedicated
 * `GET .../files/{id}` endpoint (TASK-KNW-04 only specifies list + preview),
 * so this page shares the list page's query cache and looks up the file by
 * id - consistent with the backend's intentionally list-derived design.
 */
export default function KnowledgeFileDetailPage() {
  const { applicationId, fileId } = useParams<{ applicationId: string; fileId: string }>();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const capabilities = useCapabilities(applicationId);
  const [pendingAction, setPendingAction] = useState<PendingAction>(null);

  const filesQuery = useQuery({
    queryKey: knowledgeFilesQueryKeys.list(applicationId ?? ''),
    queryFn: () => listKnowledgeFiles(applicationId ?? ''),
    enabled: Boolean(applicationId),
    refetchInterval: (query) => {
      const files = query.state.data;
      return files?.some((file) => ACTIVE_STATUSES.includes(file.status)) ? 3000 : false;
    },
  });

  const file = filesQuery.data?.find((candidate) => candidate.id === fileId);

  const previewQuery = useQuery({
    queryKey: knowledgeFilesQueryKeys.preview(applicationId ?? '', fileId ?? ''),
    queryFn: () => previewKnowledgeFile(applicationId ?? '', fileId ?? ''),
    enabled:
      Boolean(applicationId) &&
      Boolean(fileId) &&
      Boolean(file) &&
      PREVIEWABLE_STATUSES.includes(file!.status),
  });

  function invalidateList() {
    return queryClient.invalidateQueries({
      queryKey: knowledgeFilesQueryKeys.list(applicationId ?? ''),
    });
  }

  const disableMutation = useMutation({
    mutationFn: () => disableKnowledgeFile(applicationId ?? '', fileId ?? ''),
    onSuccess: async () => {
      await invalidateList();
      setPendingAction(null);
      toast.success('File disabled');
    },
    onError: (error) => toast.error(errorMessage(error, 'Failed to disable file')),
  });

  const enableMutation = useMutation({
    mutationFn: () => enableKnowledgeFile(applicationId ?? '', fileId ?? ''),
    onSuccess: async () => {
      await invalidateList();
      setPendingAction(null);
      toast.success('File enabled');
    },
    onError: (error) => toast.error(errorMessage(error, 'Failed to enable file')),
  });

  const reprocessMutation = useMutation({
    mutationFn: () => reprocessKnowledgeFile(applicationId ?? '', fileId ?? ''),
    onSuccess: async () => {
      await invalidateList();
      setPendingAction(null);
      toast.success('File queued for reprocessing');
    },
    onError: (error) => toast.error(errorMessage(error, 'Failed to reprocess file')),
  });

  const deleteMutation = useMutation({
    mutationFn: () => deleteKnowledgeFile(applicationId ?? '', fileId ?? ''),
    onSuccess: async () => {
      await invalidateList();
      toast.success('File deleted');
      navigate(`/applications/${applicationId}/knowledge`);
    },
    onError: (error) => toast.error(errorMessage(error, 'Failed to delete file')),
  });

  if (filesQuery.isLoading) {
    return (
      <section aria-label="Knowledge file detail" className="space-y-4">
        <Skeleton className="h-8 w-64" />
        <Skeleton className="h-40 w-full" />
      </section>
    );
  }

  if (filesQuery.isError) {
    return (
      <section aria-label="Knowledge file detail">
        <p role="alert" className="text-sm text-danger">
          {errorMessage(filesQuery.error, 'Failed to load knowledge file')}
        </p>
      </section>
    );
  }

  if (!file) {
    return (
      <section aria-label="Knowledge file detail">
        <p role="alert" className="text-sm text-danger">
          This file doesn&apos;t exist or you don&apos;t have access to it.
        </p>
      </section>
    );
  }

  const isMutating =
    disableMutation.isPending ||
    enableMutation.isPending ||
    reprocessMutation.isPending ||
    deleteMutation.isPending;

  return (
    <div aria-labelledby="knowledge-file-heading" className="mx-auto max-w-6xl space-y-6">
      <PageHeader
        eyebrow="Knowledge file"
        icon={FileText}
        title={file.displayName}
        description={`${file.mimeType} · ${formatFileSize(file.fileSize)}`}
        actions={
          capabilities.canManageKnowledge &&
          file.status !== 5 && (
            <>
              {file.isEnabled ? (
                <Button variant="outline" size="sm" onClick={() => setPendingAction('disable')}>
                  <BanIcon />
                  Disable
                </Button>
              ) : (
                <Button variant="outline" size="sm" onClick={() => setPendingAction('enable')}>
                  <CheckIcon />
                  Enable
                </Button>
              )}
              {(file.status === 2 || file.status === 3) && (
                <Button variant="outline" size="sm" onClick={() => setPendingAction('reprocess')}>
                  <RefreshCwIcon />
                  Reprocess
                </Button>
              )}
              <Button variant="destructive" size="sm" onClick={() => setPendingAction('delete')}>
                <TrashIcon />
                Delete
              </Button>
            </>
          )
        }
      />

      <Card>
        <CardHeader>
          <CardTitle>Status</CardTitle>
          <CardDescription>Lifecycle and processing state for this file.</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4 px-6 text-sm">
          <div className="flex flex-wrap items-center gap-6">
            <div className="flex items-center gap-2">
              <span className="text-text-muted">Status</span>
              <StatusBadge
                status={STATUS_KIND_BY_FILE_STATUS[file.status]}
                label={STATUS_LABEL_BY_FILE_STATUS[file.status]}
              />
            </div>
            <div className="flex items-center gap-2">
              <span className="text-text-muted">Uploaded</span>
              <span>{format(new Date(file.uploadedAt), 'PPp')}</span>
            </div>
            {file.lastProcessedAt && (
              <div className="flex items-center gap-2">
                <span className="text-text-muted">Last processed</span>
                <span>{format(new Date(file.lastProcessedAt), 'PPp')}</span>
              </div>
            )}
          </div>
          {file.status === 1 && file.stage !== null && (
            <div className="space-y-1">
              <div className="flex items-center justify-between text-text-muted">
                <span>{processingStageName(file.stage)}</span>
                <span>{file.progress ?? 0}%</span>
              </div>
              <Progress value={file.progress ?? 0} />
            </div>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Preview</CardTitle>
          <CardDescription>Readable source content (FR-3.4).</CardDescription>
        </CardHeader>
        <CardContent className="px-6">
          {!PREVIEWABLE_STATUSES.includes(file.status) ? (
            <p className="text-sm text-text-muted">
              Preview is available once the file has finished processing.
            </p>
          ) : previewQuery.isLoading ? (
            <Skeleton className="h-40 w-full" />
          ) : previewQuery.isError ? (
            <p role="alert" className="text-sm text-danger">
              {errorMessage(previewQuery.error, 'Failed to load preview')}
            </p>
          ) : (
            <div className="max-h-[32rem] overflow-y-auto rounded-control border border-border p-4">
              <SafeMarkdown content={previewQuery.data ?? ''} />
            </div>
          )}
        </CardContent>
      </Card>

      <ConfirmDialog
        open={pendingAction !== null}
        onOpenChange={(open) => !open && setPendingAction(null)}
        title={
          pendingAction === 'disable'
            ? 'Disable file?'
            : pendingAction === 'enable'
              ? 'Enable file?'
              : pendingAction === 'reprocess'
                ? 'Reprocess file?'
                : 'Delete file?'
        }
        description={
          pendingAction === 'disable'
            ? `"${file.displayName}" is excluded from retrieval but remains visible to admins.`
            : pendingAction === 'enable'
              ? `"${file.displayName}" becomes eligible for retrieval again.`
              : pendingAction === 'reprocess'
                ? `"${file.displayName}" will be re-processed from its original source; prior chunks will be regenerated.`
                : `"${file.displayName}" will be deleted. Historical outputs remain viewable, but its content will no longer be retrievable.`
        }
        confirmLabel={
          pendingAction === 'disable'
            ? 'Disable'
            : pendingAction === 'enable'
              ? 'Enable'
              : pendingAction === 'reprocess'
                ? 'Reprocess'
                : 'Delete'
        }
        destructive={pendingAction === 'delete' || pendingAction === 'disable'}
        isPending={isMutating}
        onConfirm={() => {
          if (pendingAction === 'disable') {
            disableMutation.mutate();
          } else if (pendingAction === 'enable') {
            enableMutation.mutate();
          } else if (pendingAction === 'reprocess') {
            reprocessMutation.mutate();
          } else if (pendingAction === 'delete') {
            deleteMutation.mutate();
          }
        }}
      />
    </div>
  );
}
