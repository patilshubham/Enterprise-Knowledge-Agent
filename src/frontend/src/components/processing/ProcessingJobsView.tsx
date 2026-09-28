import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { createColumnHelper } from '@tanstack/react-table';
import { format } from 'date-fns';
import { RefreshCwIcon } from 'lucide-react';
import { toast } from 'sonner';

import {
  getApplicationProcessingDetail,
  getPlatformProcessingDetail,
  listApplicationProcessing,
  listPlatformProcessing,
  processingJobsQueryKeys,
  retryProcessingJob,
  type JobStatus,
  type ListProcessingJobsParams,
  type ProcessingJobSummaryResponse,
  type ProcessingJobsScope,
} from '@/api/processingJobs';
import { ApiError } from '@/api/problemDetails';
import { DataTable, type DataTableColumnDef } from '@/components/data-table/DataTable';
import type { DataTableRowAction } from '@/components/data-table/DataTableRowActions';
import type { DataTableFeatures } from '@/components/data-table/table-features';
import { DetailDrawer } from '@/components/timeline/DetailDrawer';
import { StatusBadge } from '@/components/status/StatusBadge';
import { Input } from '@/components/ui/input';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { buildProcessingStages, jobStatusToStatusKind } from '@/lib/jobStatusMapping';

const JOB_STATUSES: JobStatus[] = ['Queued', 'Running', 'Completed', 'Failed', 'Cancelled'];

function errorMessage(error: unknown, fallback: string): string {
  return error instanceof ApiError ? (error.problem.detail ?? error.problem.title) : fallback;
}

export interface ProcessingJobsViewProps {
  scope: ProcessingJobsScope;
  /** Gates the retry action - callers compute this from `useCapabilities` (server re-checks regardless). */
  canRetry: boolean;
}

/**
 * TASK-PRDSH-01/02: shared processing-job listing + detail drawer, reused by
 * both `PlatformProcessingPage` (platform-wide) and `ApplicationProcessingPage`
 * (scoped). Row click opens a `DetailDrawer` (TASK-UX-09) with a
 * `StageTimeline` for the job's 9-stage pipeline instead of navigating to a
 * dedicated detail route - processing jobs have no owning full page, unlike
 * agent runs.
 */
