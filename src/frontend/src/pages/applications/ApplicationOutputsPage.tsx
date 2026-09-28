import { useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { createColumnHelper } from '@tanstack/react-table';
import { format } from 'date-fns';
import { FileOutput } from 'lucide-react';
import { Link, useNavigate, useParams } from 'react-router-dom';

import { agentsQueryKeys, listAgents } from '@/api/agents';
import { ApiError } from '@/api/problemDetails';
import {
  listOutputs,
  outputsQueryKeys,
  type ListOutputsParams,
  type OutputSummaryResponse,
  type RunStatus,
} from '@/api/outputs';
import { useJoinApplicationGroup } from '@/app/realtime/useRealtimeGroups';
import { PageHeader } from '@/components/patterns/PageHeader';
import { Badge } from '@/components/ui/badge';
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

const RUN_STATUSES: RunStatus[] = ['Queued', 'Running', 'Completed', 'Failed', 'Cancelled'];

function errorMessage(error: unknown, fallback: string): string {
  return error instanceof ApiError ? (error.problem.detail ?? error.problem.title) : fallback;
}

/**
 * TASK-OUT-05: `/applications/:applicationId/outputs` - the output library
 * listing. Agent/creator/date-range/status filters are sent to the server
 * (module 23's `ListAsync`, no total-count/paging); free-text search and
 * pagination are handled client-side by the shared `DataTable` (matches the
 * `ApplicationKnowledgePage` convention).
 */
export default function ApplicationOutputsPage() {
  const { applicationId } = useParams<{ applicationId: string }>();
  const navigate = useNavigate();

  // TASK-SIG-05/06: joins `application:{id}` so RunQueued/RunStageChanged/
  // RunCompleted/RunFailed/RunCancelled events invalidate this list live.
  useJoinApplicationGroup(applicationId);

  const [agentFilter, setAgentFilter] = useState<string>('all');
  const [statusFilter, setStatusFilter] = useState<RunStatus | 'all'>('all');
  const [createdByFilter, setCreatedByFilter] = useState('');
  const [fromFilter, setFromFilter] = useState('');
  const [toFilter, setToFilter] = useState('');

  const agentsQuery = useQuery({
    queryKey: agentsQueryKeys.list(applicationId ?? ''),
    queryFn: () => listAgents(applicationId ?? ''),
    enabled: Boolean(applicationId),
  });

  const params: ListOutputsParams = useMemo(
    () => ({
      agentId: agentFilter === 'all' ? undefined : agentFilter,
      status: statusFilter === 'all' ? undefined : statusFilter,
      createdBy: createdByFilter.trim() || undefined,
      from: fromFilter ? new Date(fromFilter).toISOString() : undefined,
      to: toFilter ? new Date(toFilter).toISOString() : undefined,
    }),
    [agentFilter, statusFilter, createdByFilter, fromFilter, toFilter],
  );

  const outputsQuery = useQuery({
    queryKey: outputsQueryKeys.list(applicationId ?? '', params),
    queryFn: () => listOutputs(applicationId ?? '', params),
    enabled: Boolean(applicationId),
  });

  const columnHelper = useMemo(
    () => createColumnHelper<DataTableFeatures, OutputSummaryResponse>(),
    [],
  );

  const columns: DataTableColumnDef<OutputSummaryResponse>[] = useMemo(
    () => [
      columnHelper.accessor('title', {
        header: 'Title',
        cell: (info) => (
          <Link
            to={`/applications/${applicationId}/outputs/${info.row.original.id}`}
            className="font-medium text-text-primary hover:underline"
            onClick={(event) => event.stopPropagation()}
          >
            {info.getValue()}
          </Link>
        ),
      }),
      columnHelper.accessor('agentName', {
        header: 'Agent',
      }),
      columnHelper.accessor('createdBy', {
        header: 'Created by',
      }),
      columnHelper.accessor('createdAt', {
        header: 'Created',
        cell: (info) => format(new Date(info.getValue()), 'PP'),
      }),
      columnHelper.accessor('isEdited', {
        header: 'Edited',
        cell: (info) => (info.getValue() ? <Badge variant="outline">Edited</Badge> : null),
      }),
    ],
    [columnHelper, applicationId],
  );

  return (
    <div aria-labelledby="outputs-heading" className="mx-auto max-w-6xl space-y-6">
      <PageHeader
        eyebrow="Application"
        icon={FileOutput}
        title="Outputs"
        description="Generated, grounded answers from your agent runs — open one to review its content and citations."
      />

      {outputsQuery.isError ? (
        <p role="alert" className="text-sm text-danger">
          {errorMessage(outputsQuery.error, 'Failed to load outputs')}
        </p>
      ) : (
        <DataTable
          columns={columns}
          data={outputsQuery.data ?? []}
          isLoading={outputsQuery.isLoading}
          getRowId={(row) => row.id}
          searchPlaceholder="Search titles..."
          emptyState="No outputs to show."
          onRowClick={(row) => navigate(`/applications/${applicationId}/outputs/${row.id}`)}
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
                onValueChange={(value) => setStatusFilter(value as RunStatus | 'all')}
              >
                <SelectTrigger size="sm" aria-label="Filter by run status">
                  <SelectValue placeholder="All statuses" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">All statuses</SelectItem>
                  {RUN_STATUSES.map((status) => (
                    <SelectItem key={status} value={status}>
                      {status}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>

              <Input
                value={createdByFilter}
                onChange={(event) => setCreatedByFilter(event.target.value)}
                placeholder="Created by..."
                className="h-8 w-40"
                aria-label="Filter by creator"
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
