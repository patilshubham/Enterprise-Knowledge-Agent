import { useMutation, useQuery } from '@tanstack/react-query';
import { RefreshCwIcon, Wrench } from 'lucide-react';
import { toast } from 'sonner';
import {
  getAoaiHealth,
  getOperationsHealth,
  operationsQueryKeys,
  type ComponentHealth,
  type DeploymentHealthStatus,
} from '@/api/operations';
import { ApiError } from '@/api/problemDetails';
import { useCapabilities } from '@/app/authz/useCapabilities';
import { Metric } from '@/components/dashboards/dashboard-metrics';
import { ErrorState } from '@/components/errors/ErrorState';
import { PageHeader } from '@/components/patterns/PageHeader';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { StatusBadge } from '@/components/status/StatusBadge';
import type { StatusKind } from '@/components/status/status-tokens';

const STATUS_KIND_BY_HEALTH_STATUS: Record<ComponentHealth['status'], StatusKind> = {
  Healthy: 'ready',
  Degraded: 'warning',
  Unhealthy: 'failed',
};

function errorMessage(error: unknown, fallback: string): string {
  return error instanceof ApiError ? (error.problem.detail ?? error.problem.title) : fallback;
}

/**
 * TASK-OPS-03: platform operations page (`/platform/operations`, module 31,
 * ASDD §24). Visible to SuperAdmin/SuperViewer only - the server's
 * `CanViewOperations` policy independently enforces this. Component
 * health/DLQ/worker data auto-refreshes on a short poll (there is no
 * realtime event for dependency health); the Azure OpenAI deployment check
 * is deliberately NOT auto-loaded - it is only run when the user clicks
 * "Run deep check", to avoid burning model quota on routine page views.
 */
export default function PlatformOperationsPage() {
  const capabilities = useCapabilities();

  const healthQuery = useQuery({
    queryKey: operationsQueryKeys.health(),
    queryFn: getOperationsHealth,
    enabled: capabilities.canViewOperations,
    refetchInterval: 30_000,
  });

  const aoaiHealthMutation = useMutation({
    mutationFn: getAoaiHealth,
    onError: (error) => {
      toast.error(errorMessage(error, 'Failed to check Azure OpenAI availability'));
    },
  });

  return (
    <div aria-labelledby="platform-operations-heading" className="mx-auto max-w-6xl space-y-6">
      <PageHeader
        eyebrow="Platform"
        icon={Wrench}
        title="Operations"
        description="Live health of platform components, queues, workers, and Azure OpenAI deployments."
      />

      {!capabilities.canViewOperations ? (
        <p role="alert" className="text-sm text-danger">
          You don&apos;t have access to platform operations.
        </p>
      ) : healthQuery.isLoading ? (
        <div className="space-y-4">
          <Skeleton className="h-40 w-full" />
          <Skeleton className="h-40 w-full" />
        </div>
      ) : healthQuery.isError ? (
        <ErrorState
          error={healthQuery.error}
          fallbackDetail="Failed to load operations health"
          onRetry={() => healthQuery.refetch()}
        />
      ) : (
        healthQuery.data && (
          <PlatformOperationsContent
            checkedAt={healthQuery.data.checkedAt}
            components={healthQuery.data.components}
            queues={healthQuery.data.queues}
            workers={healthQuery.data.workers}
            aoaiDeployments={aoaiHealthMutation.data}
            isCheckingAoai={aoaiHealthMutation.isPending}
            onRunAoaiCheck={() => aoaiHealthMutation.mutate()}
          />
        )
      )}
    </div>
  );
}

interface PlatformOperationsContentProps {
  checkedAt: string;
  components: ComponentHealth[];
  queues: {
    queueName: string;
    approximateMessageCount: number;
    deadLetterQueueMessageCount: number;
  }[];
  workers: { workerId: string; workerKind: string; lastHeartbeatAt: string; isStale: boolean }[];
  aoaiDeployments: DeploymentHealthStatus[] | undefined;
  isCheckingAoai: boolean;
  onRunAoaiCheck: () => void;
}

function PlatformOperationsContent({
  checkedAt,
  components,
  queues,
  workers,
  aoaiDeployments,
  isCheckingAoai,
  onRunAoaiCheck,
}: PlatformOperationsContentProps) {
  return (
    <>
      <p className="text-sm text-text-muted">Last checked {new Date(checkedAt).toLocaleString()}</p>

      <Card>
        <CardHeader>
          <CardTitle>Component health</CardTitle>
          <CardDescription>
            Postgres, pgvector, object storage, message queue, and SignalR.
          </CardDescription>
        </CardHeader>
        <CardContent className="px-6">
          <ul className="space-y-2">
            {components.map((component) => (
              <li key={component.name} className="flex flex-wrap items-center gap-3 text-sm">
                <StatusBadge
                  status={STATUS_KIND_BY_HEALTH_STATUS[component.status]}
                  label={component.status}
                />
                <span className="font-medium">{component.name}</span>
                {component.detail && <span className="text-text-muted">{component.detail}</span>}
              </li>
            ))}
          </ul>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Workers</CardTitle>
          <CardDescription>Document and agent worker heartbeats (Module 31).</CardDescription>
        </CardHeader>
        <CardContent className="px-6">
          {workers.length === 0 ? (
            <p className="text-sm text-text-muted">No worker heartbeats recorded.</p>
          ) : (
            <ul className="space-y-2">
              {workers.map((worker) => (
                <li key={worker.workerId} className="flex items-center gap-3 text-sm">
                  <StatusBadge
                    status={worker.isStale ? 'warning' : 'ready'}
                    label={worker.isStale ? 'Stale' : 'Healthy'}
                  />
                  <span className="font-medium">{worker.workerId}</span>
                  <span className="text-text-muted">{worker.workerKind}</span>
                  <span className="text-text-muted">
                    Last heartbeat {new Date(worker.lastHeartbeatAt).toLocaleString()}
                  </span>
                </li>
              ))}
            </ul>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Queues</CardTitle>
          <CardDescription>
            Approximate SQS depth, including dead-letter counts (TASK-SQS-04).
          </CardDescription>
        </CardHeader>
        <CardContent className="px-6">
          <ul className="space-y-2">
            {queues.map((queue) => (
              <li key={queue.queueName} className="flex flex-wrap items-center gap-6 text-sm">
                <span className="font-medium">{queue.queueName}</span>
                <Metric label="Depth" value={queue.approximateMessageCount} />
                <Metric label="Dead-letter" value={queue.deadLetterQueueMessageCount} />
              </li>
            ))}
          </ul>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Azure OpenAI</CardTitle>
          <CardDescription>
            Deep reachability check is manual (sparing) to avoid burning model quota.
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-3 px-6">
          <Button variant="outline" size="sm" disabled={isCheckingAoai} onClick={onRunAoaiCheck}>
            <RefreshCwIcon
              aria-hidden="true"
              className={isCheckingAoai ? 'animate-spin' : undefined}
            />
            Run deep check
          </Button>
          {aoaiDeployments && (
            <ul className="space-y-2">
              {aoaiDeployments.map((deployment) => (
                <li
                  key={deployment.deployment}
                  className="flex flex-wrap items-center gap-3 text-sm"
                >
                  <StatusBadge
                    status={deployment.reachable ? 'ready' : 'failed'}
                    label={deployment.reachable ? 'Reachable' : 'Unreachable'}
                  />
                  <span className="font-medium">{deployment.deployment}</span>
                  <span className="text-text-muted">{deployment.purpose}</span>
                </li>
              ))}
            </ul>
          )}
        </CardContent>
      </Card>
    </>
  );
}
