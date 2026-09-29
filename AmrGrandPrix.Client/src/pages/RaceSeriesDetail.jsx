/**
 * Race Series Detail
 * Every running of one event across years/variants, plus an all-time statistics view. A
 * logged-in user linked to a runner gets a link to their own results in this series.
 */

import { useEffect, useMemo, useState } from 'react';
import { Link, useParams, useSearchParams } from 'react-router-dom';
import { createColumnHelper } from '@tanstack/react-table';
import { Trophy } from 'lucide-react';
import { useAuth } from '../hooks/useAuth';
import { DataTable } from '@/components/ui/data-table';
import { Badge } from '@/components/ui/badge';
import { Skeleton } from '@/components/ui/skeleton';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Tabs, TabsList, TabsTrigger, TabsContent } from '@/components/ui/tabs';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Card, CardHeader, CardTitle, CardContent } from '@/components/ui/card';
import { formatTime } from '@/lib/time';

const columnHelper = createColumnHelper();

function GrandPrixBadge() {
  return (
    <Badge variant="success" className="gap-1">
      <Trophy className="size-3" /> Grand Prix
    </Badge>
  );
}

const RACE_COLUMNS = [
  columnHelper.accessor('year', { header: 'Year' }),
  columnHelper.accessor('date', { header: 'Date' }),
  columnHelper.accessor('courseVariant', { header: 'Course Variant', cell: (info) => info.getValue() || '—' }),
  columnHelper.display({
    id: 'gp',
    header: 'Grand Prix',
    cell: ({ row }) => (row.original.isGrandPrixRace ? <GrandPrixBadge /> : null),
  }),
  columnHelper.accessor('resultsCount', { header: 'Results' }),
  columnHelper.display({
    id: 'link',
    header: '',
    cell: ({ row }) => (
      <Link to={`/races/${row.original.raceId}/results`} className="font-medium text-primary hover:underline">
        View Results
      </Link>
    ),
  }),
];

function RacesTab({ races }) {
  return (
    <DataTable
      columns={RACE_COLUMNS}
      data={races}
      searchPlaceholder="Search year/variant…"
      emptyMessage="No races recorded in this series yet."
    />
  );
}

const TOP20_COLUMNS = [
  columnHelper.accessor('runnerName', { header: 'Runner' }),
  columnHelper.accessor('time', { header: 'Time', cell: (info) => formatTime(info.getValue()) }),
  columnHelper.accessor('year', { header: 'Year' }),
];

// Course Record History and Age Group Records columns are unused for now - see the comment
// in StatisticsTab below where their tables are hidden pending fixes to the underlying data.
const RECORD_HISTORY_COLUMNS = [
  columnHelper.accessor('runnerName', { header: 'Runner' }),
  columnHelper.accessor('time', { header: 'Time', cell: (info) => formatTime(info.getValue()) }),
  columnHelper.accessor('raceDate', { header: 'Date Set' }),
];

const AGE_GROUP_COLUMNS = [
  columnHelper.accessor('ageCategory', { header: 'Age Group' }),
  columnHelper.accessor('runnerName', { header: 'Runner' }),
  columnHelper.accessor('time', { header: 'Time', cell: (info) => formatTime(info.getValue()) }),
  columnHelper.accessor('raceDate', { header: 'Date Set' }),
];

