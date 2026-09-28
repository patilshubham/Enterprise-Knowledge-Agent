import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { createColumnHelper } from '@tanstack/react-table';
import { format } from 'date-fns';
import { BanIcon, CheckIcon, FolderKanban, RefreshCwIcon, TrashIcon, UploadIcon } from 'lucide-react';
import { Link, useParams } from 'react-router-dom';
import { toast } from 'sonner';
import {
  deleteKnowledgeFile,
  disableKnowledgeFile,
  enableKnowledgeFile,
  knowledgeFilesQueryKeys,
  listKnowledgeFiles,
  reprocessKnowledgeFile,
  uploadKnowledgeFiles,
  type KnowledgeFileResponse,
  type KnowledgeFileStatus,
  type UploadFileResult,
} from '@/api/knowledgeFiles';
import { ApiError } from '@/api/problemDetails';
import { useCapabilities } from '@/app/authz/useCapabilities';
import { useJoinApplicationGroup } from '@/app/realtime/useRealtimeGroups';
import { ConfirmDialog } from '@/components/dialogs/ConfirmDialog';
import { KnowledgeFileUploadDialog } from '@/components/knowledge/KnowledgeFileUploadDialog';
import { PageHeader } from '@/components/patterns/PageHeader';
import { Button } from '@/components/ui/button';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { DataTable, type DataTableColumnDef } from '@/components/data-table/DataTable';
import type { DataTableRowAction } from '@/components/data-table/DataTableRowActions';
import type { DataTableFeatures } from '@/components/data-table/table-features';
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

const ACTIVE_STATUSES: KnowledgeFileStatus[] = [0, 1, 5];

type PendingAction = {
  type: 'disable' | 'enable' | 'reprocess' | 'delete';
  file: KnowledgeFileResponse;
} | null;

function errorMessage(error: unknown, fallback: string): string {
  return error instanceof ApiError ? (error.problem.detail ?? error.problem.title) : fallback;
}

function summarizeUploadResults(results: UploadFileResult[]): string {
  const accepted = results.filter((r) => r.outcome === 0).length;
  const rest = results.length - accepted;
  if (rest === 0) {
    return `${accepted} file${accepted === 1 ? '' : 's'} uploaded`;
  }
  return `${accepted} accepted, ${rest} skipped - see details in the dialog`;
}

/**
 * TASK-KNW-06: knowledge files list (`/applications/:applicationId/knowledge`).
 * Viewable by any application member (server-enforced `CanViewApplication`);
 * upload/disable/enable/reprocess/delete are further gated to
 * `canManageKnowledge` (SuperAdmin/ApplicationAdmin). Joins the
 * `application:{id}` SignalR group (TASK-SIG-05/06, Module 25) so
 * FileQueued/FileStageChanged/FileCompleted/FileFailed events invalidate
 * this list live; the short polling interval below is kept as a resilience
 * fallback for the (rare) window before the socket reconnects.
 */
