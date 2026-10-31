/**
 * Points Breakdown: every runner's points in every Grand Prix race of the year for one gender —
 * the Overall (Open) division followed by each age group, like the published GP results sheet.
 */

import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { AlertCircle, Check } from 'lucide-react';
import { TableHeader, TableBody, TableRow, TableHead, TableCell } from '@/components/ui/table';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Skeleton } from '@/components/ui/skeleton';
import { cn } from '@/lib/utils';
import { RankBadge } from './shared';
import { STICKY_HEAD } from './constants';

const sectionId = (ageCategory) =>
  `breakdown-${ageCategory ? ageCategory.toLowerCase().replace(/[^a-z0-9]+/g, '-') : 'overall'}`;

// Rank and Runner stay pinned while the race columns scroll sideways
const PIN_RANK = 'sticky left-0 w-12 min-w-12 bg-background';
const PIN_RUNNER = 'sticky left-12 min-w-40 bg-background shadow-[1px_0_0_var(--color-border)]';

function PointsCell({ cell }) {
  if (!cell) return <TableCell className="text-center text-muted-foreground/40">·</TableCell>;

  return (
    <TableCell
      className={cn(
        'text-center tabular-nums',
        !cell.counted && 'text-muted-foreground/70 line-through decoration-muted-foreground/40'
      )}
      title={[
        !cell.counted && 'Not counted (best 4 races only).',
        cell.isRecordBonus && 'Includes +10 course-record bonus.',
      ].filter(Boolean).join(' ') || undefined}
    >
      {cell.points}
      {cell.isRecordBonus && <sup className="ml-0.5 font-semibold text-amber-600 dark:text-amber-400">R</sup>}
    </TableCell>
  );
}

function BreakdownTable({ columns, rows }) {
  return (
    <div className="overflow-hidden rounded-xl border border-border">
      <div className="max-h-[70vh] overflow-auto">
        <table className="w-full caption-bottom text-sm">
          <TableHeader>
            <TableRow className="hover:bg-transparent">
              <TableHead className={cn(STICKY_HEAD, 'left-0 z-20 w-12 min-w-12')}>Rank</TableHead>
              <TableHead className={cn(STICKY_HEAD, 'left-12 z-20 min-w-40')}>Runner</TableHead>
              <TableHead className={cn(STICKY_HEAD, 'text-right')}>Total</TableHead>
              {columns.map((c) => (
                <TableHead
                  key={c.raceSeriesId}
                  title={c.name}
                  className={cn(STICKY_HEAD, 'h-auto min-w-20 max-w-24 py-2 text-center leading-tight whitespace-normal')}
                >
                  {c.name}
                </TableHead>
              ))}
              <TableHead className={cn(STICKY_HEAD, 'text-right')}>Races</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {rows.map((r) => (
              <TableRow key={r.runnerId} className="hover:bg-transparent">
                <TableCell className={PIN_RANK}><RankBadge rank={r.rank} /></TableCell>
                <TableCell className={cn(PIN_RUNNER, 'font-medium text-foreground')}>
                  <div className="flex items-center gap-1.5 whitespace-nowrap">
                    <Link to={`/runners/${r.runnerId}`} className="hover:text-primary">{r.runnerName}</Link>
                    {r.runTheGamutQualified && (
                      <Badge variant="success" className="gap-1 px-1" title="Run the Gamut (7+ events)">
                        <Check className="size-3" />
                      </Badge>
                    )}
                  </div>
                </TableCell>
                <TableCell className="text-right font-semibold tabular-nums text-primary">{r.totalPoints}</TableCell>
                {columns.map((c) => (
                  <PointsCell key={c.raceSeriesId} cell={r.points[c.raceSeriesId]} />
                ))}
                <TableCell className="text-right tabular-nums text-muted-foreground">{r.racesCompleted}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </table>
      </div>
    </div>
  );
}

export default function PointsBreakdown({ year, gender, searchTerm }) {
  const [data, setData] = useState(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);
    fetch(`/api/standings/${year}/breakdown/${gender}`)
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
  }, [year, gender]);

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
        <AlertDescription>Error loading points breakdown: {error}</AlertDescription>
      </Alert>
    );
  }
  if (!data) return null;

  const term = searchTerm.trim().toLowerCase();
  const genderWord = gender === 'male' ? 'Men' : 'Women';
  const sections = [
    { ...data.overall, title: `Overall ${genderWord}` },
    ...data.ageGroups.map((g) => ({ ...g, title: `${gender === 'male' ? 'Male' : 'Female'} ${g.ageCategory}` })),
  ]
    .map((s) => ({ ...s, rows: s.rows.filter((r) => r.runnerName?.toLowerCase().includes(term)) }))
    .filter((s) => s.rows.length > 0);

  if (sections.length === 0) {
    return (
      <div className="rounded-xl border border-border bg-card px-6 py-16 text-center text-sm text-muted-foreground">
        {term ? 'No runners match your search.' : 'No Grand Prix points for this year yet.'}
      </div>
    );
  }

  return (
    <div className="space-y-8">
      <div className="space-y-3">
        <div className="flex flex-wrap items-center gap-2">
          {!data.isFinalized && (
            <Badge
              variant="outline"
              className="border-transparent bg-amber-500/10 font-medium text-amber-700 dark:bg-amber-500/15 dark:text-amber-400"
              title={`The ${year} Grand Prix is still in progress, so these standings may change.`}
            >
              Tentative
            </Badge>
          )}
          {sections.map((s) => (
            <a
              key={sectionId(s.ageCategory)}
              href={`#${sectionId(s.ageCategory)}`}
              onClick={(e) => {
                e.preventDefault();
                document.getElementById(sectionId(s.ageCategory))?.scrollIntoView({ behavior: 'smooth', block: 'start' });
              }}
              className="rounded-full border border-border px-3 py-1 text-xs font-medium text-muted-foreground hover:bg-muted hover:text-foreground"
            >
              {s.ageCategory ?? 'Overall'}
            </a>
          ))}
        </div>
        <p className="text-xs text-muted-foreground">
          Best 4 races count toward the total;{' '}
          <span className="line-through decoration-muted-foreground/60">struck-through</span> points don't count.{' '}
          <sup className="font-semibold text-amber-600 dark:text-amber-400">R</sup> = includes +10 course-record bonus.{' '}
          <Check className="inline size-3.5" /> = Run the Gamut (7+ events).
        </p>
      </div>

      {sections.map((s) => (
        <section key={sectionId(s.ageCategory)} id={sectionId(s.ageCategory)} className="scroll-mt-20">
          <h3 className="mb-3 flex items-baseline gap-2 text-base font-semibold">
            {s.title}
            <span className="text-sm font-normal text-muted-foreground">{s.rows.length} runners</span>
          </h3>
          <BreakdownTable columns={data.columns} rows={s.rows} />
        </section>
      ))}
    </div>
  );
}