function StatisticsTab({ seriesId, variants }) {
  // Variants with at least one race, in the series' display order.
  const racedVariants = useMemo(() => variants.filter((v) => v.raceCount > 0), [variants]);
  const [variantId, setVariantId] = useState(racedVariants[0]?.raceVariantId ?? null);
  const [gender, setGender] = useState('Male');
  const [stats, setStats] = useState(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    if (!variantId) {
      setLoading(false);
      return;
    }
    setLoading(true);
    const params = new URLSearchParams({ gender, variantId });
    fetch(`/api/race-series/${seriesId}/statistics?${params}`)
      .then((r) => (r.ok ? r.json() : null))
      .then(setStats)
      .finally(() => setLoading(false));
  }, [seriesId, variantId, gender]);

  if (!variantId) return <p className="text-muted-foreground">No results recorded yet.</p>;

  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-wrap items-center gap-4">
        {racedVariants.length > 1 && (
          <Select value={variantId} onValueChange={setVariantId}>
            <SelectTrigger className="w-56">
              <SelectValue>{(value) => racedVariants.find((v) => v.raceVariantId === value)?.name}</SelectValue>
            </SelectTrigger>
            <SelectContent>
              {racedVariants.map((v) => (
                <SelectItem key={v.raceVariantId} value={v.raceVariantId}>{v.name}</SelectItem>
              ))}
            </SelectContent>
          </Select>
        )}
        <Select value={gender} onValueChange={setGender}>
          <SelectTrigger className="w-32">
            <SelectValue>{(value) => value}</SelectValue>
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="Male">Male</SelectItem>
            <SelectItem value="Female">Female</SelectItem>
          </SelectContent>
        </Select>
      </div>

      {loading || !stats ? (
        <Skeleton className="h-64 w-full" />
      ) : (
        <>
          <Card>
            <CardHeader><CardTitle>Top 20 All-Time</CardTitle></CardHeader>
            <CardContent>
              <DataTable columns={TOP20_COLUMNS} data={stats.top20AllTime} emptyMessage="No finish times recorded yet." />
            </CardContent>
          </Card>

          {/* TODO: Course Record History and Age Group Records tables are hidden - the
              record-tracking data/logic behind them (RaceStatisticsService) needs more work
              before these are accurate enough to show. Re-enable using RECORD_HISTORY_COLUMNS /
              AGE_GROUP_COLUMNS + stats.courseRecordHistory / stats.ageGroupRecords once fixed. */}
        </>
      )}
    </div>
  );
}

const VALID_TABS = new Set(['races', 'stats']);

export default function RaceSeriesDetail() {
  const { seriesId } = useParams();
  const [searchParams] = useSearchParams();
  const { user, isAuthenticated } = useAuth();
  const [series, setSeries] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  const requestedTab = searchParams.get('tab');
  const initialTab = VALID_TABS.has(requestedTab) ? requestedTab : 'races';

  useEffect(() => {
    setLoading(true);
    fetch(`/api/race-series/${seriesId}`)
      .then((r) => {
        if (!r.ok) throw new Error(`HTTP ${r.status}`);
        return r.json();
      })
      .then(setSeries)
      .catch((e) => setError(e.message))
      .finally(() => setLoading(false));
  }, [seriesId]);

  const myRunnerId = isAuthenticated() ? user?.runnerId : null;

  return (
    <div className="min-h-svh bg-background px-4 py-8 md:py-12">
      <div className="mx-auto max-w-5xl">
        {loading && <Skeleton className="h-64 w-full" />}
        {error && (
          <Alert variant="destructive">
            <AlertDescription>Error loading race series: {error}</AlertDescription>
          </Alert>
        )}

        {series && (
          <>
            <div className="mb-6 flex flex-wrap items-center gap-3">
              <h1 className="bg-gradient-to-r from-primary to-secondary bg-clip-text text-2xl font-semibold tracking-tight text-transparent md:text-3xl">
                {series.name}
              </h1>
              {series.isGrandPrixSeries && <GrandPrixBadge />}
            </div>
            {series.description && <p className="mb-6 text-muted-foreground">{series.description}</p>}
            {myRunnerId && (
              <Link
                to={`/runners/${myRunnerId}/series/${seriesId}`}
                className="mb-6 inline-block text-sm font-medium text-primary hover:underline"
              >
                My results in this series →
              </Link>
            )}

            <Tabs defaultValue={initialTab}>
              <TabsList variant="line" className="mb-6 h-auto gap-4 border-b border-border p-0">
                <TabsTrigger value="races" className="rounded-none px-1 py-2 text-base data-active:font-semibold">
                  Races
                </TabsTrigger>
                <TabsTrigger value="stats" className="rounded-none px-1 py-2 text-base data-active:font-semibold">
                  Statistics
                </TabsTrigger>
              </TabsList>

              <TabsContent value="races"><RacesTab races={series.races} /></TabsContent>
              <TabsContent value="stats"><StatisticsTab seriesId={seriesId} variants={series.variants} /></TabsContent>
            </Tabs>
          </>
        )}

        <div className="mt-6 flex gap-6 border-t border-border pt-4 text-sm">
          <Link to="/race-results" className="text-muted-foreground hover:text-foreground">← Race Results</Link>
        </div>
      </div>
    </div>
  );
}
