import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { createColumnHelper } from '@tanstack/react-table';
import { format } from 'date-fns';
import { PlayCircle, PlusIcon } from 'lucide-react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { toast } from 'sonner';

import { agentsQueryKeys, listAgents } from '@/api/agents';
import { ApiError } from '@/api/problemDetails';
import {
  listRuns,
  runsQueryKeys,
  submitRun,
  type AgentRunSummaryResponse,
  type JobStatus,
  type ListRunsParams,
} from '@/api/runs';
import { useJoinApplicationGroup } from '@/app/realtime/useRealtimeGroups';
import { useCapabilities } from '@/app/authz/useCapabilities';
import { PageHeader } from '@/components/patterns/PageHeader';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { DataTable, type DataTableColumnDef } from '@/components/data-table/DataTable';
import type { DataTableFeatures } from '@/components/data-table/table-features';
import { RunAgentDialog } from '@/components/runs/RunAgentDialog';
import { StatusBadge } from '@/components/status/StatusBadge';
import { jobStatusToStatusKind } from '@/lib/jobStatusMapping';

const JOB_STATUSES: JobStatus[] = ['Queued', 'Running', 'Completed', 'Failed', 'Cancelled'];

function errorMessage(error: unknown, fallback: string): string {
  return error instanceof ApiError ? (error.problem.detail ?? error.problem.title) : fallback;
}

/**
 * TASK-RDSH-01: `/applications/:applicationId/runs` - the agent-run
 * dashboard listing. Mirrors `ApplicationOutputsPage`'s filter/DataTable
 * convention almost exactly; row click navigates to the full
 * `RunDetailPage` (unlike processing jobs, which use a side drawer).
 */
export default function ApplicationRunsPage() {
  const { applicationId } = useParams<{ applicationId: string }>();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const capabilities = useCapabilities(applicationId);

  // TASK-SIG-05/06: joins `application:{id}` so Run* events invalidate this list live.
  useJoinApplicationGroup(applicationId);

  const [agentFilter, setAgentFilter] = useState<string>('all');
  const [statusFilter, setStatusFilter] = useState<JobStatus | 'all'>('all');
  const [createdByFilter, setCreatedByFilter] = useState('');
  const [fromFilter, setFromFilter] = useState('');
  const [toFilter, setToFilter] = useState('');
  const [isRunDialogOpen, setIsRunDialogOpen] = useState(false);

  const agentsQuery = useQuery({
    queryKey: agentsQueryKeys.list(applicationId ?? ''),
    queryFn: () => listAgents(applicationId ?? ''),
    enabled: Boolean(applicationId),
  });

  const runnableAgents = useMemo(
    () => (agentsQuery.data ?? []).filter((agent) => agent.status === 'Active'),
    [agentsQuery.data],
  );

  const params: ListRunsParams = useMemo(
    () => ({
      agentId: agentFilter === 'all' ? undefined : agentFilter,
      status: statusFilter === 'all' ? undefined : statusFilter,
      createdBy: createdByFilter.trim() || undefined,
      from: fromFilter ? new Date(fromFilter).toISOString() : undefined,
      to: toFilter ? new Date(toFilter).toISOString() : undefined,
    }),
    [agentFilter, statusFilter, createdByFilter, fromFilter, toFilter],
  );

  const runsQuery = useQuery({
    queryKey: runsQueryKeys.list(applicationId ?? '', params),
    queryFn: () => listRuns(applicationId ?? '', params),
    enabled: Boolean(applicationId),
  });

  const runMutation = useMutation({
    mutationFn: (values: { agentId: string; userRequest: string; outputTitle?: string }) =>
      submitRun(applicationId ?? '', values.agentId, {
        userRequest: values.userRequest,
        outputTitle: values.outputTitle || null,
        idempotencyKey: crypto.randomUUID(),
      }),
    onSuccess: async (response) => {
      setIsRunDialogOpen(false);
      await queryClient.invalidateQueries({ queryKey: runsQueryKeys.list(applicationId ?? '') });
      toast.success('Run submitted');
      navigate(`/applications/${applicationId}/runs/${response.runId}`);
    },
    onError: (error) => toast.error(errorMessage(error, 'Failed to submit run')),
  });

  const columnHelper = useMemo(
    () => createColumnHelper<DataTableFeatures, AgentRunSummaryResponse>(),
    [],
  );

  const columns: DataTableColumnDef<AgentRunSummaryResponse>[] = useMemo(
    () => [
      columnHelper.accessor('agentName', {
        header: 'Agent',
        cell: (info) => (
          <Link
            to={`/applications/${applicationId}/runs/${info.row.original.id}`}
            className="font-medium text-text-primary hover:underline"
            onClick={(event) => event.stopPropagation()}
          >
            {info.getValue()}
          </Link>
        ),
      }),
      columnHelper.accessor('status', {
        header: 'Status',
        cell: (info) => <StatusBadge status={jobStatusToStatusKind(info.getValue())} />,
      }),
      columnHelper.accessor('stage', { header: 'Stage' }),
      columnHelper.accessor('requestedBy', { header: 'Requested by' }),
      columnHelper.accessor('createdAt', {
        header: 'Created',
        cell: (info) => format(new Date(info.getValue()), 'PP'),
      }),
    ],
    [columnHelper, applicationId],
  );

  return (
    <div aria-labelledby="runs-heading" className="mx-auto max-w-6xl space-y-6">
      <PageHeader
        eyebrow="Application"
        icon={PlayCircle}
        title="Agent runs"
        description="Each execution of an agent against a request. Open a run to watch its stages and see the output."
        actions={
          capabilities.canExecuteAgent && (
            <Button size="sm" onClick={() => setIsRunDialogOpen(true)}>
              <PlusIcon className="size-4" />
              New run
            </Button>
          )
        }
      />

      <RunAgentDialog
        open={isRunDialogOpen}
        onOpenChange={setIsRunDialogOpen}
        agentOptions={runnableAgents.map((agent) => ({ id: agent.id, name: agent.name }))}
        isPending={runMutation.isPending}
        onSubmit={(values) =>
          runMutation.mutate({
            agentId: values.agentId ?? '',
            userRequest: values.userRequest,
            outputTitle: values.outputTitle,
          })
        }
      />

      {runsQuery.isError ? (
        <p role="alert" className="text-sm text-danger">
          {errorMessage(runsQuery.error, 'Failed to load agent runs')}
        </p>
      ) : (
        <DataTable
          columns={columns}
          data={runsQuery.data ?? []}
          isLoading={runsQuery.isLoading}
          getRowId={(row) => row.id}
          searchPlaceholder="Search runs..."
          emptyState="No agent runs to show."
          onRowClick={(row) => navigate(`/applications/${applicationId}/runs/${row.id}`)}
          toolbarFilters={
            <div className="flex flex-wrap items-center gap-2">
              <Select value={agentFilter} onValueChange={setAgentFilter}>
                <SelectTrigger size="sm" aria-label="Filter by agent">
                  <SelectValue placeholder="All agents" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">All agents</SelectItem>
                  {(agentsQuery.data ?? []).map((agent) => (
                    <SelectItem key={agent.id} value={agent.id}>
                      {agent.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>

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
                value={createdByFilter}
                onChange={(event) => setCreatedByFilter(event.target.value)}
                placeholder="Requested by..."
                className="h-8 w-40"
                aria-label="Filter by requester"
              />

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
    </div>
  );
}
