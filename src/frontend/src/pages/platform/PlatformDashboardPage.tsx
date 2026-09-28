import { useQuery } from '@tanstack/react-query';
import {
  Archive,
  Boxes,
  CheckCircle2,
  Cpu,
  Database,
  FolderKanban,
  LayoutDashboard,
  PlayCircle,
  Server,
  Trash2,
} from 'lucide-react';
import {
  dashboardsQueryKeys,
  getPlatformDashboard,
  type PlatformDashboardResponse,
} from '@/api/dashboards';
import { ApiError } from '@/api/problemDetails';
import { useCapabilities } from '@/app/authz/useCapabilities';
import { useJoinOperationsGroup } from '@/app/realtime/useRealtimeGroups';
import {
  AttentionItemRow,
  JobStatusMetrics,
  KnowledgeFileMetrics,
} from '@/components/dashboards/dashboard-metrics';
import { EmptyState } from '@/components/patterns/EmptyState';
import { MetricTile, MetricTileGrid } from '@/components/patterns/MetricTile';
import { PageHeader } from '@/components/patterns/PageHeader';
import { SectionCard } from '@/components/patterns/SectionCard';
import { Skeleton } from '@/components/ui/skeleton';
import { StatusBadge } from '@/components/status/StatusBadge';

function errorMessage(error: unknown, fallback: string): string {
  return error instanceof ApiError ? (error.problem.detail ?? error.problem.title) : fallback;
}

/**
 * TASK-PDSH-02: cross-application platform dashboard (`/platform/dashboard`,
 * FR-DSH-001). Visible to SuperAdmin/SuperViewer only - the server's
 * `CanViewPlatformProcessing` policy independently enforces this (a 403
 * response here means the caller's role changed since this session started
 * or they navigated here directly without the capability). Live updates via
 * the `platform:operations` SignalR group (`DashboardChanged` invalidates
 * this query - see `SignalRProvider`).
 */
export default function PlatformDashboardPage() {
  const capabilities = useCapabilities();
  useJoinOperationsGroup();

  const dashboardQuery = useQuery({
    queryKey: dashboardsQueryKeys.platform(),
    queryFn: getPlatformDashboard,
    enabled: capabilities.canViewPlatformProcessing,
  });

  return (
    <div aria-labelledby="platform-dashboard-heading" className="mx-auto max-w-6xl space-y-6">
      <PageHeader
        eyebrow="Platform"
        icon={LayoutDashboard}
        title="Dashboard"
        description="Health and throughput across every application."
      />

      {!capabilities.canViewPlatformProcessing ? (
        <p role="alert" className="text-sm text-danger">
          You don&apos;t have access to the platform dashboard.
        </p>
      ) : dashboardQuery.isLoading ? (
        <div className="space-y-4">
          <Skeleton className="h-20 w-full rounded-card" />
          <Skeleton className="h-56 w-full rounded-card" />
        </div>
      ) : dashboardQuery.isError ? (
        <p role="alert" className="text-sm text-danger">
          {errorMessage(dashboardQuery.error, 'Failed to load the platform dashboard')}
        </p>
      ) : (
        dashboardQuery.data && <PlatformDashboardContent dashboard={dashboardQuery.data} />
      )}
    </div>
  );
}

