/**
 * Runner Series Page (public)
 * One runner's results in one race series, split into a section per course variant, each
 * toggling between a results table and a finish-time-by-year chart.
 */

import { useMemo, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { BarChart3, Table2 } from 'lucide-react';
import { useRunnerProfile } from '../hooks/useRunnerProfile';
import { RunnerHeader, RunnerPageShell } from '@/components/runner/RunnerHeader';
import { RunnerResultsTable } from '@/components/runner/RunnerResultsTable';
import { RunnerTimeChart } from '@/components/runner/RunnerTimeChart';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card';
import { cn } from '@/lib/utils';
import { formatTime } from '@/lib/time';

const VIEWS = [
  { value: 'chart', label: 'Graph', icon: <BarChart3 className="size-4" /> },
  { value: 'table', label: 'Table', icon: <Table2 className="size-4" /> },
];

function VariantSection({ title, results }) {
  const finishes = results.filter((r) => r.overallPlace);
  const pr = results.find((r) => r.isPersonalRecord) ?? (finishes.length === 1 ? finishes[0] : null);
  // A graph of one point isn't a trajectory - start on the table until there's something to plot.
  const [view, setView] = useState(finishes.length > 1 ? 'chart' : 'table');

  return (
    <Card>
      <CardHeader className="flex flex-row flex-wrap items-start justify-between gap-3">
        <div>
          <CardTitle>{title}</CardTitle>
          <CardDescription>
            {finishes.length} {finishes.length === 1 ? 'finish' : 'finishes'}
            {results.length !== finishes.length && ` of ${results.length} starts`}
            {pr && <> · Best {formatTime(pr.time)} ({pr.year})</>}
          </CardDescription>
        </div>
        <div className="inline-flex overflow-hidden rounded-lg border border-border">
          {VIEWS.map(({ value, label, icon }, i) => (
            <Button
              key={value}
              type="button"
              size="sm"
              variant={view === value ? 'default' : 'ghost'}
              className={cn('rounded-none', i > 0 && 'border-l border-border')}
              aria-pressed={view === value}
              onClick={() => setView(value)}
            >
              {icon} {label}
            </Button>
          ))}
        </div>
      </CardHeader>
      <CardContent>
        {view === 'chart' ? (
          <RunnerTimeChart results={results} />
        ) : (
          <RunnerResultsTable results={results} showRace={false} />
        )}
      </CardContent>
    </Card>
  );
}

export default function RunnerSeries() {
  const { runnerId, seriesId } = useParams();
  const { profile, loading, error } = useRunnerProfile(runnerId);

  const series = profile?.series.find((s) => s.raceSeriesId === seriesId);

  // Sections follow the series summary's variant order (most-raced first).
  const sections = useMemo(() => {
    if (!profile || !series) return [];
    const seriesResults = profile.results.filter((r) => r.raceSeriesId === seriesId);
    return series.variants.map((v) => ({
      key: v.raceVariantId,
      title: v.courseVariant || series.name,
      results: seriesResults.filter((r) => r.raceVariantId === v.raceVariantId),
    }));
  }, [profile, series, seriesId]);

  return (
    <RunnerPageShell loading={loading} error={error ?? (profile && !series ? 'No results in this series for this runner.' : null)}>
      {profile && series && (
        <>
          <div className="mb-2 text-sm text-muted-foreground">
            <Link to={`/runners/${runnerId}`} className="hover:text-foreground">{profile.fullName}</Link>
            <span className="mx-1.5">/</span>
            <span>{series.name}</span>
          </div>

          <RunnerHeader profile={profile}>
            <Link to={`/race-series/${seriesId}`} className="font-medium text-primary hover:underline">
              {series.name}
            </Link>
          </RunnerHeader>

          <div className="mt-8 flex flex-col gap-6">
            {sections.map((s) => (
              <VariantSection key={s.key} title={s.title} results={s.results} />
            ))}
          </div>

          <div className="mt-6 flex flex-wrap gap-6 border-t border-border pt-4 text-sm">
            <Link to={`/runners/${runnerId}`} className="text-muted-foreground hover:text-foreground">
              ← All of {profile.firstName}&rsquo;s results
            </Link>
            <Link to={`/race-series/${seriesId}?tab=stats`} className="text-muted-foreground hover:text-foreground">
              {series.name} all-time statistics →
            </Link>
          </div>
        </>
      )}
    </RunnerPageShell>
  );
}
