/**
 * Landing Page (public, "/")
 * Introduces the site as the Alaska Mountain Runners results archive, then features the Grand Prix:
 * the basic rules and a leader snapshot for the latest season with standings.
 */

import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { ArrowRight, Award, Flag, ListChecks, Mountain, MountainSnow, Trophy, Users } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Skeleton } from '@/components/ui/skeleton';
import { RankBadge } from '@/components/standings/shared';
import MountainRidge from '@/components/landing/MountainRidge';
import { useAuth } from '../hooks/useAuth';
import { trackEvent } from '@/lib/analytics';

const SNAPSHOT_SIZE = 5;
const SNAPSHOT_GENDERS = ['female', 'male'];

const RULES = [
  {
    icon: Trophy,
    title: 'Open Division',
    body: 'The top 20 women and men in each race score points: 100 for a win, then 90, 85, 80… down to 1. Set a new course record for 10 bonus points.',
  },
  {
    icon: Award,
    title: 'Age Divisions',
    body: 'The top 5 in each age group score 5, 4, 3, 2 and 1 points, from 17 and Under up through 80-89.',
  },
  {
    icon: MountainSnow,
    title: 'Best 4 Races Count',
    body: 'Run as many Grand Prix races as you like; your four best scores make your season total. Ties go to the best single race.',
  },
  {
    icon: Flag,
    title: 'Run the Gamut',
    body: 'Finish 7 or more of the season’s Grand Prix events, scoring or not, to earn Run the Gamut honors.',
  },
];

const numberFormat = new Intl.NumberFormat('en-US');

async function fetchJson(url) {
  const res = await fetch(url);
  if (!res.ok) throw new Error(`HTTP ${res.status}`);
  return res.json();
}

/** Site totals line, e.g. "142 races · 15 years · 3,800 athletes". Hidden until loaded or on error. */
function SiteTotals() {
  const [summary, setSummary] = useState(null);

  useEffect(() => {
    let cancelled = false;
    fetchJson('/api/stats/summary')
      .then((data) => !cancelled && setSummary(data))
      .catch(() => {}); // decorative: hide rather than show zeros
    return () => {
      cancelled = true;
    };
  }, []);

  if (!summary || summary.raceCount === 0) return <p className="h-5" aria-hidden="true" />;

  const parts = [
    `${numberFormat.format(summary.raceCount)} races`,
    `${numberFormat.format(summary.yearCount)} years`,
    `${numberFormat.format(summary.athleteCount)} athletes`,
  ];
  return <p className="h-5 text-sm font-medium tracking-wide text-muted-foreground">{parts.join(' · ')}</p>;
}

/** Top-N Open Division leaders per gender for the latest year that has standings. */
function useStandingsSnapshot() {
  const [state, setState] = useState({ loading: true, year: null, isFinalized: false, leaders: {} });

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const years = await fetchJson('/api/standings/years');
        if (years.length === 0) {
          if (!cancelled) setState({ loading: false, year: null, isFinalized: false, leaders: {} });
          return;
        }
        const year = years[0];
        const [season, ...boards] = await Promise.all([
          fetchJson(`/api/standings/${year}/season`),
          ...SNAPSHOT_GENDERS.map((g) => fetchJson(`/api/standings/${year}/open/${g}?top=${SNAPSHOT_SIZE}`)),
        ]);
        const leaders = Object.fromEntries(SNAPSHOT_GENDERS.map((g, i) => [g, boards[i].standings || []]));
        if (!cancelled) setState({ loading: false, year, isFinalized: season.isFinalized, leaders });
      } catch {
        if (!cancelled) setState({ loading: false, year: null, isFinalized: false, leaders: {}, error: true });
      }
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  return state;
}

function LeaderList({ gender, standings, loading }) {
  return (
    <div className="rounded-xl bg-card p-4 ring-1 ring-foreground/10">
      <h4 className="mb-3 text-sm font-semibold tracking-wider text-muted-foreground uppercase">
        {gender === 'female' ? 'Women' : 'Men'}
      </h4>
      {loading ? (
        <div className="flex flex-col gap-2">
          {Array.from({ length: SNAPSHOT_SIZE }, (_, i) => (
            <Skeleton key={i} className="h-8 w-full" />
          ))}
        </div>
      ) : standings.length === 0 ? (
        <p className="py-6 text-center text-sm text-muted-foreground">No standings yet.</p>
      ) : (
        <ol className="flex flex-col gap-1">
          {standings.map((s) => (
            <li key={s.standingId} className="flex items-center gap-3 rounded-lg px-1 py-1">
              <RankBadge rank={s.rank} />
              <Link to={`/runners/${s.runnerId}`} className="min-w-0 flex-1 truncate font-medium hover:text-primary">
                {s.runnerName}
              </Link>
              <span className="font-semibold tabular-nums text-primary">{s.totalPoints}</span>
            </li>
          ))}
        </ol>
      )}
    </div>
  );
}

