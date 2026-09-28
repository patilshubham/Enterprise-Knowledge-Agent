import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { format } from 'date-fns';
import { BanIcon, PlayCircle, RefreshCwIcon } from 'lucide-react';
import { Link, useParams } from 'react-router-dom';
import { toast } from 'sonner';

import { ApiError } from '@/api/problemDetails';
import { cancelRun, getRunDetail, retryRun, runsQueryKeys } from '@/api/runs';
import { useCapabilities } from '@/app/authz/useCapabilities';
import { useJoinRunGroup } from '@/app/realtime/useRealtimeGroups';
import { ConfirmDialog } from '@/components/dialogs/ConfirmDialog';
import { PageHeader } from '@/components/patterns/PageHeader';
import { StageTimeline } from '@/components/timeline/StageTimeline';
import { StatusBadge } from '@/components/status/StatusBadge';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { buildRunStages, jobStatusToStatusKind } from '@/lib/jobStatusMapping';

const NON_CANCELLABLE_STATUSES = new Set(['Completed', 'Failed', 'Cancelled']);

function errorMessage(error: unknown, fallback: string): string {
  return error instanceof ApiError ? (error.problem.detail ?? error.problem.title) : fallback;
}

function formatJson(json: string | null): string | null {
  if (!json) {
    return null;
  }
  try {
    return JSON.stringify(JSON.parse(json), null, 2);
  } catch {
    return json;
  }
}

/**
 * TASK-RDSH-02: `/applications/:applicationId/runs/:runId` - the agent-run
 * detail page. Mirrors `OutputDetailPage`'s structure (header + actions,
 * status/progress, snapshot fields, then supporting detail) but replaces
 * the Markdown workspace with a `StageTimeline` + evidence package, since a
 * run's own generated output (if any) is viewed via `OutputDetailPage`
 * instead of duplicated here.
 */
