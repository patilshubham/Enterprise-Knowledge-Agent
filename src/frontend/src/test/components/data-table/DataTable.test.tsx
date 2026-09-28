import { fireEvent, render, screen, within } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { createColumnHelper } from '@tanstack/react-table';
import { DataTable, type DataTableColumnDef } from '@/components/data-table/DataTable';
import type { DataTableFeatures } from '@/components/data-table/table-features';

interface Person {
  id: string;
  name: string;
  age: number;
  status: string;
}

function makePeople(count: number): Person[] {
  return Array.from({ length: count }, (_, index) => ({
    id: `person-${index}`,
    name: `Person ${String(count - index).padStart(2, '0')}`,
    age: 20 + index,
    status: index % 2 === 0 ? 'Active' : 'Disabled',
  }));
}

const columnHelper = createColumnHelper<DataTableFeatures, Person>();
const columns: DataTableColumnDef<Person>[] = [
  columnHelper.accessor('name', { header: 'Name' }),
  columnHelper.accessor('age', { header: 'Age' }),
  columnHelper.accessor('status', { header: 'Status' }),
];

function renderTable(props: Partial<React.ComponentProps<typeof DataTable<Person>>> = {}) {
  render(<DataTable<Person> columns={columns} data={props.data ?? makePeople(3)} {...props} />);
  // The desktop `<table>` is the source of truth for row content; the mobile
  // card fallback renders the same data simultaneously in jsdom (visibility
  // is CSS-only), so every assertion below is scoped to the desktop table to
  // avoid "multiple elements found" ambiguity.
  return within(screen.getAllByRole('table')[0]);
}

describe('DataTable (TASK-UX-06)', () => {
  it('renders provided rows', () => {
    const table = renderTable({ data: makePeople(2) });

    expect(table.getByText('Person 02')).toBeInTheDocument();
    expect(table.getByText('Person 01')).toBeInTheDocument();
  });

  it('shows an empty state when there is no data', () => {
    const table = renderTable({ data: [] });

    expect(table.getByText('No results.')).toBeInTheDocument();
  });

  it('renders a custom empty state node', () => {
    const table = renderTable({ data: [], emptyState: 'Nothing to show yet' });

    expect(table.getByText('Nothing to show yet')).toBeInTheDocument();
  });

  it('shows loading skeleton rows instead of data', () => {
    const table = renderTable({ data: makePeople(3), isLoading: true, skeletonRowCount: 2 });

    expect(table.queryByText('Person 03')).not.toBeInTheDocument();
    const rows = table.getAllByRole('row');
    // header row + 2 skeleton rows
    expect(rows).toHaveLength(3);
  });

  it('filters rows via the search input (client-side global filter)', async () => {
    const table = renderTable({ data: makePeople(3) });

    fireEvent.change(screen.getByLabelText('Search'), { target: { value: 'Person 01' } });

    expect(await table.findByText('Person 01')).toBeInTheDocument();
    expect(table.queryByText('Person 02')).not.toBeInTheDocument();
    expect(table.queryByText('Person 03')).not.toBeInTheDocument();
  });

  it('sorts rows when a sortable column header is clicked', () => {
    const table = renderTable({ data: makePeople(3) });

    const nameHeaderButton = table.getByRole('button', { name: /Name/ });

    // Default order: Person 03, Person 02, Person 01 (descending by construction).
    let bodyRows = table.getAllByRole('row').slice(1);
    expect(within(bodyRows[0]).getByText('Person 03')).toBeInTheDocument();

    fireEvent.click(nameHeaderButton);
    bodyRows = table.getAllByRole('row').slice(1);
    expect(within(bodyRows[0]).getByText('Person 01')).toBeInTheDocument();
  });

  it('paginates rows client-side', () => {
    renderTable({ data: makePeople(15) });

    expect(screen.getByText('Page 1 of 2')).toBeInTheDocument();
    const previousButton = screen.getByRole('button', { name: 'Previous' });
    const nextButton = screen.getByRole('button', { name: 'Next' });
    expect(previousButton).toBeDisabled();
    expect(nextButton).not.toBeDisabled();

    fireEvent.click(nextButton);

    expect(screen.getByText('Page 2 of 2')).toBeInTheDocument();
    expect(previousButton).not.toBeDisabled();
  });

  it('gates row actions per-row: only rows with actions show the menu trigger', () => {
    const people = makePeople(2);
    const onDelete = vi.fn();
    const table = renderTable({
      data: people,
      rowActions: (row) =>
        row.id === people[0].id
          ? [{ id: 'delete', label: 'Delete', destructive: true, onSelect: onDelete }]
          : [],
    });

    // Only the one row with a non-empty actions array renders a menu trigger.
    expect(table.getAllByRole('button', { name: 'Row actions' })).toHaveLength(1);
  });
});
