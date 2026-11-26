/**
 * Athletes Page (public)
 * Every runner in the system with age, result count, and best Grand Prix season, sortable and
 * searchable. The directory endpoint carries no personal data (no DOB, email, or alternate
 * names). Admins and managers get a pencil button per row that opens the racer editor.
 */

import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { createColumnHelper } from '@tanstack/react-table';
import { trackEvent } from '@/lib/analytics';
import { AlertCircle, Loader2, Pencil, X } from 'lucide-react';
import * as runnerService from '../services/runnerService';
import { useEditRacer } from '../hooks/useEditRacer';
import { DataTable } from '@/components/ui/data-table';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';

const columnHelper = createColumnHelper();

const Dash = () => <span className="text-muted-foreground">—</span>;

// Null ages/scores map to undefined so `sortUndefined: 'last'` keeps them at the bottom in both
// sort directions. Only the name takes part in the search box.
const BASE_COLUMNS = [
  columnHelper.accessor('fullName', {
    header: 'Name',
    cell: ({ row, getValue }) => (
      <Link to={`/runners/${row.original.runnerId}`} className="font-medium text-foreground hover:text-primary">
        {getValue()}
      </Link>
    ),
  }),
  columnHelper.accessor('gender', {
    header: 'Gender',
    enableGlobalFilter: false,
  }),
  columnHelper.accessor((a) => a.currentAge ?? undefined, {
    id: 'age',
    header: 'Age',
    sortUndefined: 'last',
    enableGlobalFilter: false,
    cell: ({ row }) =>
      row.original.currentAge == null ? (
        <Dash />
      ) : (
        <span
          className="tabular-nums"
          title={row.original.isAgeEstimated ? 'Estimated from ages reported in race results' : undefined}
        >
          {row.original.currentAge}
          {row.original.isAgeEstimated && <span className="ml-1 text-xs text-muted-foreground">(est.)</span>}
        </span>
      ),
  }),
  columnHelper.accessor('resultCount', {
    header: 'Results',
    enableGlobalFilter: false,
    cell: ({ getValue }) => <span className="tabular-nums">{getValue()}</span>,
  }),
  columnHelper.accessor((a) => a.topGrandPrixScore ?? undefined, {
    id: 'topScore',
    header: 'Top GP Score',
    sortUndefined: 'last',
    sortDescFirst: true,
    enableGlobalFilter: false,
    cell: ({ row }) =>
      row.original.topGrandPrixScore == null ? (
        <Dash />
      ) : (
        <span className="tabular-nums">
          {row.original.topGrandPrixScore}
          <span className="ml-1.5 text-xs text-muted-foreground">({row.original.topGrandPrixYear})</span>
        </span>
      ),
  }),
];

const INITIAL_SORTING = [{ id: 'topScore', desc: true }];

export default function Athletes() {
  const [athletes, setAthletes] = useState(null);
  const [loadError, setLoadError] = useState(null);

  const load = useCallback(() => {
    runnerService
      .getAthleteDirectory()
      .then((data) => {
        setAthletes(data);
        setLoadError(null);
      })
      .catch((err) => setLoadError(err.message));
  }, []);

  useEffect(load, [load]);

  // Report that the search was used, once per visit - never what was typed, so no names reach GA
  const searchTracked = useRef(false);
  const handleSearchChange = useCallback((value) => {
    if (searchTracked.current || !value.trim()) return;
    searchTracked.current = true;
    trackEvent('athlete_search');
  }, []);

  const { canEdit, openEditor, loadingId, error: editError, clearError, dialog } = useEditRacer({ onSaved: load });

  const columns = useMemo(
    () => [
      ...BASE_COLUMNS,
      columnHelper.display({
        id: 'actions',
        header: () => <span className="sr-only">Actions</span>,
        cell: ({ row }) => {
          const { runnerId, fullName } = row.original;
          return (
            <div className="flex justify-end gap-1.5">
              <Button variant="outline" size="sm" nativeButton={false} render={<Link to={`/runners/${runnerId}`}>View racer</Link>} />
              {canEdit && (
                <Button
                  variant="ghost"
                  size="icon-sm"
                  onClick={() => openEditor(runnerId)}
                  disabled={loadingId === runnerId}
                  aria-label={`Edit ${fullName}`}
                  title="Edit racer"
                >
                  {loadingId === runnerId ? <Loader2 className="animate-spin" /> : <Pencil />}
                </Button>
              )}
            </div>
          );
        },
      }),
    ],
    [canEdit, openEditor, loadingId],
  );

  return (
    <div className="min-h-svh bg-background px-4 py-8 md:py-12">
      <div className="mx-auto max-w-5xl">
        <div className="mb-6">
          <h1 className="bg-gradient-to-r from-primary to-secondary bg-clip-text text-2xl font-semibold tracking-tight text-transparent md:text-3xl">
            Athletes
          </h1>
          <p className="mt-1 text-muted-foreground">
            Everyone with results in the system. Top GP score is a runner&apos;s best Open-division Grand Prix season.
          </p>
        </div>

        {editError && (
          <Alert variant="destructive" className="mb-4">
            <AlertCircle />
            <AlertDescription className="flex items-center justify-between gap-2">
              Couldn&apos;t open the racer record: {editError}
              <Button variant="ghost" size="icon-sm" onClick={clearError} aria-label="Dismiss">
                <X />
              </Button>
            </AlertDescription>
          </Alert>
        )}

        {loadError ? (
          <Alert variant="destructive">
            <AlertCircle />
            <AlertDescription>Failed to load athletes: {loadError}</AlertDescription>
          </Alert>
        ) : athletes === null ? (
          <div className="space-y-3">
            <Skeleton className="h-9 w-full max-w-sm" />
            <Skeleton className="h-96 w-full" />
          </div>
        ) : (
          <DataTable
            columns={columns}
            data={athletes}
            initialSorting={INITIAL_SORTING}
            pageSize={50}
            searchPlaceholder="Search athletes…"
            onSearchChange={handleSearchChange}
            emptyMessage="No athletes found."
          />
        )}

        <div className="mt-6 flex gap-6 border-t border-border pt-4 text-sm">
          <Link to="/" className="text-muted-foreground hover:text-foreground">← Home</Link>
          <Link to="/report" className="text-muted-foreground hover:text-foreground">Report an issue</Link>
        </div>
      </div>

      {dialog}
    </div>
  );
}
