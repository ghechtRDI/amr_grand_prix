/**
 * Runner Profile Page (public)
 * A runner's basic info, headline stats, a card per race series they've raced, their Grand Prix
 * season history, and every result newest-first.
 */

import { Link, useParams } from 'react-router-dom';
import { ChevronRight } from 'lucide-react';
import { useRunnerProfile } from '../hooks/useRunnerProfile';
import { RunnerHeader, RunnerPageShell } from '@/components/runner/RunnerHeader';
import { RunnerResultsTable } from '@/components/runner/RunnerResultsTable';
import { RunnerStats } from '@/components/runner/RunnerStats';
import { GrandPrixHistory } from '@/components/runner/GrandPrixHistory';
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card';
import { formatTime } from '@/lib/time';

function yearSpan(first, last) {
  return first === last ? `${first}` : `${first}–${last}`;
}

function SeriesCard({ runnerId, series }) {
  return (
    <Link to={`/runners/${runnerId}/series/${series.raceSeriesId}`} className="group block">
      <Card className="h-full transition-colors group-hover:border-primary/60">
        <CardHeader>
          <CardTitle className="flex items-center justify-between gap-2">
            {series.name}
            <ChevronRight className="size-4 shrink-0 text-muted-foreground transition-transform group-hover:translate-x-0.5" />
          </CardTitle>
          <CardDescription>
            {series.resultCount} {series.resultCount === 1 ? 'race' : 'races'} · {yearSpan(series.firstYear, series.lastYear)}
          </CardDescription>
        </CardHeader>
        <CardContent>
          <dl className="flex flex-col gap-1 text-sm">
            {series.variants.slice(0, 3).map((v) => (
              <div key={v.raceVariantId} className="flex justify-between gap-3">
                <dt className="truncate text-muted-foreground">{v.courseVariant || 'Best'}</dt>
                <dd className="font-mono tabular-nums">{v.bestTime ? formatTime(v.bestTime) : '—'}</dd>
              </div>
            ))}
            {series.variants.length > 3 && (
              <div className="text-xs text-muted-foreground">+{series.variants.length - 3} more variants</div>
            )}
          </dl>
        </CardContent>
      </Card>
    </Link>
  );
}

export default function RunnerProfile() {
  const { runnerId } = useParams();
  const { profile, loading, error } = useRunnerProfile(runnerId);

  return (
    <RunnerPageShell loading={loading} error={error}>
      {profile && (
        <>
          <div className="mb-2 text-sm text-muted-foreground">
            <Link to="/" className="hover:text-foreground">Home</Link>
            <span className="mx-1.5">/</span>
            <span>Runners</span>
          </div>

          <RunnerHeader profile={profile} />

          <div className="mt-6">
            <RunnerStats results={profile.results} />
          </div>

          {profile.series.length > 0 && (
            <section className="mt-8">
              <h2 className="mb-3 text-lg font-semibold">Race Series</h2>
              <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
                {profile.series.map((s) => (
                  <SeriesCard key={s.raceSeriesId} runnerId={runnerId} series={s} />
                ))}
              </div>
            </section>
          )}

          {profile.grandPrixHistory.length > 0 && (
            <section className="mt-8">
              <h2 className="mb-3 text-lg font-semibold">Grand Prix History</h2>
              <GrandPrixHistory standings={profile.grandPrixHistory} />
            </section>
          )}

          <section className="mt-8">
            <h2 className="mb-3 text-lg font-semibold">All Results</h2>
            <RunnerResultsTable results={profile.results} />
          </section>

          <div className="mt-6 flex flex-wrap gap-6 border-t border-border pt-4 text-sm">
            <Link to="/race-results" className="text-muted-foreground hover:text-foreground">← Race Results</Link>
            <Link to="/report" className="text-muted-foreground hover:text-foreground">Report an issue with these results</Link>
          </div>
        </>
      )}
    </RunnerPageShell>
  );
}
