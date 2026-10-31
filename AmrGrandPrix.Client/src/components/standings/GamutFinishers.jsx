/**
 * Run the Gamut: every runner who completed 7+ of the year's Grand Prix events, scoring or not,
 * with a check under each event they finished.
 */

import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { AlertCircle, Check } from 'lucide-react';
import { TableHeader, TableBody, TableRow, TableHead, TableCell } from '@/components/ui/table';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Skeleton } from '@/components/ui/skeleton';
import { cn } from '@/lib/utils';
import { STICKY_HEAD } from './constants';

// Runner stays pinned while the event columns scroll sideways
const PIN_RUNNER = 'sticky left-0 min-w-40 bg-background shadow-[1px_0_0_var(--color-border)]';

const GENDER_LABELS = { Male: 'M', Female: 'F', Nonbinary: 'NB' };

function GamutTable({ columns, runners }) {
  return (
    <div className="overflow-hidden rounded-xl border border-border">
      <div className="max-h-[70vh] overflow-auto">
        <table className="w-full caption-bottom text-sm">
          <TableHeader>
            <TableRow className="hover:bg-transparent">
              <TableHead className={cn(STICKY_HEAD, 'left-0 z-20 min-w-40')}>Runner</TableHead>
              <TableHead className={cn(STICKY_HEAD, 'text-center')}>Gender</TableHead>
              <TableHead className={cn(STICKY_HEAD, 'text-right')}>Events</TableHead>
              {columns.map((c) => (
                <TableHead
                  key={c.raceSeriesId}
                  title={c.name}
                  className={cn(STICKY_HEAD, 'h-auto min-w-20 max-w-24 py-2 text-center leading-tight whitespace-normal')}
                >
                  {c.name}
                </TableHead>
              ))}
            </TableRow>
          </TableHeader>
          <TableBody>
            {runners.map((r) => {
              const completed = new Set(r.completedSeriesIds);
              return (
                <TableRow key={r.runnerId} className="hover:bg-transparent">
                  <TableCell className={cn(PIN_RUNNER, 'font-medium whitespace-nowrap text-foreground')}>
                    <Link to={`/runners/${r.runnerId}`} className="hover:text-primary">{r.runnerName}</Link>
                  </TableCell>
                  <TableCell className="text-center text-muted-foreground">{GENDER_LABELS[r.gender] ?? '—'}</TableCell>
                  <TableCell className="text-right font-semibold tabular-nums text-primary">
                    {r.eventsCompleted}/{columns.length}
                  </TableCell>
                  {columns.map((c) => (
                    <TableCell key={c.raceSeriesId} className="text-center">
                      {completed.has(c.raceSeriesId)
                        ? <Check className="inline size-4 text-emerald-600 dark:text-emerald-400" aria-label="Completed" />
                        : <span className="text-muted-foreground/40">·</span>}
                    </TableCell>
                  ))}
                </TableRow>
              );
            })}
          </TableBody>
        </table>
      </div>
    </div>
  );
}

export default function GamutFinishers({ year, searchTerm }) {
  const [data, setData] = useState(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);
    fetch(`/api/standings/${year}/gamut`)
      .then((res) => {
        if (!res.ok) throw new Error(`HTTP ${res.status}`);
        return res.json();
      })
      .then((json) => !cancelled && setData(json))
      .catch((e) => {
        if (cancelled) return;
        setError(e.message);
        setData(null);
      })
      .finally(() => !cancelled && setLoading(false));
    return () => {
      cancelled = true;
    };
  }, [year]);

  if (loading) {
    return (
      <div className="space-y-2">
        <Skeleton className="h-9 w-full" />
        <Skeleton className="h-64 w-full" />
      </div>
    );
  }
  if (error) {
    return (
      <Alert variant="destructive">
        <AlertCircle />
        <AlertDescription>Error loading Run the Gamut: {error}</AlertDescription>
      </Alert>
    );
  }
  if (!data) return null;

  const term = searchTerm.trim().toLowerCase();
  const runners = data.runners.filter((r) => r.runnerName?.toLowerCase().includes(term));

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-center gap-2">
        {!data.isFinalized && (
          <Badge
            variant="outline"
            className="border-transparent bg-amber-500/10 font-medium text-amber-700 dark:bg-amber-500/15 dark:text-amber-400"
            title={`The ${year} Grand Prix is still in progress, so this list may change.`}
          >
            Tentative
          </Badge>
        )}
        <p className="text-xs text-muted-foreground">
          Runners who completed at least {data.minEvents} of the {data.columns.length} Grand Prix events, whether or
          not they scored. Finishing the Knoya Original (Dome) or Government Peak Uphill Only course also counts.
        </p>
      </div>

      {runners.length === 0 ? (
        <div className="rounded-xl border border-border bg-card px-6 py-16 text-center text-sm text-muted-foreground">
          {term ? 'No runners match your search.' : `No one has run the Gamut in ${year} yet.`}
        </div>
      ) : (
        <GamutTable columns={data.columns} runners={runners} />
      )}
    </div>
  );
}
