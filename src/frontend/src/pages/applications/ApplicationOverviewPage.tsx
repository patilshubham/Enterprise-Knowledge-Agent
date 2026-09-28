import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  ArchiveIcon,
  ArchiveRestoreIcon,
  Bot,
  FileOutput,
  FolderKanban,
  History,
  LayoutGrid,
  PencilIcon,
  PlayCircle,
  Server,
  TrashIcon,
} from 'lucide-react';
import { Link, useParams } from 'react-router-dom';
import { toast } from 'sonner';
import {
  archiveApplication,
  applicationsQueryKeys,
  deleteApplication,
  getApplication,
  restoreApplication,
  updateApplication,
  type ApplicationResponse,
} from '@/api/applications';
import {
  dashboardsQueryKeys,
  getApplicationDashboard,
  type JobStatusCounts,
  type KnowledgeFileCounts,
} from '@/api/dashboards';
import { ApiError } from '@/api/problemDetails';
import { useCapabilities } from '@/app/authz/useCapabilities';
import { useJoinApplicationGroup } from '@/app/realtime/useRealtimeGroups';
import {
  ApplicationFormDialog,
  type ApplicationFormValues,
} from '@/components/applications/ApplicationFormDialog';
import {
  AttentionItemRow,
  JobStatusMetrics,
  KnowledgeFileMetrics,
} from '@/components/dashboards/dashboard-metrics';
import { ConfirmDialog } from '@/components/dialogs/ConfirmDialog';
import { EmptyState } from '@/components/patterns/EmptyState';
import { MetricTile, MetricTileGrid } from '@/components/patterns/MetricTile';
import { PageHeader } from '@/components/patterns/PageHeader';
import { SectionCard } from '@/components/patterns/SectionCard';
import { WorkflowPipeline, type PipelineStage } from '@/components/patterns/WorkflowPipeline';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { StatusBadge } from '@/components/status/StatusBadge';
import type { StatusKind } from '@/components/status/status-tokens';

const STATUS_KIND_BY_APPLICATION_STATUS: Record<ApplicationResponse['status'], StatusKind> = {
  Active: 'ready',
  Archived: 'disabled',
  Deleted: 'failed',
};

type PendingAction = 'archive' | 'restore' | 'delete' | null;

/** Derives an accessible status pill for the Knowledge stage from its file counts. */
function knowledgePill(c: KnowledgeFileCounts): { status?: StatusKind; statusLabel?: string } {
  if (c.processing > 0) return { status: 'processing', statusLabel: 'Processing' };
  if (c.failed > 0) return { status: 'failed', statusLabel: `${c.failed} failed` };
  if (c.queued > 0) return { status: 'queued', statusLabel: 'Queued' };
  if (c.ready > 0) return { status: 'ready', statusLabel: 'Ready' };
  return {};
}

/** Derives an accessible status pill for a job/run stage from its status counts. */
function jobPill(c: JobStatusCounts): { status?: StatusKind; statusLabel?: string } {
  if (c.running > 0) return { status: 'processing', statusLabel: 'Running' };
  if (c.failed > 0) return { status: 'failed', statusLabel: `${c.failed} failed` };
  if (c.queued > 0) return { status: 'queued', statusLabel: 'Queued' };
  if (c.completed > 0) return { status: 'ready', statusLabel: 'Completed' };
  return {};
}

function joinHints(...parts: (string | false | undefined)[]): string | undefined {
  const kept = parts.filter(Boolean) as string[];
  return kept.length > 0 ? kept.join(' · ') : undefined;
}

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
 * TASK-APP-03: per-application overview (`/applications/:applicationId/overview`).
 * Reachable by any of the four roles that can view this application - the
 * server's `CanViewApplication` policy returns 404 (not 403) for others so
 * cross-app existence never leaks. Mutating actions are further gated to
 * `canManageApplications` (SuperAdmin only, mirrors `CanManageApplications`).
 */
