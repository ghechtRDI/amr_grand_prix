/**
 * Race Results Browser
 * Browse historical race results either by year or by race series (all runnings of the same
 * event across years/variants).
 */

import { useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { Trophy } from 'lucide-react';
import { Card, CardHeader, CardTitle, CardDescription } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Skeleton } from '@/components/ui/skeleton';
import { Tabs, TabsList, TabsTrigger, TabsContent } from '@/components/ui/tabs';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { SearchInput } from '@/components/ui/search-input';

function GrandPrixBadge() {
  return (
    <Badge variant="success" className="gap-1">
      <Trophy className="size-3" /> Grand Prix
    </Badge>
  );
}

function ByYearView() {
  const [races, setRaces] = useState([]);
  const [loading, setLoading] = useState(true);
  const [year, setYear] = useState(null);
  const [search, setSearch] = useState('');

  useEffect(() => {
    fetch('/api/races')
      .then((r) => r.json())
      .then((data) => {
        setRaces(data);
        if (data.length > 0) setYear(data[0].year);
      })
      .finally(() => setLoading(false));
  }, []);

  const years = useMemo(
    () => [...new Set(races.map((r) => r.year))].sort((a, b) => b - a),
    [races]
  );

  const racesForYear = useMemo(
    () =>
      races
        .filter((r) => r.year === year)
        .filter((r) => r.name.toLowerCase().includes(search.trim().toLowerCase())),
    [races, year, search]
  );

  if (loading) return <Skeleton className="h-64 w-full" />;
  if (years.length === 0) {
    return <p className="py-12 text-center text-sm text-muted-foreground">No races have been recorded yet.</p>;
  }

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <Select value={year ? String(year) : ''} onValueChange={(v) => setYear(parseInt(v))}>
          <SelectTrigger className="w-28">
            <SelectValue>{(value) => value}</SelectValue>
          </SelectTrigger>
          <SelectContent>
            {years.map((y) => (
              <SelectItem key={y} value={String(y)}>{y}</SelectItem>
            ))}
          </SelectContent>
        </Select>
        <SearchInput value={search} onChange={setSearch} placeholder="Search races…" className="w-full sm:w-64" />
      </div>

      {racesForYear.length === 0 ? (
        <p className="py-12 text-center text-sm text-muted-foreground">No races found for {year}.</p>
      ) : (
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {racesForYear.map((race) => (
            <Link key={race.raceId} to={`/races/${race.raceId}/results`} className="group block h-full">
              <Card className="h-full border-2 border-transparent transition-all group-hover:-translate-y-0.5 group-hover:border-primary group-hover:shadow-md">
                <CardHeader>
                  <div className="flex items-start justify-between gap-2">
                    <CardTitle className="text-base">{race.name}</CardTitle>
                    {race.isGrandPrixRace && <GrandPrixBadge />}
                  </div>
                  <CardDescription>
                    {race.date}
                    {race.courseVariant && ` · ${race.courseVariant}`}
                    {' · '}
                    {race.resultsCount} result{race.resultsCount === 1 ? '' : 's'}
                  </CardDescription>
                </CardHeader>
              </Card>
            </Link>
          ))}
        </div>
      )}
    </div>
  );
}

function BySeriesView() {
  const [series, setSeries] = useState([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState('');

  useEffect(() => {
    fetch('/api/race-series')
      .then((r) => r.json())
      .then(setSeries)
      .finally(() => setLoading(false));
  }, []);

  const filtered = series.filter((s) => s.name.toLowerCase().includes(search.trim().toLowerCase()));

  if (loading) return <Skeleton className="h-64 w-full" />;

  return (
    <div className="flex flex-col gap-4">
      <SearchInput value={search} onChange={setSearch} placeholder="Search races…" className="w-full sm:w-64" />

      {filtered.length === 0 ? (
        <p className="py-12 text-center text-sm text-muted-foreground">No race series found.</p>
      ) : (
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {filtered.map((s) => (
            <Link key={s.raceSeriesId} to={`/race-series/${s.raceSeriesId}`} className="group block h-full">
              <Card className="h-full border-2 border-transparent transition-all group-hover:-translate-y-0.5 group-hover:border-primary group-hover:shadow-md">
                <CardHeader>
                  <div className="flex items-start justify-between gap-2">
                    <CardTitle className="text-base">{s.name}</CardTitle>
                    {s.isGrandPrixSeries && <GrandPrixBadge />}
                  </div>
                  <CardDescription>
                    {s.raceCount} running{s.raceCount === 1 ? '' : 's'}
                    {s.mostRecentYear && ` · most recent: ${s.mostRecentYear}`}
                  </CardDescription>
                </CardHeader>
              </Card>
            </Link>
          ))}
        </div>
      )}
    </div>
  );
}

export default function ResultsBrowser() {
  return (
    <div className="min-h-svh bg-background px-4 py-8 md:py-12">
      <div className="mx-auto max-w-5xl">
        <div className="mb-6">
          <h1 className="bg-gradient-to-r from-primary to-secondary bg-clip-text text-2xl font-semibold tracking-tight text-transparent md:text-3xl">
            Race Results
          </h1>
          <p className="mt-1 text-muted-foreground">Browse historical results by year or by race.</p>
        </div>

        <Tabs defaultValue="year">
          <TabsList variant="line" className="mb-6 h-auto gap-4 border-b border-border p-0">
            <TabsTrigger value="year" className="rounded-none px-1 py-2 text-base data-active:font-semibold">
              By Year
            </TabsTrigger>
            <TabsTrigger value="race" className="rounded-none px-1 py-2 text-base data-active:font-semibold">
              By Race
            </TabsTrigger>
          </TabsList>

          <TabsContent value="year"><ByYearView /></TabsContent>
          <TabsContent value="race"><BySeriesView /></TabsContent>
        </Tabs>

        <div className="mt-6 flex gap-6 border-t border-border pt-4 text-sm">
          <Link to="/" className="text-muted-foreground hover:text-foreground">← Home</Link>
        </div>
      </div>
    </div>
  );
}
