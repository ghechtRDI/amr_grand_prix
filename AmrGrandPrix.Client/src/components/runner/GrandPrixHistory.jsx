import { useMemo } from 'react';
import { Link } from 'react-router-dom';
import { Trophy } from 'lucide-react';
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from '@/components/ui/table';
import { Badge } from '@/components/ui/badge';
import { ordinal } from '@/lib/runnerStats';
import { cn } from '@/lib/utils';

function RankCell({ standing }) {
  if (!standing) return <TableCell className="text-muted-foreground">—</TableCell>;
  return (
    <TableCell className="tabular-nums">
      <span className="font-semibold text-foreground">{ordinal(standing.rank)}</span>
      <span className="text-muted-foreground"> of {standing.divisionSize}</span>
    </TableCell>
  );
}

/**
 * Season-by-season Grand Prix results: Open and Age division rank/points side by side per year.
 * Seasons that haven't been finalized yet (the Grand Prix is still in progress) are marked
 * tentative, since their ranks and points can still change.
 */
export function GrandPrixHistory({ standings }) {
  const seasons = useMemo(() => {
    const byYear = new Map();
    for (const s of standings) {
      if (!byYear.has(s.year)) byYear.set(s.year, { year: s.year, open: null, age: null });
      byYear.get(s.year)[s.ageCategory ? 'age' : 'open'] = s;
    }
    return [...byYear.values()].sort((a, b) => b.year - a.year);
  }, [standings]);

  if (seasons.length === 0) return null;

  const hasTentative = seasons.some(({ open, age }) => !(open ?? age).isFinalized);

  return (
    <div className="flex flex-col gap-2">
      <div className="overflow-x-auto rounded-xl border border-border">
        <Table>
          <TableHeader>
            <TableRow className="hover:bg-transparent">
              <TableHead>Season</TableHead>
              <TableHead>Open Rank</TableHead>
              <TableHead className="text-right">Open Pts</TableHead>
              <TableHead>Age Group</TableHead>
              <TableHead>Age Rank</TableHead>
              <TableHead className="text-right">Age Pts</TableHead>
              <TableHead className="text-right">GP Races</TableHead>
              <TableHead />
            </TableRow>
          </TableHeader>
          <TableBody>
            {seasons.map(({ year, open, age }) => {
              const any = open ?? age;
              const tentative = !any.isFinalized;
              return (
                <TableRow key={year} className={cn(tentative && 'text-muted-foreground')}>
                  <TableCell>
                    <div className="flex items-center gap-2 whitespace-nowrap">
                      <Link to={`/standings/${year}`} className="font-medium text-foreground hover:text-primary">{year}</Link>
                      {tentative && (
                        <Badge
                          variant="outline"
                          className="border-transparent bg-amber-500/10 font-medium text-amber-700 dark:bg-amber-500/15 dark:text-amber-400"
                          title={`The ${year} Grand Prix is still in progress, so these standings may change.`}
                        >
                          Tentative
                        </Badge>
                      )}
                    </div>
                  </TableCell>
                  <RankCell standing={open} />
                  <TableCell className="text-right tabular-nums">{open?.totalPoints ?? '—'}</TableCell>
                  <TableCell className="whitespace-nowrap">{age?.ageCategory ?? '—'}</TableCell>
                  <RankCell standing={age} />
                  <TableCell className="text-right tabular-nums">{age?.totalPoints ?? '—'}</TableCell>
                  <TableCell className="text-right tabular-nums">{any.racesCompleted}</TableCell>
                  <TableCell>
                    {any.runTheGamutQualified && (
                      <Badge variant="success" className="gap-1 whitespace-nowrap">
                        <Trophy className="size-3" /> Run the Gamut
                      </Badge>
                    )}
                  </TableCell>
                </TableRow>
              );
            })}
          </TableBody>
        </Table>
      </div>
      {hasTentative && (
        <p className="text-xs text-muted-foreground">
          Tentative seasons are still in progress; ranks and points may change until the Grand Prix is finalized.
        </p>
      )}
    </div>
  );
}
