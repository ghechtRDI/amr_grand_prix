/**
 * Race Series Detail
 * Every running of one event across years/variants, plus an all-time statistics view and (for a
 * logged-in user linked to a runner with results in this series) a personal performance chart.
 */

import { useEffect, useMemo, useState } from 'react';
import { Link, useParams, useSearchParams } from 'react-router-dom';
import { createColumnHelper } from '@tanstack/react-table';
import { LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer } from 'recharts';
import { Trophy } from 'lucide-react';
import { useAuth } from '../hooks/useAuth';
import * as tokenService from '../services/tokenService';
import { DataTable } from '@/components/ui/data-table';
import { Badge } from '@/components/ui/badge';
import { Skeleton } from '@/components/ui/skeleton';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Tabs, TabsList, TabsTrigger, TabsContent } from '@/components/ui/tabs';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Card, CardHeader, CardTitle, CardContent } from '@/components/ui/card';
import { formatTime, parseTimeSpanToSeconds } from '@/lib/time';

const columnHelper = createColumnHelper();

function GrandPrixBadge() {
  return (
    <Badge variant="success" className="gap-1">
      <Trophy className="size-3" /> Grand Prix
    </Badge>
  );
}

function formatSecondsAsClock(totalSeconds) {
  if (totalSeconds == null) return '';
  const h = Math.floor(totalSeconds / 3600);
  const m = Math.floor((totalSeconds % 3600) / 60);
  const s = Math.round(totalSeconds % 60);
  return h > 0
    ? `${h}:${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`
    : `${m}:${String(s).padStart(2, '0')}`;
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

const STANDARD_VARIANT = '__standard__';

function StatisticsTab({ seriesId, races }) {
  const variants = useMemo(
    () => [...new Set(races.map((r) => r.courseVariant || STANDARD_VARIANT))],
    [races]
  );
  const [variant, setVariant] = useState(variants[0] ?? STANDARD_VARIANT);
  const [gender, setGender] = useState('Male');
  const [stats, setStats] = useState(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    setLoading(true);
    const params = new URLSearchParams({ gender });
    if (variant && variant !== STANDARD_VARIANT) params.set('variant', variant);

    fetch(`/api/race-series/${seriesId}/statistics?${params}`)
      .then((r) => (r.ok ? r.json() : null))
      .then(setStats)
      .finally(() => setLoading(false));
  }, [seriesId, variant, gender]);

  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-wrap items-center gap-4">
        {variants.length > 1 && (
          <Select value={variant} onValueChange={setVariant}>
            <SelectTrigger className="w-48">
              <SelectValue>{(value) => (value === STANDARD_VARIANT ? 'Standard' : value)}</SelectValue>
            </SelectTrigger>
            <SelectContent>
              {variants.map((v) => (
                <SelectItem key={v} value={v}>{v === STANDARD_VARIANT ? 'Standard' : v}</SelectItem>
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

const HISTORY_COLUMNS = [
  columnHelper.accessor('year', { header: 'Year' }),
  columnHelper.accessor('courseVariant', { header: 'Course Variant', cell: (info) => info.getValue() || '—' }),
  columnHelper.accessor('time', { header: 'Time', cell: (info) => formatTime(info.getValue()) }),
  columnHelper.accessor('place', { header: 'Place', cell: (info) => info.getValue() ?? '—' }),
  columnHelper.accessor('status', { header: 'Status' }),
];

function MyHistoryTab({ runnerId, seriesId }) {
  const [history, setHistory] = useState(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    fetch(`/api/runners/${runnerId}/series/${seriesId}/history`, {
      headers: { Authorization: `Bearer ${tokenService.getAccessToken()}` },
    })
      .then((r) => (r.ok ? r.json() : []))
      .then(setHistory)
      .finally(() => setLoading(false));
  }, [runnerId, seriesId]);

  const chartData = useMemo(
    () =>
      (history || [])
        .filter((h) => h.status === 'Finished' && h.time)
        .map((h) => ({ year: h.year, seconds: parseTimeSpanToSeconds(h.time) }))
        .sort((a, b) => a.year - b.year),
    [history]
  );

  if (loading) return <Skeleton className="h-64 w-full" />;
  if (!history || history.length === 0) {
    return <p className="py-8 text-center text-sm text-muted-foreground">You don't have any results in this series yet.</p>;
  }

  return (
    <div className="flex flex-col gap-6">
      {chartData.length > 1 && (
        <Card>
          <CardHeader><CardTitle>Your Finish Time Over the Years</CardTitle></CardHeader>
          <CardContent>
            <ResponsiveContainer width="100%" height={280}>
              <LineChart data={chartData} margin={{ left: 8, right: 16 }}>
                <CartesianGrid strokeDasharray="3 3" className="stroke-border" />
                <XAxis dataKey="year" tick={{ fontSize: 12 }} />
                <YAxis tickFormatter={formatSecondsAsClock} tick={{ fontSize: 12 }} width={60} />
                <Tooltip formatter={(value) => [formatSecondsAsClock(value), 'Time']} labelFormatter={(year) => `Year ${year}`} />
                <Line type="monotone" dataKey="seconds" stroke="var(--color-primary)" strokeWidth={2} dot={{ r: 4 }} />
              </LineChart>
            </ResponsiveContainer>
          </CardContent>
        </Card>
      )}

      <DataTable columns={HISTORY_COLUMNS} data={history} searchPlaceholder="Search…" />
    </div>
  );
}

const VALID_TABS = new Set(['races', 'stats', 'mine']);

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

  const showMyHistory = isAuthenticated() && !!user?.runnerId;

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

            <Tabs defaultValue={initialTab === 'mine' && !showMyHistory ? 'races' : initialTab}>
              <TabsList variant="line" className="mb-6 h-auto gap-4 border-b border-border p-0">
                <TabsTrigger value="races" className="rounded-none px-1 py-2 text-base data-active:font-semibold">
                  Races
                </TabsTrigger>
                <TabsTrigger value="stats" className="rounded-none px-1 py-2 text-base data-active:font-semibold">
                  Statistics
                </TabsTrigger>
                {showMyHistory && (
                  <TabsTrigger value="mine" className="rounded-none px-1 py-2 text-base data-active:font-semibold">
                    My History
                  </TabsTrigger>
                )}
              </TabsList>

              <TabsContent value="races"><RacesTab races={series.races} /></TabsContent>
              <TabsContent value="stats"><StatisticsTab seriesId={seriesId} races={series.races} /></TabsContent>
              {showMyHistory && (
                <TabsContent value="mine">
                  <MyHistoryTab runnerId={user.runnerId} seriesId={seriesId} />
                </TabsContent>
              )}
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
