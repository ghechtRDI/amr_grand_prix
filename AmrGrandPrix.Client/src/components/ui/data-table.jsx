import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  useReactTable,
  getCoreRowModel,
  getSortedRowModel,
  getFilteredRowModel,
  getPaginationRowModel,
  flexRender,
} from '@tanstack/react-table';
import { ArrowDown, ArrowUp, ArrowUpDown, ChevronLeft, ChevronRight } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from '@/components/ui/table';
import { SearchInput } from '@/components/ui/search-input';
import { cn } from '@/lib/utils';

/**
 * A sortable, globally-searchable table built on the @tanstack/react-table primitives already
 * used elsewhere in the app (see components/upload/DataReviewStep.jsx). Pass `columns` built with
 * `createColumnHelper()` and `data`; sorting and the search box are wired up here so callers don't
 * each hand-roll their own sort state (see the pre-existing pattern in pages/RaceResults.jsx).
 *
 * `initialSorting` sets the starting sort (TanStack `SortingState`, e.g. `[{ id: 'points', desc: true }]`).
 * `pageSize` turns on client-side paging for long tables; leave it unset to show every row.
 * `getRowHref(rowData)` makes each row clickable, going to the returned path (cmd/ctrl/middle-click
 * opens a new tab). It is a mouse convenience: put a real <Link> in one of the cells so the row is
 * reachable by keyboard and screen reader too. Clicks on links/buttons inside the row, and drags
 * that select text, are left alone.
 */
function isRowLinkClick(event) {
  if (event.target.closest('a, button, input, select, textarea, [role="button"]')) return false;
  return !window.getSelection()?.toString();
}

export function DataTable({
  columns,
  data,
  searchable = true,
  searchPlaceholder = 'Search…',
  emptyMessage = 'No results.',
  initialSorting = [],
  pageSize,
  className,
  onSearchChange,
  getRowHref,
}) {
  const navigate = useNavigate();
  const [sorting, setSorting] = useState(initialSorting);
  const [globalFilter, setGlobalFilterState] = useState('');
  // Typing in the search box (as opposed to the table resetting it) is reported to onSearchChange
  const handleSearchInput = (value) => {
    setGlobalFilterState(value);
    onSearchChange?.(value);
  };
  const paginated = pageSize > 0;

  const table = useReactTable({
    data,
    columns,
    state: { sorting, globalFilter },
    onSortingChange: setSorting,
    onGlobalFilterChange: setGlobalFilterState,
    getCoreRowModel: getCoreRowModel(),
    getSortedRowModel: getSortedRowModel(),
    getFilteredRowModel: getFilteredRowModel(),
    ...(paginated && {
      getPaginationRowModel: getPaginationRowModel(),
      initialState: { pagination: { pageIndex: 0, pageSize } },
    }),
  });

  const filteredCount = table.getFilteredRowModel().rows.length;
  const { pageIndex } = table.getState().pagination;
  const firstRow = filteredCount === 0 ? 0 : pageIndex * pageSize + 1;
  const lastRow = Math.min((pageIndex + 1) * pageSize, filteredCount);

  return (
    <div className={cn('flex flex-col gap-3', className)}>
      {searchable && (
        <SearchInput value={globalFilter} onChange={handleSearchInput} placeholder={searchPlaceholder} className="max-w-sm" />
      )}

      <Table>
        <TableHeader>
          {table.getHeaderGroups().map((headerGroup) => (
            <TableRow key={headerGroup.id} className="hover:bg-transparent">
              {headerGroup.headers.map((header) => {
                const sortable = header.column.getCanSort();
                const sortDirection = header.column.getIsSorted();

                return (
                  <TableHead key={header.id}>
                    {header.isPlaceholder ? null : sortable ? (
                      <button
                        type="button"
                        onClick={header.column.getToggleSortingHandler()}
                        className="flex items-center gap-1 select-none hover:text-foreground"
                      >
                        {flexRender(header.column.columnDef.header, header.getContext())}
                        {sortDirection === 'asc' && <ArrowUp className="size-3.5" />}
                        {sortDirection === 'desc' && <ArrowDown className="size-3.5" />}
                        {!sortDirection && <ArrowUpDown className="size-3.5 opacity-40" />}
                      </button>
                    ) : (
                      flexRender(header.column.columnDef.header, header.getContext())
                    )}
                  </TableHead>
                );
              })}
            </TableRow>
          ))}
        </TableHeader>
        <TableBody>
          {table.getRowModel().rows.length === 0 ? (
            <TableRow>
              <TableCell colSpan={columns.length} className="py-8 text-center text-muted-foreground">
                {emptyMessage}
              </TableCell>
            </TableRow>
          ) : (
            table.getRowModel().rows.map((row) => (
              <TableRow
                key={row.id}
                {...(getRowHref && {
                  className: 'cursor-pointer',
                  onClick: (e) => {
                    if (!isRowLinkClick(e)) return;
                    const href = getRowHref(row.original);
                    if (e.metaKey || e.ctrlKey) window.open(href, '_blank', 'noopener');
                    else navigate(href);
                  },
                  onAuxClick: (e) => {
                    if (e.button === 1 && isRowLinkClick(e)) window.open(getRowHref(row.original), '_blank', 'noopener');
                  },
                })}
              >
                {row.getVisibleCells().map((cell) => (
                  <TableCell key={cell.id}>{flexRender(cell.column.columnDef.cell, cell.getContext())}</TableCell>
                ))}
              </TableRow>
            ))
          )}
        </TableBody>
      </Table>

      {paginated && filteredCount > pageSize && (
        <div className="flex items-center justify-between gap-3 text-sm text-muted-foreground">
          <span className="tabular-nums">
            {firstRow}–{lastRow} of {filteredCount}
          </span>
          <div className="flex gap-2">
            <Button variant="outline" size="sm" onClick={() => table.previousPage()} disabled={!table.getCanPreviousPage()}>
              <ChevronLeft /> Previous
            </Button>
            <Button variant="outline" size="sm" onClick={() => table.nextPage()} disabled={!table.getCanNextPage()}>
              Next <ChevronRight />
            </Button>
          </div>
        </div>
      )}
    </div>
  );
}

export default DataTable;