function GrandPrixSection() {
  const { loading, year, isFinalized, leaders, error } = useStandingsSnapshot();
  const showSnapshot = loading || (year && !error);

  return (
    <section aria-labelledby="gp-heading" className="px-4 pb-12 md:pb-16">
      <div className="mx-auto max-w-5xl overflow-hidden rounded-2xl bg-card ring-1 ring-foreground/10 shadow-sm">
        <div className="h-1.5 bg-gradient-to-r from-primary to-secondary" />
        <div className="p-6 md:p-10">
          <div className="mb-8 max-w-3xl">
            <p className="mb-2 flex items-center gap-2 text-sm font-semibold tracking-wider text-primary uppercase">
              <Mountain className="size-4" aria-hidden="true" />
              Season-long series
            </p>
            <h2 id="gp-heading" className="text-2xl font-semibold tracking-tight md:text-3xl">
              The AMR Grand Prix
            </h2>
            <p className="mt-3 text-muted-foreground">
              Every summer the Grand Prix scores runners across Alaska’s classic mountain races, from
              Mount Marathon to Bird Ridge to Matanuska Peak. Points add up race by race toward
              season-long Open and Age Division titles.
            </p>
          </div>

          <ul className="mb-10 grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
            {RULES.map((rule) => (
              <li key={rule.title} className="rounded-xl bg-muted/50 p-4">
                <span className="mb-3 inline-flex size-9 items-center justify-center rounded-lg bg-primary/15 text-primary">
                  <rule.icon className="size-5" aria-hidden="true" />
                </span>
                <h3 className="mb-1 font-semibold">{rule.title}</h3>
                <p className="text-sm text-muted-foreground">{rule.body}</p>
              </li>
            ))}
          </ul>

          {showSnapshot && (
            <div>
              <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
                <h3 className="flex items-center gap-2 text-lg font-semibold">
                  {year ? `${year} Open Division Leaders` : 'Open Division Leaders'}
                  {year && (
                    <Badge variant={isFinalized ? 'success' : 'secondary'}>{isFinalized ? 'Final' : 'Tentative'}</Badge>
                  )}
                </h3>
                {year && (
                  <Button
                    variant="outline"
                    size="sm"
                    nativeButton={false}
                    render={
                      <Link to={`/standings/${year}`} onClick={() => trackEvent('landing_cta_click', { cta: 'full_standings' })}>
                        Full standings <ArrowRight className="size-4" aria-hidden="true" />
                      </Link>
                    }
                  />
                )}
              </div>
              <div className="grid gap-4 md:grid-cols-2">
                {SNAPSHOT_GENDERS.map((g) => (
                  <LeaderList key={g} gender={g} standings={leaders[g] || []} loading={loading} />
                ))}
              </div>
            </div>
          )}
        </div>
      </div>
    </section>
  );
}

export default function Landing() {
  const { user } = useAuth();

  return (
    <div className="min-h-svh bg-background">
      <section className="relative overflow-hidden bg-gradient-to-b from-primary/10 via-secondary/5 to-background">
        <div className="relative z-10 mx-auto max-w-4xl px-4 pt-12 text-center md:pt-20">
          {user && (
            <p className="mb-4 text-sm text-muted-foreground">
              Welcome back, {user.firstName || user.email}.
              {user.runnerId && (
                <>
                  {' '}
                  <Link to={`/runners/${user.runnerId}`} className="font-medium text-primary hover:underline">
                    See your results
                  </Link>
                </>
              )}
            </p>
          )}
          <h1 className="text-3xl font-semibold tracking-tight text-balance md:text-5xl">
            Alaska Mountain Runners{' '}
            <span className="bg-gradient-to-r from-primary to-secondary bg-clip-text text-transparent">Race Results</span>
          </h1>
          <p className="mx-auto mt-4 max-w-2xl text-base text-pretty text-muted-foreground md:text-lg">
            The home of past results from Alaska’s mountain and trail running races. Browse by race,
            by year, or by athlete.
          </p>
          <div className="mt-8 flex flex-wrap items-center justify-center gap-3">
            <Button
              size="lg"
              nativeButton={false}
              render={
                <Link to="/race-results" onClick={() => trackEvent('landing_cta_click', { cta: 'browse_results' })}>
                  <ListChecks className="size-4" aria-hidden="true" /> Browse Race Results
                </Link>
              }
            />
            <Button
              size="lg"
              variant="outline"
              nativeButton={false}
              render={
                <Link to="/athletes" onClick={() => trackEvent('landing_cta_click', { cta: 'find_athlete' })}>
                  <Users className="size-4" aria-hidden="true" /> Find an Athlete
                </Link>
              }
            />
          </div>
          <div className="mt-6">
            <SiteTotals />
          </div>
        </div>
        <MountainRidge className="-mt-4 h-40 md:-mt-10 md:h-64" />
      </section>

      <GrandPrixSection />
    </div>
  );
}
