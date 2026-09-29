import { useState } from 'react';
import {
  useReactTable,
  getCoreRowModel,
  getSortedRowModel,
  getFilteredRowModel,
  flexRender,
} from '@tanstack/react-table';
import { ArrowDown, ArrowUp, ArrowUpDown } from 'lucide-react';
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from '@/components/ui/table';
import { SearchInput } from '@/components/ui/search-input';
import { cn } from '@/lib/utils';

/**
 * A sortable, globally-searchable table built on the @tanstack/react-table primitives already
 * used elsewhere in the app (see components/upload/DataReviewStep.jsx). Pass `columns` built with
 * `createColumnHelper()` and `data`; sorting and the search box are wired up here so callers don't
 * each hand-roll their own sort state (see the pre-existing pattern in pages/RaceResults.jsx).
 */
export function DataTable({
  columns,
  data,
  searchable = true,
  searchPlaceholder = 'Search…',
  emptyMessage = 'No results.',
  className,
}) {
  const [sorting, setSorting] = useState([]);
  const [globalFilter, setGlobalFilter] = useState('');

  const table = useReactTable({
    data,
    columns,
    state: { sorting, globalFilter },
    onSortingChange: setSorting,
    onGlobalFilterChange: setGlobalFilter,
    getCoreRowModel: getCoreRowModel(),
    getSortedRowModel: getSortedRowModel(),
    getFilteredRowModel: getFilteredRowModel(),
  });

  return (
    <div className={cn('flex flex-col gap-3', className)}>
      {searchable && (
        <SearchInput value={globalFilter} onChange={setGlobalFilter} placeholder={searchPlaceholder} className="max-w-sm" />
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
              <TableRow key={row.id}>
                {row.getVisibleCells().map((cell) => (
                  <TableCell key={cell.id}>{flexRender(cell.column.columnDef.cell, cell.getContext())}</TableCell>
                ))}
              </TableRow>
            ))
          )}
        </TableBody>
      </Table>
    </div>
  );
}

export default DataTable;