export default function ApplicationOverviewPage() {
  const { applicationId } = useParams<{ applicationId: string }>();
  const queryClient = useQueryClient();
  const [isEditOpen, setIsEditOpen] = useState(false);
  const [pendingAction, setPendingAction] = useState<PendingAction>(null);

  const applicationQuery = useQuery({
    queryKey: applicationsQueryKeys.detail(applicationId ?? ''),
    queryFn: () => getApplication(applicationId ?? ''),
    enabled: Boolean(applicationId),
  });

  const capabilities = useCapabilities(applicationId, {
    viewerExecutionEnabled: applicationQuery.data?.allowViewerExecution,
  });

  useJoinApplicationGroup(applicationId);

  const dashboardQuery = useQuery({
    queryKey: dashboardsQueryKeys.application(applicationId ?? ''),
    queryFn: () => getApplicationDashboard(applicationId ?? ''),
    enabled: Boolean(applicationId) && capabilities.canViewApplication,
  });

  function invalidate() {
    return Promise.all([
      queryClient.invalidateQueries({
        queryKey: applicationsQueryKeys.detail(applicationId ?? ''),
      }),
      queryClient.invalidateQueries({ queryKey: applicationsQueryKeys.list() }),
    ]);
  }

  const updateMutation = useMutation({
    mutationFn: (values: ApplicationFormValues) => updateApplication(applicationId ?? '', values),
    onSuccess: async () => {
      await invalidate();
      setIsEditOpen(false);
      toast.success('Application updated');
    },
    onError: (error) => toast.error(errorMessage(error, 'Failed to update application')),
  });

  const archiveMutation = useMutation({
    mutationFn: () => archiveApplication(applicationId ?? ''),
    onSuccess: async () => {
      await invalidate();
      setPendingAction(null);
      toast.success('Application archived');
    },
    onError: (error) => toast.error(errorMessage(error, 'Failed to archive application')),
  });

  const restoreMutation = useMutation({
    mutationFn: () => restoreApplication(applicationId ?? ''),
    onSuccess: async () => {
      await invalidate();
      setPendingAction(null);
      toast.success('Application restored');
    },
    onError: (error) => toast.error(errorMessage(error, 'Failed to restore application')),
  });

  const deleteMutation = useMutation({
    mutationFn: () => deleteApplication(applicationId ?? ''),
    onSuccess: async () => {
      await invalidate();
      setPendingAction(null);
      toast.success('Application deleted');
    },
    onError: (error) => toast.error(errorMessage(error, 'Failed to delete application')),
  });

  if (applicationQuery.isLoading) {
    return (
      <section aria-label="Application overview" className="mx-auto max-w-6xl space-y-6">
        <div className="flex items-center gap-4">
          <Skeleton className="size-11 rounded-control" />
          <div className="space-y-2">
            <Skeleton className="h-7 w-64" />
            <Skeleton className="h-4 w-80" />
          </div>
        </div>
        <Skeleton className="h-40 w-full rounded-card" />
        <Skeleton className="h-56 w-full rounded-card" />
      </section>
    );
  }

  if (applicationQuery.isError) {
    const notFound =
      applicationQuery.error instanceof ApiError && applicationQuery.error.status === 404;
    return (
      <section aria-label="Application overview">
        <p role="alert" className="text-sm text-danger">
          {notFound
            ? "This application doesn't exist or you don't have access to it."
            : errorMessage(applicationQuery.error, 'Failed to load application')}
        </p>
      </section>
    );
  }

  const application = applicationQuery.data;
  if (!application) {
    return null;
  }

  const isMutating =
    updateMutation.isPending ||
    archiveMutation.isPending ||
    restoreMutation.isPending ||
    deleteMutation.isPending;

  const dashboard = dashboardQuery.data;
  const appId = application.id;

  let pipelineStages: PipelineStage[] | null = null;
  if (dashboard) {
    pipelineStages = [
      {
        key: 'knowledge',
        label: 'Knowledge',
        icon: FolderKanban,
        tone: 'accent',
        to: `/applications/${appId}/knowledge`,
        value: dashboard.knowledgeFiles.ready,
        valueLabel: 'files ready',
        ...knowledgePill(dashboard.knowledgeFiles),
        hint: joinHints(
          dashboard.knowledgeFiles.processing > 0 && `${dashboard.knowledgeFiles.processing} processing`,
          dashboard.knowledgeFiles.failed > 0 && `${dashboard.knowledgeFiles.failed} failed`,
        ),
      },
      {
        key: 'processing',
        label: 'Processing',
        icon: Server,
        tone: 'processing',
        to: `/applications/${appId}/processing`,
        value: dashboard.processingJobs.queued + dashboard.processingJobs.running,
        valueLabel: 'jobs in progress',
        ...jobPill(dashboard.processingJobs),
        hint: joinHints(`${dashboard.processingJobs.completed} completed`),
      },
      {
        key: 'agents',
        label: 'Agents',
        icon: Bot,
        tone: 'accent',
        to: `/applications/${appId}/agents`,
        value: dashboard.activeAgents,
        valueLabel: 'active agents',
        status: dashboard.activeAgents > 0 ? 'ready' : undefined,
        statusLabel: dashboard.activeAgents > 0 ? 'Active' : undefined,
        hint: `${dashboard.totalAgents} total`,
      },
      {
        key: 'runs',
        label: 'Runs',
        icon: PlayCircle,
        tone: 'accent',
        to: `/applications/${appId}/runs`,
        value: dashboard.agentRuns.queued + dashboard.agentRuns.running,
        valueLabel: 'runs in progress',
        ...jobPill(dashboard.agentRuns),
        hint: joinHints(`${dashboard.agentRuns.completed} completed`),
      },
    ];
    if (capabilities.canViewOutput) {
      pipelineStages.push({
        key: 'outputs',
        label: 'Outputs',
        icon: FileOutput,
        tone: 'success',
        to: `/applications/${appId}/outputs`,
        value: dashboard.generatedOutputs,
        valueLabel: 'generated outputs',
        status: dashboard.generatedOutputs > 0 ? 'ready' : undefined,
        statusLabel: dashboard.generatedOutputs > 0 ? 'Ready' : undefined,
      });
    }
  }

  const isFresh = dashboard
    ? dashboard.knowledgeFiles.ready +
          dashboard.knowledgeFiles.processing +
          dashboard.knowledgeFiles.queued +
          dashboard.knowledgeFiles.failed +
          dashboard.knowledgeFiles.disabled ===
          0 && dashboard.totalAgents === 0
    : false;

  return (
    <div aria-labelledby="application-overview-heading" className="mx-auto max-w-6xl space-y-6">
      <PageHeader
        eyebrow="Application"
        icon={LayoutGrid}
        title={application.name}
        description={application.description || 'No description provided yet.'}
        meta={
          <>
            <StatusBadge
              status={STATUS_KIND_BY_APPLICATION_STATUS[application.status]}
              label={application.status}
            />
            <span className="text-sm text-text-muted">
              Viewer execution{' '}
              <span className="font-medium text-text-secondary">
                {application.allowViewerExecution ? 'Enabled' : 'Disabled'}
              </span>
            </span>
          </>
        }
        actions={
          <>
            {capabilities.canViewAudit && (
              <Button variant="ghost" size="sm" asChild>
                <Link to={`/applications/${appId}/activity`}>
                  <History />
                  Activity
                </Link>
              </Button>
            )}
            {capabilities.canManageApplications && application.status !== 'Deleted' && (
              <>
                <Button variant="outline" size="sm" onClick={() => setIsEditOpen(true)}>
                  <PencilIcon />
                  Edit
                </Button>
                {application.status === 'Active' && (
                  <Button variant="outline" size="sm" onClick={() => setPendingAction('archive')}>
                    <ArchiveIcon />
                    Archive
                  </Button>
                )}
                {application.status === 'Archived' && (
                  <Button variant="outline" size="sm" onClick={() => setPendingAction('restore')}>
                    <ArchiveRestoreIcon />
                    Restore
                  </Button>
                )}
                <Button variant="destructive" size="sm" onClick={() => setPendingAction('delete')}>
                  <TrashIcon />
                  Delete
                </Button>
              </>
            )}
          </>
        }
      />

      {dashboard && dashboard.attentionItems.length > 0 && (
        <div className="space-y-2">
          {dashboard.attentionItems.map((item) => (
            <AttentionItemRow key={item.kind} item={item} />
          ))}
        </div>
      )}

      {capabilities.canViewApplication && isFresh && (
        <SectionCard icon={FolderKanban} title="Get started" iconTone="success">
          <EmptyState
            compact
            icon={FolderKanban}
            title="Set up this application"
            description="Upload knowledge files so your agents have grounded material to retrieve. Once files are ready, create an agent and run it to generate outputs."
            action={
              <Button asChild size="sm">
                <Link to={`/applications/${appId}/knowledge`}>Upload knowledge</Link>
              </Button>
            }
          />
        </SectionCard>
      )}

      {capabilities.canViewApplication && (
        <SectionCard
          icon={LayoutGrid}
          title="Workflow"
          description="From knowledge to generated outputs — open any stage to dive in."
        >
          {pipelineStages ? (
            <WorkflowPipeline stages={pipelineStages} />
          ) : (
            <div className="flex flex-col gap-2 lg:flex-row">
              {Array.from({ length: 5 }).map((_, index) => (
                <Skeleton key={index} className="h-28 flex-1 rounded-card" />
              ))}
            </div>
          )}
        </SectionCard>
      )}

      {capabilities.canViewApplication && dashboard && (
        <div className="grid gap-4 lg:grid-cols-2">
          <SectionCard icon={FolderKanban} title="Knowledge files">
            <KnowledgeFileMetrics counts={dashboard.knowledgeFiles} />
          </SectionCard>
          <SectionCard icon={Server} title="Processing jobs" iconTone="processing">
            <JobStatusMetrics counts={dashboard.processingJobs} />
          </SectionCard>
          <SectionCard icon={PlayCircle} title="Agent runs">
            <JobStatusMetrics counts={dashboard.agentRuns} />
          </SectionCard>
          <SectionCard icon={Bot} title="Agents & outputs">
            <MetricTileGrid columns={3}>
              <MetricTile
                label="Active agents"
                value={dashboard.activeAgents}
                icon={Bot}
                tone="accent"
              />
              <MetricTile
                label="Total agents"
                value={dashboard.totalAgents}
                icon={Bot}
                tone="neutral"
              />
              <MetricTile
                label="Generated outputs"
                value={dashboard.generatedOutputs}
                icon={FileOutput}
                tone="success"
              />
            </MetricTileGrid>
          </SectionCard>
        </div>
      )}

      <ApplicationFormDialog
        open={isEditOpen}
        onOpenChange={setIsEditOpen}
        mode="edit"
        initialValues={toFormValues(application)}
        onSubmit={(values) => updateMutation.mutate(values)}
        isPending={updateMutation.isPending}
      />

      <ConfirmDialog
        open={pendingAction !== null}
        onOpenChange={(open) => !open && setPendingAction(null)}
        title={
          pendingAction === 'archive'
            ? 'Archive application?'
            : pendingAction === 'restore'
              ? 'Restore application?'
              : 'Delete application?'
        }
        description={
          pendingAction === 'archive'
            ? `"${application.name}" becomes read-only for other modules until restored.`
            : pendingAction === 'restore'
              ? `"${application.name}" becomes active again.`
              : `"${application.name}" will be soft-deleted. Historical outputs remain viewable, but the application can no longer be used.`
        }
        confirmLabel={
          pendingAction === 'archive'
            ? 'Archive'
            : pendingAction === 'restore'
              ? 'Restore'
              : 'Delete'
        }
        destructive={pendingAction !== 'restore'}
        isPending={isMutating}
        onConfirm={() => {
          if (pendingAction === 'archive') {
            archiveMutation.mutate();
          } else if (pendingAction === 'restore') {
            restoreMutation.mutate();
          } else if (pendingAction === 'delete') {
            deleteMutation.mutate();
          }
        }}
      />
    </div>
  );
}
