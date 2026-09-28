import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { createColumnHelper } from '@tanstack/react-table';
import { format } from 'date-fns';
import { ArchiveRestoreIcon, ArchiveIcon, LayoutGrid, PencilIcon, PlusIcon, TrashIcon } from 'lucide-react';
import { Link } from 'react-router-dom';
import { toast } from 'sonner';
import {
  archiveApplication,
  applicationsQueryKeys,
  createApplication,
  deleteApplication,
  listApplications,
  restoreApplication,
  updateApplication,
  type ApplicationResponse,
} from '@/api/applications';
import { ApiError } from '@/api/problemDetails';
import { useCapabilities } from '@/app/authz/useCapabilities';
import {
  ApplicationFormDialog,
  type ApplicationFormValues,
} from '@/components/applications/ApplicationFormDialog';
import { ConfirmDialog } from '@/components/dialogs/ConfirmDialog';
import { PageHeader } from '@/components/patterns/PageHeader';
import { Button } from '@/components/ui/button';
import { DataTable, type DataTableColumnDef } from '@/components/data-table/DataTable';
import type { DataTableRowAction } from '@/components/data-table/DataTableRowActions';
import type { DataTableFeatures } from '@/components/data-table/table-features';
import { StatusBadge } from '@/components/status/StatusBadge';
import type { StatusKind } from '@/components/status/status-tokens';

const STATUS_KIND_BY_APPLICATION_STATUS: Record<ApplicationResponse['status'], StatusKind> = {
  Active: 'ready',
  Archived: 'disabled',
  Deleted: 'failed',
};

type PendingAction = {
  type: 'archive' | 'restore' | 'delete';
  application: ApplicationResponse;
} | null;

function toFormValues(application: ApplicationResponse): ApplicationFormValues {
  return {
    name: application.name,
    description: application.description ?? '',
    allowViewerExecution: application.allowViewerExecution,
  };
}

function errorMessage(error: unknown, fallback: string): string {
  return error instanceof ApiError ? (error.problem.detail ?? error.problem.title) : fallback;
}

/**
 * TASK-APP-03: platform-wide applications list (`/platform/applications`).
 * Visible to SuperAdmin/SuperViewer (nav-gated); the server independently
 * scopes the list per caller (FR-3.5) and enforces `CanManageApplications`
 * (SuperAdmin only) on every mutation regardless of what this page shows.
 */