export function ProcessingJobsView({ scope, canRetry }: ProcessingJobsViewProps) {
  const queryClient = useQueryClient();
  const [statusFilter, setStatusFilter] = useState<JobStatus | 'all'>('all');
  const [fromFilter, setFromFilter] = useState('');
  const [toFilter, setToFilter] = useState('');
  const [openJobId, setOpenJobId] = useState<string | null>(null);

  const params: ListProcessingJobsParams = useMemo(
    () => ({
      status: statusFilter === 'all' ? undefined : statusFilter,
      from: fromFilter ? new Date(fromFilter).toISOString() : undefined,
      to: toFilter ? new Date(toFilter).toISOString() : undefined,
    }),
    [statusFilter, fromFilter, toFilter],
  );

  const listQuery = useQuery({
    queryKey: processingJobsQueryKeys.list(scope, params),
    queryFn: () =>
      'applicationId' in scope
        ? listApplicationProcessing(scope.applicationId, params)
        : listPlatformProcessing(params),
  });

  const detailQuery = useQuery({
    queryKey: processingJobsQueryKeys.detail(scope, openJobId ?? ''),
    queryFn: () =>
      'applicationId' in scope
        ? getApplicationProcessingDetail(scope.applicationId, openJobId ?? '')
        : getPlatformProcessingDetail(openJobId ?? ''),
    enabled: Boolean(openJobId),
  });

  const retryMutation = useMutation({
    mutationFn: (job: Pick<ProcessingJobSummaryResponse, 'id' | 'applicationId'>) =>
      retryProcessingJob(job.applicationId, job.id),
    onSuccess: async () => {
      toast.success('Job retry queued');
      await queryClient.invalidateQueries({ queryKey: processingJobsQueryKeys.all });
    },
    onError: (error) => toast.error(errorMessage(error, 'Failed to retry job')),
  });

  const columnHelper = useMemo(
    () => createColumnHelper<DataTableFeatures, ProcessingJobSummaryResponse>(),
    [],
  );

  const columns: DataTableColumnDef<ProcessingJobSummaryResponse>[] = useMemo(
    () => [
      columnHelper.accessor('fileName', { header: 'File' }),
      columnHelper.accessor('status', {
        header: 'Status',
        cell: (info) => <StatusBadge status={jobStatusToStatusKind(info.getValue())} />,
      }),
      columnHelper.accessor('stage', { header: 'Stage' }),
      columnHelper.accessor('progress', {
        header: 'Progress',
        cell: (info) => `${info.getValue()}%`,
      }),
      columnHelper.accessor('attemptNumber', { header: 'Attempt' }),
      columnHelper.accessor('createdAt', {
        header: 'Created',
        cell: (info) => format(new Date(info.getValue()), 'PP p'),
      }),
    ],
    [columnHelper],
  );

  function rowActions(row: ProcessingJobSummaryResponse): DataTableRowAction[] {
    if (!canRetry || !row.retryEligible) {
      return [];
    }
    return [
      {
        id: 'retry',
        label: 'Retry',
        icon: <RefreshCwIcon />,
        onSelect: () => retryMutation.mutate(row),
      },
    ];
  }

  const openJobSummary = listQuery.data?.find((job) => job.id === openJobId);
  const detail = detailQuery.data;

  return (
    <>
      {listQuery.isError ? (
        <p role="alert" className="text-sm text-danger">
          {errorMessage(listQuery.error, 'Failed to load processing jobs')}
        </p>
      ) : (
        <DataTable
          columns={columns}
          data={listQuery.data ?? []}
          isLoading={listQuery.isLoading}
          getRowId={(row) => row.id}
          searchPlaceholder="Search files..."
          emptyState="No processing jobs to show."
          onRowClick={(row) => setOpenJobId(row.id)}
          rowActions={rowActions}
          toolbarFilters={
            <div className="flex flex-wrap items-center gap-2">
              <Select
                value={statusFilter}
                onValueChange={(value) => setStatusFilter(value as JobStatus | 'all')}
              >
                <SelectTrigger size="sm" aria-label="Filter by status">
                  <SelectValue placeholder="All statuses" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">All statuses</SelectItem>
                  {JOB_STATUSES.map((status) => (
                    <SelectItem key={status} value={status}>
                      {status}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>

              <Input
                type="date"
                value={fromFilter}
                onChange={(event) => setFromFilter(event.target.value)}
                className="h-8 w-36"
                aria-label="Created from"
              />
              <Input
                type="date"
                value={toFilter}
                onChange={(event) => setToFilter(event.target.value)}
                className="h-8 w-36"
                aria-label="Created to"
              />
            </div>
          }
        />
      )}

      <DetailDrawer
        open={openJobId !== null}
        onOpenChange={(open) => !open && setOpenJobId(null)}
        title={openJobSummary?.fileName ?? 'Processing job'}
        status={jobStatusToStatusKind(detail?.status ?? openJobSummary?.status ?? 'Queued')}
        stages={detail ? buildProcessingStages(detail.stage, detail.status, detail.progress) : []}
        startedAt={detail?.startedAt ? format(new Date(detail.startedAt), 'PP p') : undefined}
        correlationId={detail?.correlationId}
        onRetry={canRetry && detail?.retryEligible ? () => retryMutation.mutate(detail) : undefined}
      >
        {detail?.failureMessage && (
          <div className="rounded-md border border-danger/30 bg-danger/5 p-3 text-sm text-danger">
            {detail.failureCategory && <p className="font-medium">{detail.failureCategory}</p>}
            <p>{detail.failureMessage}</p>
          </div>
        )}
      </DetailDrawer>
    </>
  );
}