function PlatformDashboardContent({ dashboard }: { dashboard: PlatformDashboardResponse }) {
  const staleWorkers = dashboard.workers.filter((worker) => worker.isStale).length;

  return (
    <div className="space-y-6">
      {dashboard.attentionItems.length > 0 && (
        <div className="space-y-2">
          {dashboard.attentionItems.map((item) => (
            <AttentionItemRow key={item.kind} item={item} />
          ))}
        </div>
      )}

      <MetricTileGrid columns={4}>
        <MetricTile
          label="Active applications"
          value={dashboard.applications.active}
          icon={Boxes}
          tone="accent"
        />
        <MetricTile
          label="Knowledge files ready"
          value={dashboard.knowledgeFiles.ready}
          icon={FolderKanban}
          tone="success"
        />
        <MetricTile
          label="Runs completed"
          value={dashboard.agentRuns.completed}
          icon={PlayCircle}
          tone="success"
        />
        <MetricTile
          label="Azure OpenAI"
          value={dashboard.aoaiAvailable ? 'Online' : 'Offline'}
          icon={Cpu}
          tone={dashboard.aoaiAvailable ? 'success' : 'danger'}
          dimWhenZero={false}
          hint={staleWorkers > 0 ? `${staleWorkers} stale worker(s)` : undefined}
        />
      </MetricTileGrid>

      <div className="grid gap-4 lg:grid-cols-2">
        <SectionCard icon={Boxes} title="Applications" description="By lifecycle status.">
          <MetricTileGrid columns={3}>
            <MetricTile
              label="Active"
              value={dashboard.applications.active}
              icon={CheckCircle2}
              tone="success"
            />
            <MetricTile
              label="Archived"
              value={dashboard.applications.archived}
              icon={Archive}
              tone="neutral"
            />
            <MetricTile
              label="Deleted"
              value={dashboard.applications.deleted}
              icon={Trash2}
              tone="neutral"
            />
          </MetricTileGrid>
        </SectionCard>

        <SectionCard
          icon={FolderKanban}
          title="Knowledge files"
          description="Across every application."
        >
          <KnowledgeFileMetrics counts={dashboard.knowledgeFiles} />
        </SectionCard>

        <SectionCard icon={Server} title="Processing jobs" iconTone="processing">
          <JobStatusMetrics counts={dashboard.processingJobs} />
        </SectionCard>

        <SectionCard icon={PlayCircle} title="Agent runs">
          <JobStatusMetrics counts={dashboard.agentRuns} />
        </SectionCard>
      </div>

      <div className="grid gap-4 lg:grid-cols-2">
        <SectionCard
          icon={Cpu}
          title="Workers"
          description="Document & agent worker heartbeats (Module 31)."
        >
          {dashboard.workers.length === 0 ? (
            <EmptyState
              compact
              icon={Cpu}
              title="No worker heartbeats"
              description="Workers report in as they process jobs — none have checked in yet."
            />
          ) : (
            <ul className="space-y-2">
              {dashboard.workers.map((worker) => (
                <li
                  key={worker.workerId}
                  className="flex flex-wrap items-center gap-3 rounded-control border border-border bg-surface/60 px-3 py-2 text-sm"
                >
                  <StatusBadge
                    status={worker.isStale ? 'warning' : 'ready'}
                    label={worker.isStale ? 'Stale' : 'Healthy'}
                  />
                  <span className="font-medium text-text-primary">{worker.workerId}</span>
                  <span className="text-text-muted">{worker.workerKind}</span>
                  <span className="ml-auto text-xs text-text-muted">
                    {new Date(worker.lastHeartbeatAt).toLocaleString()}
                  </span>
                </li>
              ))}
            </ul>
          )}
        </SectionCard>

        <SectionCard
          icon={Database}
          title="Queues"
          description="Approximate SQS depth, including dead-letter counts."
        >
          {dashboard.queues.length === 0 ? (
            <EmptyState compact icon={Database} title="No queues reported" />
          ) : (
            <ul className="space-y-2">
              {dashboard.queues.map((queue) => (
                <li
                  key={queue.queueName}
                  className="flex flex-wrap items-center gap-4 rounded-control border border-border bg-surface/60 px-3 py-2 text-sm"
                >
                  <span className="min-w-0 flex-1 truncate font-medium text-text-primary">
                    {queue.queueName}
                  </span>
                  <span className="text-text-muted">
                    Depth{' '}
                    <span className="font-semibold tabular-nums text-text-primary">
                      {queue.approximateMessageCount}
                    </span>
                  </span>
                  <span className="text-text-muted">
                    Dead-letter{' '}
                    <span
                      className={
                        queue.deadLetterQueueMessageCount > 0
                          ? 'font-semibold tabular-nums text-warning'
                          : 'font-semibold tabular-nums text-text-primary'
                      }
                    >
                      {queue.deadLetterQueueMessageCount}
                    </span>
                  </span>
                </li>
              ))}
            </ul>
          )}
        </SectionCard>
      </div>
    </div>
  );
}