export default function RunDetailPage() {
  const { applicationId, runId } = useParams<{ applicationId: string; runId: string }>();
  const queryClient = useQueryClient();
  const capabilities = useCapabilities(applicationId);
  const [cancelDialogOpen, setCancelDialogOpen] = useState(false);

  // TASK-SIG-05/06: joins `run:{id}` for live per-run stage/progress updates.
  useJoinRunGroup(runId);

  const runQuery = useQuery({
    queryKey: runsQueryKeys.detail(applicationId ?? '', runId ?? ''),
    queryFn: () => getRunDetail(applicationId ?? '', runId ?? ''),
    enabled: Boolean(applicationId) && Boolean(runId),
  });

  function invalidate() {
    return Promise.all([
      queryClient.invalidateQueries({
        queryKey: runsQueryKeys.detail(applicationId ?? '', runId ?? ''),
      }),
      queryClient.invalidateQueries({ queryKey: runsQueryKeys.list(applicationId ?? '') }),
    ]);
  }

  const retryMutation = useMutation({
    mutationFn: () => retryRun(applicationId ?? '', runId ?? ''),
    onSuccess: async () => {
      await invalidate();
      toast.success('Run retry queued');
    },
    onError: (error) => toast.error(errorMessage(error, 'Failed to retry run')),
  });

  const cancelMutation = useMutation({
    mutationFn: () => cancelRun(applicationId ?? '', run?.agentId ?? '', runId ?? ''),
    onSuccess: async () => {
      setCancelDialogOpen(false);
      await invalidate();
      toast.success('Cancellation requested');
    },
    onError: (error) => {
      setCancelDialogOpen(false);
      toast.error(errorMessage(error, 'Failed to cancel run'));
    },
  });

  const run = runQuery.data;

  if (runQuery.isLoading) {
    return (
      <section aria-label="Run detail" className="space-y-4">
        <Skeleton className="h-8 w-64" />
        <Skeleton className="h-96 w-full" />
      </section>
    );
  }

  if (runQuery.isError || !run) {
    const notFound = runQuery.error instanceof ApiError && runQuery.error.status === 404;
    return (
      <section aria-label="Run detail">
        <p role="alert" className="text-sm text-danger">
          {notFound
            ? "This run doesn't exist or you don't have access to it."
            : errorMessage(runQuery.error, 'Failed to load run')}
        </p>
      </section>
    );
  }

  const canCancel =
    capabilities.canManageAgents &&
    !run.cancellationRequested &&
    !NON_CANCELLABLE_STATUSES.has(run.status);

  const agentPromptSnapshot = capabilities.canViewFullPrompts ? run.agentPromptSnapshot : null;
  const requestedParameters = formatJson(run.requestedParametersJson);
  const effectiveParameters = formatJson(run.effectiveParametersJson);
  const retrievalSettings = formatJson(run.retrievalSettingsJson);

  return (
    <div aria-label="Run detail" className="mx-auto max-w-6xl space-y-6">
      <PageHeader
        eyebrow="Agent run"
        icon={PlayCircle}
        title={run.agentName}
        meta={
          <>
            <StatusBadge status={jobStatusToStatusKind(run.status)} />
            {run.cancellationRequested && <Badge variant="outline">Cancellation requested</Badge>}
            <span className="text-sm text-text-muted">
              Requested by {run.requestedBy} on {format(new Date(run.createdAt), 'PP p')}
            </span>
          </>
        }
        actions={
          <>
            {run.generatedOutputId && (
              <Button variant="outline" size="sm" asChild>
                <Link to={`/applications/${applicationId}/outputs/${run.generatedOutputId}`}>
                  View output
                </Link>
              </Button>
            )}
            {capabilities.canManageAgents && run.retryEligible && (
              <Button
                variant="outline"
                size="sm"
                onClick={() => retryMutation.mutate()}
                disabled={retryMutation.isPending}
              >
                <RefreshCwIcon />
                Retry
              </Button>
            )}
            {canCancel && (
              <Button variant="destructive" size="sm" onClick={() => setCancelDialogOpen(true)}>
                <BanIcon />
                Cancel
              </Button>
            )}
          </>
        }
      />

      {run.failureMessage && (
        <div className="rounded-md border border-danger/30 bg-danger/5 p-3 text-sm text-danger">
          {run.failureCategory && <p className="font-medium">{run.failureCategory}</p>}
          <p>{run.failureMessage}</p>
        </div>
      )}

      <div className="grid gap-6 md:grid-cols-[minmax(0,1fr)_20rem]">
        <div className="space-y-6">
          <div className="space-y-1">
            <h2 className="text-sm font-semibold text-text-primary">Request</h2>
            <p className="rounded-md border border-border bg-surface-muted p-3 text-sm whitespace-pre-wrap">
              {run.userRequest}
            </p>
          </div>

          {agentPromptSnapshot && (
            <div className="space-y-1">
              <h2 className="text-sm font-semibold text-text-primary">Agent prompt snapshot</h2>
              <p className="rounded-md border border-border bg-surface-muted p-3 text-sm whitespace-pre-wrap">
                {agentPromptSnapshot}
              </p>
            </div>
          )}

          {run.evidence.length > 0 && (
            <div className="space-y-2">
              <h2 className="text-sm font-semibold text-text-primary">Evidence</h2>
              <ul className="space-y-2">
                {run.evidence.map((item) => (
                  <li key={item.id} className="rounded-md border border-border p-3 text-sm">
                    <div className="flex items-center justify-between gap-2">
                      <span className="font-medium text-text-primary">{item.sourceId}</span>
                      <span className="text-xs text-text-muted">{item.fileName}</span>
                    </div>
                    <p className="text-xs text-text-muted">{item.headingPath}</p>
                    <p className="mt-1 text-text-primary">{item.passage}</p>
                  </li>
                ))}
              </ul>
            </div>
          )}

          {(requestedParameters || effectiveParameters || retrievalSettings) && (
            <div className="space-y-3">
              <h2 className="text-sm font-semibold text-text-primary">Parameters</h2>
              {requestedParameters && (
                <details className="rounded-md border border-border p-3 text-sm">
                  <summary className="cursor-pointer font-medium">Requested parameters</summary>
                  <pre className="mt-2 overflow-x-auto text-xs">{requestedParameters}</pre>
                </details>
              )}
              {effectiveParameters && (
                <details className="rounded-md border border-border p-3 text-sm">
                  <summary className="cursor-pointer font-medium">Effective parameters</summary>
                  <pre className="mt-2 overflow-x-auto text-xs">{effectiveParameters}</pre>
                </details>
              )}
              {retrievalSettings && (
                <details className="rounded-md border border-border p-3 text-sm">
                  <summary className="cursor-pointer font-medium">Retrieval settings</summary>
                  <pre className="mt-2 overflow-x-auto text-xs">{retrievalSettings}</pre>
                </details>
              )}
            </div>
          )}
        </div>

        <div className="space-y-4">
          <h2 className="text-sm font-semibold text-text-primary">Progress</h2>
          <StageTimeline stages={buildRunStages(run.stage, run.status, run.progress)} />
          <dl className="space-y-2 text-sm">
            <div>
              <dt className="text-xs text-text-muted">Correlation ID</dt>
              <dd className="font-mono text-xs break-all text-text-primary">{run.correlationId}</dd>
            </div>
            {run.startedAt && (
              <div>
                <dt className="text-xs text-text-muted">Started</dt>
                <dd className="text-text-primary">{format(new Date(run.startedAt), 'PP p')}</dd>
              </div>
            )}
            {run.completedAt && (
              <div>
                <dt className="text-xs text-text-muted">Completed</dt>
                <dd className="text-text-primary">{format(new Date(run.completedAt), 'PP p')}</dd>
              </div>
            )}
          </dl>
        </div>
      </div>

      <ConfirmDialog
        open={cancelDialogOpen}
        onOpenChange={setCancelDialogOpen}
        title="Cancel this run?"
        description="The run will stop before generation completes. This cannot be undone."
        confirmLabel="Cancel run"
        destructive
        isPending={cancelMutation.isPending}
        onConfirm={() => cancelMutation.mutate()}
      />
    </div>
  );
}
