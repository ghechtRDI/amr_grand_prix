import { useMemo } from 'react';
import { computeRunnerStats, ordinal } from '@/lib/runnerStats';

function Stat({ label, value, detail }) {
  return (
    <div className="rounded-xl border border-border bg-card px-4 py-3">
      <div className="text-xs font-medium tracking-wide text-muted-foreground uppercase">{label}</div>
      <div className="mt-1 text-2xl font-semibold tabular-nums">{value}</div>
      {detail && <div className="mt-0.5 truncate text-xs text-muted-foreground" title={detail}>{detail}</div>}
    </div>
  );
}

function raceDetail(r) {
  return `${r.raceSeriesName ?? r.raceName} ${r.year}`;
}

/** Headline numbers across every result: volume, longevity, and best finishes. */
export function RunnerStats({ results }) {
  const stats = useMemo(() => computeRunnerStats(results), [results]);
  if (results.length === 0) return null;

  const { bestOverall, bestAgeGroup, streak } = stats;

  return (
    <div className="grid grid-cols-2 gap-3 md:grid-cols-5">
      <Stat
        label="Races"
        value={stats.races}
        detail={stats.finishes !== stats.races ? `${stats.finishes} finishes` : null}
      />
      <Stat
        label="Years Active"
        value={stats.yearsActive}
        detail={stats.firstYear === stats.lastYear ? `${stats.firstYear}` : `${stats.firstYear}–${stats.lastYear}`}
      />
      <Stat
        label="Longest Streak"
        value={streak && streak.years > 1 ? `${streak.years} yrs` : '—'}
        detail={streak && streak.years > 1 ? `${streak.name} through ${streak.endYear}` : 'Consecutive years at one race'}
      />
      <Stat
        label="Best Overall"
        value={bestOverall ? ordinal(bestOverall.overallPlace) : '—'}
        detail={bestOverall && raceDetail(bestOverall)}
      />
      <Stat
        label="Best Age Group"
        value={bestAgeGroup ? ordinal(bestAgeGroup.ageGroupPlace) : '—'}
        detail={
          stats.ageGroupWins > 1
            ? `${stats.ageGroupWins} age-group wins`
            : bestAgeGroup && raceDetail(bestAgeGroup)
        }
      />
    </div>
  );
}