export default function ApplicationKnowledgePage() {
  const { applicationId } = useParams<{ applicationId: string }>();
  const queryClient = useQueryClient();
  const capabilities = useCapabilities(applicationId);

  useJoinApplicationGroup(applicationId);

  const [statusFilter, setStatusFilter] = useState<KnowledgeFileStatus | 'all'>('all');
  const [isUploadOpen, setIsUploadOpen] = useState(false);
  const [uploadResults, setUploadResults] = useState<UploadFileResult[] | null>(null);
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

  const filteredFiles = useMemo(() => {
    const files = filesQuery.data ?? [];
    return statusFilter === 'all' ? files : files.filter((file) => file.status === statusFilter);
  }, [filesQuery.data, statusFilter]);

  function invalidateList() {
    return queryClient.invalidateQueries({
      queryKey: knowledgeFilesQueryKeys.list(applicationId ?? ''),
    });
  }

  const uploadMutation = useMutation({
    mutationFn: (files: File[]) => uploadKnowledgeFiles(applicationId ?? '', files),
    onSuccess: async (results) => {
      await invalidateList();
      setUploadResults(results);
      toast.success(summarizeUploadResults(results));
    },
    onError: (error) => toast.error(errorMessage(error, 'Failed to upload files')),
  });

  const disableMutation = useMutation({
    mutationFn: (fileId: string) => disableKnowledgeFile(applicationId ?? '', fileId),
    onSuccess: async () => {
      await invalidateList();
      setPendingAction(null);
      toast.success('File disabled');
    },
    onError: (error) => toast.error(errorMessage(error, 'Failed to disable file')),
  });

  const enableMutation = useMutation({
    mutationFn: (fileId: string) => enableKnowledgeFile(applicationId ?? '', fileId),
    onSuccess: async () => {
      await invalidateList();
      setPendingAction(null);
      toast.success('File enabled');
    },
    onError: (error) => toast.error(errorMessage(error, 'Failed to enable file')),
  });

  const reprocessMutation = useMutation({
    mutationFn: (fileId: string) => reprocessKnowledgeFile(applicationId ?? '', fileId),
    onSuccess: async () => {
      await invalidateList();
      setPendingAction(null);
      toast.success('File queued for reprocessing');
    },
    onError: (error) => toast.error(errorMessage(error, 'Failed to reprocess file')),
  });

  const deleteMutation = useMutation({
    mutationFn: (fileId: string) => deleteKnowledgeFile(applicationId ?? '', fileId),
    onSuccess: async () => {
      await invalidateList();
      setPendingAction(null);
      toast.success('File deleted');
    },
    onError: (error) => toast.error(errorMessage(error, 'Failed to delete file')),
  });

  const columnHelper = useMemo(
    () => createColumnHelper<DataTableFeatures, KnowledgeFileResponse>(),
    [],
  );

  const columns: DataTableColumnDef<KnowledgeFileResponse>[] = useMemo(
    () => [
      columnHelper.accessor('displayName', {
        header: 'Name',
        cell: (info) => (
          <Link
            to={`/applications/${applicationId}/knowledge/${info.row.original.id}`}
            className="font-medium text-text-primary hover:underline"
            onClick={(event) => event.stopPropagation()}
          >
            {info.getValue()}
          </Link>
        ),
      }),
      columnHelper.accessor('status', {
        header: 'Status',
        cell: (info) => {
          const file = info.row.original;
          const label =
            file.status === 1 && file.progress !== null
              ? `Processing (${file.progress}%)`
              : STATUS_LABEL_BY_FILE_STATUS[file.status];
          return <StatusBadge status={STATUS_KIND_BY_FILE_STATUS[file.status]} label={label} />;
        },
      }),
      columnHelper.accessor('fileSize', {
        header: 'Size',
        cell: (info) => formatFileSize(info.getValue()),
      }),
      columnHelper.accessor('mimeType', {
        header: 'Type',
      }),
      columnHelper.accessor('uploadedAt', {
        header: 'Uploaded',
        cell: (info) => format(new Date(info.getValue()), 'PP'),
      }),
    ],
    [columnHelper, applicationId],
  );

  function rowActions(file: KnowledgeFileResponse): DataTableRowAction[] {
    if (!capabilities.canManageKnowledge) {
      return [];
    }

    const actions: DataTableRowAction[] = [];
    if (file.isEnabled && file.status !== 5) {
      actions.push({
        id: 'disable',
        label: 'Disable',
        icon: <BanIcon />,
        onSelect: () => setPendingAction({ type: 'disable', file }),
      });
    }
    if (!file.isEnabled && file.status !== 5) {
      actions.push({
        id: 'enable',
        label: 'Enable',
        icon: <CheckIcon />,
        onSelect: () => setPendingAction({ type: 'enable', file }),
      });
    }
    if (file.status === 2 || file.status === 3) {
      actions.push({
        id: 'reprocess',
        label: 'Reprocess',
        icon: <RefreshCwIcon />,
        onSelect: () => setPendingAction({ type: 'reprocess', file }),
      });
    }
    if (file.status !== 5) {
      actions.push({
        id: 'delete',
        label: 'Delete',
        icon: <TrashIcon />,
        destructive: true,
        onSelect: () => setPendingAction({ type: 'delete', file }),
      });
    }
    return actions;
  }

  const isMutating =
    disableMutation.isPending ||
    enableMutation.isPending ||
    reprocessMutation.isPending ||
    deleteMutation.isPending;

  return (
    <div aria-labelledby="knowledge-files-heading" className="mx-auto max-w-6xl space-y-6">
      <PageHeader
        eyebrow="Knowledge"
        icon={FolderKanban}
        title="Knowledge files"
        description="Documents this application can retrieve from. Upload files, then agents ground their answers in them."
        actions={
          capabilities.canManageKnowledge && (
            <Button
              onClick={() => {
                setUploadResults(null);
                setIsUploadOpen(true);
              }}
            >
              <UploadIcon />
              Upload
            </Button>
          )
        }
      />

      {filesQuery.isError ? (
        <p role="alert" className="text-sm text-danger">
          {errorMessage(filesQuery.error, 'Failed to load knowledge files')}
        </p>
      ) : (
        <DataTable
          columns={columns}
          data={filteredFiles}
          isLoading={filesQuery.isLoading}
          getRowId={(row) => row.id}
          searchPlaceholder="Search files..."
          emptyState={
            capabilities.canManageKnowledge
              ? 'No knowledge files yet - upload one to get started.'
              : 'No knowledge files to show.'
          }
          rowActions={rowActions}
          toolbarFilters={
            <Select
              value={String(statusFilter)}
              onValueChange={(value) =>
                setStatusFilter(value === 'all' ? 'all' : (Number(value) as KnowledgeFileStatus))
              }
            >
              <SelectTrigger size="sm" aria-label="Filter by status">
                <SelectValue placeholder="All statuses" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All statuses</SelectItem>
                {([0, 1, 2, 3, 4, 5] as KnowledgeFileStatus[]).map((status) => (
                  <SelectItem key={status} value={String(status)}>
                    {STATUS_LABEL_BY_FILE_STATUS[status]}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          }
        />
      )}

      <KnowledgeFileUploadDialog
        open={isUploadOpen}
        onOpenChange={setIsUploadOpen}
        onUpload={(files) => uploadMutation.mutate(files)}
        isPending={uploadMutation.isPending}
        results={uploadResults}
      />

      <ConfirmDialog
        open={pendingAction !== null}
        onOpenChange={(open) => !open && setPendingAction(null)}
        title={
          pendingAction?.type === 'disable'
            ? 'Disable file?'
            : pendingAction?.type === 'enable'
              ? 'Enable file?'
              : pendingAction?.type === 'reprocess'
                ? 'Reprocess file?'
                : 'Delete file?'
        }
        description={
          pendingAction?.type === 'disable'
            ? `"${pendingAction.file.displayName}" is excluded from retrieval but remains visible to admins.`
            : pendingAction?.type === 'enable'
              ? `"${pendingAction.file.displayName}" becomes eligible for retrieval again.`
              : pendingAction?.type === 'reprocess'
                ? `"${pendingAction.file.displayName}" will be re-processed from its original source; prior chunks will be regenerated.`
                : `"${pendingAction?.file.displayName}" will be deleted. Historical outputs remain viewable, but its content will no longer be retrievable.`
        }
        confirmLabel={
          pendingAction?.type === 'disable'
            ? 'Disable'
            : pendingAction?.type === 'enable'
              ? 'Enable'
              : pendingAction?.type === 'reprocess'
                ? 'Reprocess'
                : 'Delete'
        }
        destructive={pendingAction?.type === 'delete' || pendingAction?.type === 'disable'}
        isPending={isMutating}
        onConfirm={() => {
          if (!pendingAction) {
            return;
          }
          const { type, file } = pendingAction;
          if (type === 'disable') {
            disableMutation.mutate(file.id);
          } else if (type === 'enable') {
            enableMutation.mutate(file.id);
          } else if (type === 'reprocess') {
            reprocessMutation.mutate(file.id);
          } else {
            deleteMutation.mutate(file.id);
          }
        }}
      />
    </div>
  );
}