export default function PlatformApplicationsPage() {
  const queryClient = useQueryClient();
  const capabilities = useCapabilities();
  const [isCreateOpen, setIsCreateOpen] = useState(false);
  const [editingApplication, setEditingApplication] = useState<ApplicationResponse | null>(null);
  const [pendingAction, setPendingAction] = useState<PendingAction>(null);

  const applicationsQuery = useQuery({
    queryKey: applicationsQueryKeys.list(),
    queryFn: listApplications,
  });

  function invalidateList() {
    return queryClient.invalidateQueries({ queryKey: applicationsQueryKeys.list() });
  }

  const createMutation = useMutation({
    mutationFn: createApplication,
    onSuccess: async () => {
      await invalidateList();
      setIsCreateOpen(false);
      toast.success('Application created');
    },
    onError: (error) => toast.error(errorMessage(error, 'Failed to create application')),
  });

  const updateMutation = useMutation({
    mutationFn: ({ id, values }: { id: string; values: ApplicationFormValues }) =>
      updateApplication(id, values),
    onSuccess: async () => {
      await invalidateList();
      setEditingApplication(null);
      toast.success('Application updated');
    },
    onError: (error) => toast.error(errorMessage(error, 'Failed to update application')),
  });

  const archiveMutation = useMutation({
    mutationFn: archiveApplication,
    onSuccess: async () => {
      await invalidateList();
      setPendingAction(null);
      toast.success('Application archived');
    },
    onError: (error) => toast.error(errorMessage(error, 'Failed to archive application')),
  });

  const restoreMutation = useMutation({
    mutationFn: restoreApplication,
    onSuccess: async () => {
      await invalidateList();
      setPendingAction(null);
      toast.success('Application restored');
    },
    onError: (error) => toast.error(errorMessage(error, 'Failed to restore application')),
  });

  const deleteMutation = useMutation({
    mutationFn: deleteApplication,
    onSuccess: async () => {
      await invalidateList();
      setPendingAction(null);
      toast.success('Application deleted');
    },
    onError: (error) => toast.error(errorMessage(error, 'Failed to delete application')),
  });

  const columnHelper = useMemo(
    () => createColumnHelper<DataTableFeatures, ApplicationResponse>(),
    [],
  );

  const columns: DataTableColumnDef<ApplicationResponse>[] = useMemo(
    () => [
      columnHelper.accessor('name', {
        header: 'Name',
        cell: (info) => (
          <Link
            to={`/applications/${info.row.original.id}/overview`}
            className="font-medium text-text-primary hover:underline"
            onClick={(event) => event.stopPropagation()}
          >
            {info.getValue()}
          </Link>
        ),
      }),
      columnHelper.accessor('description', {
        header: 'Description',
        cell: (info) => info.getValue() || <span className="text-text-muted">—</span>,
      }),
      columnHelper.accessor('status', {
        header: 'Status',
        cell: (info) => {
          const status = info.getValue() as ApplicationResponse['status'];
          return <StatusBadge status={STATUS_KIND_BY_APPLICATION_STATUS[status]} label={status} />;
        },
      }),
      columnHelper.accessor('createdAt', {
        header: 'Created',
        cell: (info) => format(new Date(info.getValue()), 'PP'),
      }),
    ],
    [columnHelper],
  );

  function rowActions(application: ApplicationResponse): DataTableRowAction[] {
    if (!capabilities.canManageApplications) {
      return [];
    }

    const actions: DataTableRowAction[] = [];
    if (application.status !== 'Deleted') {
      actions.push({
        id: 'edit',
        label: 'Edit',
        icon: <PencilIcon />,
        onSelect: () => setEditingApplication(application),
      });
    }
    if (application.status === 'Active') {
      actions.push({
        id: 'archive',
        label: 'Archive',
        icon: <ArchiveIcon />,
        onSelect: () => setPendingAction({ type: 'archive', application }),
      });
    }
    if (application.status === 'Archived') {
      actions.push({
        id: 'restore',
        label: 'Restore',
        icon: <ArchiveRestoreIcon />,
        onSelect: () => setPendingAction({ type: 'restore', application }),
      });
    }
    if (application.status !== 'Deleted') {
      actions.push({
        id: 'delete',
        label: 'Delete',
        icon: <TrashIcon />,
        destructive: true,
        onSelect: () => setPendingAction({ type: 'delete', application }),
      });
    }
    return actions;
  }

  const isMutating =
    createMutation.isPending ||
    updateMutation.isPending ||
    archiveMutation.isPending ||
    restoreMutation.isPending ||
    deleteMutation.isPending;

  return (
    <div aria-labelledby="applications-heading" className="mx-auto max-w-6xl space-y-6">
      <PageHeader
        eyebrow="Platform"
        icon={LayoutGrid}
        title="Applications"
        description="Every knowledge application you can access. Open one to manage its knowledge, agents, and runs."
        actions={
          capabilities.canManageApplications && (
            <Button onClick={() => setIsCreateOpen(true)}>
              <PlusIcon />
              New application
            </Button>
          )
        }
      />

      {applicationsQuery.isError ? (
        <p role="alert" className="text-sm text-danger">
          {errorMessage(applicationsQuery.error, 'Failed to load applications')}
        </p>
      ) : (
        <DataTable
          columns={columns}
          data={applicationsQuery.data ?? []}
          isLoading={applicationsQuery.isLoading}
          getRowId={(row) => row.id}
          searchPlaceholder="Search applications..."
          emptyState={
            capabilities.canManageApplications
              ? 'No applications yet - create one to get started.'
              : 'No applications to show.'
          }
          rowActions={rowActions}
        />
      )}

      <ApplicationFormDialog
        open={isCreateOpen}
        onOpenChange={setIsCreateOpen}
        mode="create"
        onSubmit={(values) => createMutation.mutate(values)}
        isPending={createMutation.isPending}
      />

      <ApplicationFormDialog
        open={editingApplication !== null}
        onOpenChange={(open) => !open && setEditingApplication(null)}
        mode="edit"
        initialValues={editingApplication ? toFormValues(editingApplication) : undefined}
        onSubmit={(values) => {
          if (editingApplication) {
            updateMutation.mutate({ id: editingApplication.id, values });
          }
        }}
        isPending={updateMutation.isPending}
      />

      <ConfirmDialog
        open={pendingAction !== null}
        onOpenChange={(open) => !open && setPendingAction(null)}
        title={
          pendingAction?.type === 'archive'
            ? 'Archive application?'
            : pendingAction?.type === 'restore'
              ? 'Restore application?'
              : 'Delete application?'
        }
        description={
          pendingAction?.type === 'archive'
            ? `"${pendingAction.application.name}" becomes read-only for other modules until restored.`
            : pendingAction?.type === 'restore'
              ? `"${pendingAction.application.name}" becomes active again.`
              : `"${pendingAction?.application.name}" will be soft-deleted. Historical outputs remain viewable, but the application can no longer be used.`
        }
        confirmLabel={
          pendingAction?.type === 'archive'
            ? 'Archive'
            : pendingAction?.type === 'restore'
              ? 'Restore'
              : 'Delete'
        }
        destructive={pendingAction?.type !== 'restore'}
        isPending={isMutating}
        onConfirm={() => {
          if (!pendingAction) {
            return;
          }
          const { type, application } = pendingAction;
          if (type === 'archive') {
            archiveMutation.mutate(application.id);
          } else if (type === 'restore') {
            restoreMutation.mutate(application.id);
          } else {
            deleteMutation.mutate(application.id);
          }
        }}
      />
    </div>
  );
}
