import { useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { createColumnHelper } from '@tanstack/react-table';
import { format } from 'date-fns';

import {
  auditQueryKeys,
  listApplicationActivity,
  listPlatformAudit,
  type AuditEventResponse,
  type AuditOutcome,
  type AuditScope,
  type ListAuditEventsParams,
} from '@/api/audit';
import { ApiError } from '@/api/problemDetails';
import { DataTable, type DataTableColumnDef } from '@/components/data-table/DataTable';
import type { DataTableFeatures } from '@/components/data-table/table-features';
import { StatusBadge } from '@/components/status/StatusBadge';
import { Input } from '@/components/ui/input';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';

const PAGE_SIZE = 25;
const OUTCOMES: AuditOutcome[] = ['Success', 'Failure'];

function errorMessage(error: unknown, fallback: string): string {
  return error instanceof ApiError ? (error.problem.detail ?? error.problem.title) : fallback;
}

export interface AuditEventsViewProps {
  scope: AuditScope;
}

/**
 * TASK-AUD-03: shared audit-event search table, reused by both
 * `PlatformAuditPage` (platform-wide) and `ApplicationActivityPage`
 * (scoped to one application). Both routes hit `CanViewAudit`-gated
 * endpoints (platform roles only - see `AuditEndpoints.cs`), so this view
 * has no row actions and no resource-authorization check of its own.
 */
interface AuditFilters {
  action: string;
  entityType: string;
  outcome: AuditOutcome | 'all';
  from: string;
  to: string;
}

const EMPTY_FILTERS: AuditFilters = {
  action: '',
  entityType: '',
  outcome: 'all',
  from: '',
  to: '',
};

export function AuditEventsView({ scope }: AuditEventsViewProps) {
  const [filters, setFilters] = useState<AuditFilters>(EMPTY_FILTERS);
  const [pageIndex, setPageIndex] = useState(0);

  // Any filter change resets to the first page (in the same handler, not an
  // effect) - otherwise a narrower filter could leave the user stranded on a
  // page beyond the new result count.
  function updateFilter<K extends keyof AuditFilters>(key: K, value: AuditFilters[K]) {
    setFilters((previous) => ({ ...previous, [key]: value }));
    setPageIndex(0);
  }

  const params: ListAuditEventsParams = useMemo(
    () => ({
      action: filters.action || undefined,
      entityType: filters.entityType || undefined,
      outcome: filters.outcome === 'all' ? undefined : filters.outcome,
      from: filters.from ? new Date(filters.from).toISOString() : undefined,
      to: filters.to ? new Date(filters.to).toISOString() : undefined,
      page: pageIndex + 1,
      pageSize: PAGE_SIZE,
    }),
    [filters, pageIndex],
  );

  const listQuery = useQuery({
    queryKey: auditQueryKeys.list(scope, params),
    queryFn: () =>
      'applicationId' in scope
        ? listApplicationActivity(scope.applicationId, params)
        : listPlatformAudit(params),
  });

  const columnHelper = useMemo(
    () => createColumnHelper<DataTableFeatures, AuditEventResponse>(),
    [],
  );

  const columns: DataTableColumnDef<AuditEventResponse>[] = useMemo(
    () => [
      columnHelper.accessor('occurredAt', {
        header: 'When',
        cell: (info) => format(new Date(info.getValue()), 'PP p'),
      }),
      columnHelper.accessor('action', { header: 'Action' }),
      columnHelper.accessor('outcome', {
        header: 'Outcome',
        cell: (info) => (
          <StatusBadge
            status={info.getValue() === 'Success' ? 'ready' : 'failed'}
            label={info.getValue() === 'Success' ? 'Success' : 'Failure'}
          />
        ),
      }),
      columnHelper.accessor('entityType', {
        header: 'Entity type',
        cell: (info) => info.getValue() ?? '—',
      }),
      columnHelper.accessor('entityDisplayName', {
        header: 'Entity',
        cell: (info) => info.getValue() ?? '—',
      }),
      columnHelper.accessor('failureReason', {
        header: 'Failure reason',
        cell: (info) => info.getValue() ?? '—',
      }),
      columnHelper.accessor('correlationId', {
        header: 'Correlation ID',
        cell: (info) => info.getValue() ?? '—',
      }),
    ],
    [columnHelper],
  );

  if (listQuery.isError) {
    return (
      <p role="alert" className="text-sm text-danger">
        {errorMessage(listQuery.error, 'Failed to load audit events')}
      </p>
    );
  }

  return (
    <DataTable
      columns={columns}
      data={listQuery.data?.items ?? []}
      isLoading={listQuery.isLoading}
      getRowId={(row) => row.id}
      searchPlaceholder="Filter loaded rows..."
      emptyState="No audit events to show."
      manualPagination
      rowCount={listQuery.data?.totalCount ?? 0}
      pagination={{ pageIndex, pageSize: PAGE_SIZE }}
      onPaginationChange={(updater) => {
        const next =
          typeof updater === 'function' ? updater({ pageIndex, pageSize: PAGE_SIZE }) : updater;
        setPageIndex(next.pageIndex);
      }}
      toolbarFilters={
        <div className="flex flex-wrap items-center gap-2">
          <Input
            value={filters.action}
            onChange={(event) => updateFilter('action', event.target.value)}
            placeholder="Action..."
            className="h-8 w-40"
            aria-label="Filter by action"
          />
          <Input
            value={filters.entityType}
            onChange={(event) => updateFilter('entityType', event.target.value)}
            placeholder="Entity type..."
            className="h-8 w-36"
            aria-label="Filter by entity type"
          />
          <Select
            value={filters.outcome}
            onValueChange={(value) => updateFilter('outcome', value as AuditOutcome | 'all')}
          >
            <SelectTrigger size="sm" aria-label="Filter by outcome">
              <SelectValue placeholder="All outcomes" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All outcomes</SelectItem>
              {OUTCOMES.map((outcome) => (
                <SelectItem key={outcome} value={outcome}>
                  {outcome}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
          <Input
            type="date"
            value={filters.from}
            onChange={(event) => updateFilter('from', event.target.value)}
            className="h-8 w-36"
            aria-label="Occurred from"
          />
          <Input
            type="date"
            value={filters.to}
            onChange={(event) => updateFilter('to', event.target.value)}
            className="h-8 w-36"
            aria-label="Occurred to"
          />
        </div>
      }
    />
  );
}
