import { type ReactNode, useMemo, useState } from 'react';
import {
  ChevronDown,
  ChevronsUpDown,
  ChevronUp,
  Columns3,
  Search as SearchIcon,
} from 'lucide-react';
import type {
  ColumnDef,
  ColumnVisibilityState,
  PaginationState,
  RowData,
  SortingState,
} from '@tanstack/react-table';
import { createColumnHelper, useTable } from '@tanstack/react-table';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import {
  DropdownMenu,
  DropdownMenuCheckboxItem,
  DropdownMenuContent,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { cn } from '@/lib/utils';
import { DataTableRowActions, type DataTableRowAction } from './DataTableRowActions';
import { dataTableFeatures, type DataTableFeatures } from './table-features';

export type DataTableColumnDef<TData extends RowData> = ColumnDef<
  DataTableFeatures,
  TData,
  // eslint-disable-next-line @typescript-eslint/no-explicit-any -- column value types vary per column; consumers still get TData/accessor-key inference.
  any
>;

export interface DataTableProps<TData extends RowData> {
  /** Column definitions built with `createColumnHelper<DataTableFeatures, TData>()`. */
  columns: DataTableColumnDef<TData>[];
  data: TData[];
  isLoading?: boolean;
  getRowId?: (row: TData, index: number) => string;
  /** Rendered instead of the default "No results" message when the table is empty. */
  emptyState?: ReactNode;
  searchPlaceholder?: string;
  /** Slot for extra filter controls (status/date selects, etc.), rendered next to search. */
  toolbarFilters?: ReactNode;
  /** Whole-row click navigates to the row's detail view. */
  onRowClick?: (row: TData) => void;
  /** Per-row action-menu items; omit (or return an empty array) to hide the menu for that row. */
  rowActions?: (row: TData) => DataTableRowAction[];
  skeletonRowCount?: number;

  /** Server-driven pagination: pass `rowCount` and drive `pagination` from your query. */
  manualPagination?: boolean;
  rowCount?: number;
  pagination?: PaginationState;
  onPaginationChange?: (
    updater: PaginationState | ((old: PaginationState) => PaginationState),
  ) => void;

  /** Server-driven sorting: pass `sorting` + `onSortingChange` and set `manualSorting`. */
  manualSorting?: boolean;
  sorting?: SortingState;
  onSortingChange?: (updater: SortingState | ((old: SortingState) => SortingState)) => void;

  /** Server-driven search: pass `globalFilter` + `onGlobalFilterChange` and set `manualFiltering`. */
  manualFiltering?: boolean;
  globalFilter?: string;
  onGlobalFilterChange?: (updater: string | ((old: string) => string)) => void;

  columnVisibility?: ColumnVisibilityState;
  onColumnVisibilityChange?: (
    updater: ColumnVisibilityState | ((old: ColumnVisibilityState) => ColumnVisibilityState),
  ) => void;
}

const DEFAULT_PAGE_SIZE = 10;

/**
 * TASK-UX-06: the shared enterprise data-table pattern (search, filter slot,
 * sort, column visibility, pagination, row actions in a menu, whole-row
 * click, empty/loading states, responsive card fallback). Every list screen
 * (knowledge files, users, processing jobs, agents, runs, outputs, audit)
 * should consume this instead of hand-rolling table markup.
 *
 * State is "internal unless controlled": pass e.g. `sorting` +
 * `onSortingChange` + `manualSorting` to drive a slice from a server query;
 * omit them to let the table own that slice with the full client dataset.
 */
export function DataTable<TData extends RowData>({
  columns,
  data,
  isLoading = false,
  getRowId,
  emptyState,
  searchPlaceholder = 'Search...',
  toolbarFilters,
  onRowClick,
  rowActions,
  skeletonRowCount = 5,
  manualPagination = false,
  rowCount,
  pagination: controlledPagination,
  onPaginationChange,
  manualSorting = false,
  sorting: controlledSorting,
  onSortingChange,
  manualFiltering = false,
  globalFilter: controlledGlobalFilter,
  onGlobalFilterChange,
  columnVisibility: controlledColumnVisibility,
  onColumnVisibilityChange,
}: DataTableProps<TData>) {
  const [internalSorting, setInternalSorting] = useState<SortingState>([]);
  const [internalPagination, setInternalPagination] = useState<PaginationState>({
    pageIndex: 0,
    pageSize: DEFAULT_PAGE_SIZE,
  });
  const [internalGlobalFilter, setInternalGlobalFilter] = useState('');
  const [internalColumnVisibility, setInternalColumnVisibility] = useState<ColumnVisibilityState>(
    {},
  );

  const sorting = controlledSorting ?? internalSorting;
  const pagination = controlledPagination ?? internalPagination;
  const globalFilter = controlledGlobalFilter ?? internalGlobalFilter;
  const columnVisibility = controlledColumnVisibility ?? internalColumnVisibility;

  const columnsWithActions = useMemo(() => {
    if (!rowActions) {
      return columns;
    }
    const columnHelper = createColumnHelper<DataTableFeatures, TData>();
    return [
      ...columns,
      columnHelper.display({
        id: 'actions',
        header: '',
        cell: ({ row }) => <DataTableRowActions actions={rowActions(row.original)} />,
      }),
    ];
  }, [columns, rowActions]);

  const table = useTable({
    features: dataTableFeatures,
    columns: columnsWithActions,
    data,
    getRowId,
    state: { sorting, pagination, globalFilter, columnVisibility },
    onSortingChange: onSortingChange ?? setInternalSorting,
    onPaginationChange: onPaginationChange ?? setInternalPagination,
    onGlobalFilterChange: onGlobalFilterChange ?? setInternalGlobalFilter,
    onColumnVisibilityChange: onColumnVisibilityChange ?? setInternalColumnVisibility,
    manualSorting,
    manualPagination,
    manualFiltering,
    rowCount: manualPagination ? (rowCount ?? data.length) : undefined,
    getColumnCanGlobalFilter: (column) => column.id !== 'actions',
  });

  const rows = table.getRowModel().rows;
  const hasRows = rows.length > 0;
  const hideableColumns = table.getAllLeafColumns().filter((column) => column.getCanHide());

  return (
    <div className="space-y-3">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div className="relative w-full sm:max-w-xs">
          <SearchIcon
            className="pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2 text-text-muted"
            aria-hidden="true"
          />
          <Input
            value={globalFilter}
            onChange={(event) => table.setGlobalFilter(event.target.value)}
            placeholder={searchPlaceholder}
            aria-label="Search"
            className="pl-8"
          />
        </div>
        <div className="flex flex-wrap items-center gap-2">
          {toolbarFilters}
          {hideableColumns.length > 0 && (
            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <Button variant="outline" size="sm">
                  <Columns3 className="size-4" aria-hidden="true" />
                  Columns
                </Button>
              </DropdownMenuTrigger>
              <DropdownMenuContent align="end">
                {hideableColumns.map((column) => (
                  <DropdownMenuCheckboxItem
                    key={column.id}
                    checked={column.getIsVisible()}
                    onCheckedChange={(value) => column.toggleVisibility(!!value)}
                    onSelect={(event) => event.preventDefault()}
                  >
                    {typeof column.columnDef.header === 'string'
                      ? column.columnDef.header
                      : column.id}
                  </DropdownMenuCheckboxItem>
                ))}
              </DropdownMenuContent>
            </DropdownMenu>
          )}
        </div>
      </div>

      <div className="hidden overflow-hidden rounded-card border border-border md:block">
        <Table>
          <TableHeader>
            {table.getHeaderGroups().map((headerGroup) => (
              <TableRow key={headerGroup.id} className="hover:bg-transparent">
                {headerGroup.headers.map((header) => (
                  <TableHead key={header.id}>
                    {header.isPlaceholder ? null : header.column.getCanSort() ? (
                      <button
                        type="button"
                        className="flex items-center gap-1 text-xs font-medium text-text-muted hover:text-text-primary"
                        onClick={header.column.getToggleSortingHandler()}
                      >
                        <table.FlexRender header={header} />
                        {header.column.getIsSorted() === 'asc' ? (
                          <ChevronUp className="size-3.5" aria-hidden="true" />
                        ) : header.column.getIsSorted() === 'desc' ? (
                          <ChevronDown className="size-3.5" aria-hidden="true" />
                        ) : (
                          <ChevronsUpDown className="size-3.5 opacity-50" aria-hidden="true" />
                        )}
                      </button>
                    ) : (
                      <table.FlexRender header={header} />
                    )}
                  </TableHead>
                ))}
              </TableRow>
            ))}
          </TableHeader>
          <TableBody>
            {isLoading ? (
              Array.from({ length: skeletonRowCount }).map((_, rowIndex) => (
                <TableRow key={`skeleton-${rowIndex}`} className="hover:bg-transparent">
                  {columnsWithActions.map((_, columnIndex) => (
                    <TableCell key={`skeleton-cell-${columnIndex}`}>
                      <Skeleton className="h-4 w-full max-w-32" />
                    </TableCell>
                  ))}
                </TableRow>
              ))
            ) : hasRows ? (
              rows.map((row) => (
                <TableRow
                  key={row.id}
                  onClick={onRowClick ? () => onRowClick(row.original) : undefined}
                  className={cn(onRowClick && 'cursor-pointer')}
                >
                  {row.getVisibleCells().map((cell) => (
                    <TableCell key={cell.id}>
                      <table.FlexRender cell={cell} />
                    </TableCell>
                  ))}
                </TableRow>
              ))
            ) : (
              <TableRow className="hover:bg-transparent">
                <TableCell
                  colSpan={columnsWithActions.length}
                  className="h-24 text-center text-text-muted"
                >
                  {emptyState ?? 'No results.'}
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
      </div>

      <div className="grid gap-3 md:hidden">
        {isLoading ? (
          Array.from({ length: skeletonRowCount }).map((_, rowIndex) => (
            <Card key={`mobile-skeleton-${rowIndex}`}>
              <CardContent className="space-y-2 p-4">
                <Skeleton className="h-4 w-2/3" />
                <Skeleton className="h-4 w-1/2" />
              </CardContent>
            </Card>
          ))
        ) : hasRows ? (
          rows.map((row) => (
            <Card
              key={row.id}
              onClick={onRowClick ? () => onRowClick(row.original) : undefined}
              className={cn(onRowClick && 'cursor-pointer')}
            >
              <CardContent className="space-y-2 p-4">
                {row
                  .getVisibleCells()
                  .filter((cell) => cell.column.id !== 'actions')
                  .map((cell) => (
                    <div key={cell.id} className="flex items-center justify-between gap-4 text-sm">
                      <span className="text-text-muted">
                        {typeof cell.column.columnDef.header === 'string'
                          ? cell.column.columnDef.header
                          : cell.column.id}
                      </span>
                      <span className="text-text-primary">
                        <table.FlexRender cell={cell} />
                      </span>
                    </div>
                  ))}
                {rowActions && (
                  <div className="flex justify-end" onClick={(event) => event.stopPropagation()}>
                    <DataTableRowActions actions={rowActions(row.original)} />
                  </div>
                )}
              </CardContent>
            </Card>
          ))
        ) : (
          <Card>
            <CardContent className="p-4 text-center text-sm text-text-muted">
              {emptyState ?? 'No results.'}
            </CardContent>
          </Card>
        )}
      </div>

      <div className="flex flex-col items-center justify-between gap-3 sm:flex-row">
        <p className="text-xs text-text-muted">
          Page {table.state.pagination.pageIndex + 1} of {Math.max(table.getPageCount(), 1)}
        </p>
        <div className="flex items-center gap-2">
          <Button
            variant="outline"
            size="sm"
            onClick={() => table.previousPage()}
            disabled={!table.getCanPreviousPage()}
          >
            Previous
          </Button>
          <Button
            variant="outline"
            size="sm"
            onClick={() => table.nextPage()}
            disabled={!table.getCanNextPage()}
          >
            Next
          </Button>
        </div>
      </div>
    </div>
  );
}
