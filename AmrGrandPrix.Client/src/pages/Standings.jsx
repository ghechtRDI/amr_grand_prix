/**
 * Grand Prix Standings Dashboard
 * Shows standings for Open and Age divisions, filterable by year.
 */

import { useState, useEffect, useCallback } from 'react';
import { Link, useParams, useNavigate } from 'react-router-dom';
import { AlertCircle, Check } from 'lucide-react';
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from '@/components/ui/table';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Skeleton } from '@/components/ui/skeleton';
import { Tabs, TabsList, TabsTrigger, TabsContent } from '@/components/ui/tabs';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { cn } from '@/lib/utils';

const AGE_CATEGORIES = [
  '17 and Under',
  '18-29',
  '30-39',
  '40-49',
  '50-59',
  '60-69',
  '70-79',
  '80-89',
];

const DIVISION_OPEN = 'open';
const DIVISION_AGE = 'age';

const STICKY_HEAD =
  'sticky top-0 z-10 h-9 whitespace-nowrap bg-zinc-900 text-[11px] font-semibold tracking-wider text-zinc-300 uppercase dark:bg-black';

const RANK_TINTS = {
  1: 'border-amber-400/50 bg-amber-400/20 text-amber-700 dark:text-amber-400',
  2: 'border-slate-400/50 bg-slate-400/20 text-slate-600 dark:text-slate-300',
  3: 'border-orange-700/40 bg-orange-700/15 text-orange-700 dark:text-orange-400',
};

function RankBadge({ rank }) {
  return (
    <span
      className={cn(
        'inline-flex size-7 items-center justify-center rounded-full border text-sm font-bold tabular-nums',
        RANK_TINTS[rank] || 'border-transparent bg-muted text-muted-foreground'
      )}
    >
      {rank}
    </span>
  );
}

function StandingsTable({ standings, showAgeCategory }) {
  if (!standings || standings.length === 0) {
    return (
      <div className="rounded-xl border border-border bg-card px-6 py-16 text-center text-sm text-muted-foreground">
        No standings data for this division.
      </div>
    );
  }

  return (
    <div className="overflow-hidden rounded-xl border border-border">
      <div className="max-h-[70vh] overflow-auto">
        <Table>
          <TableHeader>
            <TableRow className="hover:bg-transparent">
              <TableHead className={STICKY_HEAD}>Rank</TableHead>
              <TableHead className={STICKY_HEAD}>Runner</TableHead>
              {showAgeCategory && <TableHead className={STICKY_HEAD}>Age Group</TableHead>}
              <TableHead className={cn(STICKY_HEAD, 'text-right')}>Total Points</TableHead>
              <TableHead className={cn(STICKY_HEAD, 'text-right')}>Races</TableHead>
              <TableHead className={cn(STICKY_HEAD, 'text-right')}>Best</TableHead>
              <TableHead className={cn(STICKY_HEAD, 'text-right')}>2nd</TableHead>
              <TableHead className={cn(STICKY_HEAD, 'text-center')}>Gamut</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {standings.map((s, idx) => (
              <TableRow
                key={s.standingId}
                className={cn(
                  idx % 2 === 1 && !s.runTheGamutQualified && 'bg-muted/20',
                  s.runTheGamutQualified && 'bg-secondary/10 hover:bg-secondary/15'
                )}
              >
                <TableCell><RankBadge rank={s.rank} /></TableCell>
                <TableCell className="font-medium text-foreground">
                  <Link to={`/runners/${s.runnerId}`} className="hover:text-primary">{s.runnerName}</Link>
                </TableCell>
                {showAgeCategory && <TableCell>{s.ageCategory || '—'}</TableCell>}
                <TableCell className="text-right font-semibold tabular-nums text-primary">{s.totalPoints}</TableCell>
                <TableCell className="text-right tabular-nums text-muted-foreground">{s.racesCounted}/{s.racesCompleted}</TableCell>
                <TableCell className="text-right tabular-nums">{s.bestRacePoints || '—'}</TableCell>
                <TableCell className="text-right tabular-nums">{s.secondBestRacePoints || '—'}</TableCell>
                <TableCell className="text-center">
                  {s.runTheGamutQualified && (
                    <Badge variant="success" className="gap-1">
                      <Check className="size-3" />
                    </Badge>
                  )}
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>
    </div>
  );
}

const CURRENT_YEAR = new Date().getFullYear();

export default function Standings() {
  const { year: yearParam } = useParams();
  const navigate = useNavigate();

  const [availableYears, setAvailableYears] = useState([]);
  const [selectedYear, setSelectedYear] = useState(parseInt(yearParam) || CURRENT_YEAR);
  const [mainTab, setMainTab] = useState(DIVISION_OPEN);
  const [genderTab, setGenderTab] = useState('male');
  const [ageCategory, setAgeCategory] = useState(AGE_CATEGORIES[2]); // default 30-39
  const [ageGender, setAgeGender] = useState('male');

  const [standings, setStandings] = useState([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);

  useEffect(() => {
    fetch('/api/standings/years')
      .then((r) => r.json())
      .then((years) => {
        if (years.length === 0) {
          setAvailableYears([CURRENT_YEAR]);
        } else {
          setAvailableYears(years);
          if (!yearParam) setSelectedYear(years[0]);
        }
      })
      .catch(() => setAvailableYears([CURRENT_YEAR]));
  }, [yearParam]);

  const loadStandings = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      let url;
      if (mainTab === DIVISION_OPEN) {
        url = `/api/standings/${selectedYear}/open/${genderTab}`;
      } else {
        const encoded = encodeURIComponent(ageCategory);
        url = `/api/standings/${selectedYear}/age/${encoded}/${ageGender}`;
      }

      const res = await fetch(url);
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      const data = await res.json();
      setStandings(data.standings || []);
    } catch (e) {
      setError(e.message);
      setStandings([]);
    } finally {
      setLoading(false);
    }
  }, [selectedYear, mainTab, genderTab, ageCategory, ageGender]);

  useEffect(() => {
    loadStandings();
  }, [loadStandings]);

  const handleYearChange = (y) => {
    setSelectedYear(parseInt(y));
    navigate(`/standings/${y}`, { replace: true });
  };

  const divisionLabel = () => {
    if (mainTab === DIVISION_OPEN) {
      return `Open ${genderTab === 'male' ? 'Male' : 'Female'} Division`;
    }
    return `${ageCategory} ${ageGender === 'male' ? 'Male' : 'Female'}`;
  };

  const GenderToggle = ({ value, onChange }) => (
    <div className="inline-flex overflow-hidden rounded-lg border border-border">
      {['male', 'female'].map((g, i) => (
        <Button
          key={g}
          type="button"
          size="sm"
          variant={value === g ? 'default' : 'ghost'}
          className={cn('rounded-none', i > 0 && 'border-l border-border')}
          onClick={() => onChange(g)}
        >
          {g === 'male' ? 'Male' : 'Female'}
        </Button>
      ))}
    </div>
  );

  return (
    <div className="min-h-svh bg-background px-4 py-8 md:py-12">
      <div className="mx-auto max-w-5xl">
        <div className="mb-6 flex flex-wrap items-center justify-between gap-4">
          <h1 className="bg-gradient-to-r from-primary to-secondary bg-clip-text text-2xl font-semibold tracking-tight text-transparent md:text-3xl">
            Grand Prix Standings
          </h1>
          <div className="flex items-center gap-2">
            <span className="text-sm text-muted-foreground">Year:</span>
            <Select value={String(selectedYear)} onValueChange={handleYearChange}>
              <SelectTrigger className="w-28">
                <SelectValue>{(value) => value}</SelectValue>
              </SelectTrigger>
              <SelectContent>
                {availableYears.map((y) => (
                  <SelectItem key={y} value={String(y)}>{y}</SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
        </div>

        <Tabs value={mainTab} onValueChange={setMainTab}>
          <TabsList variant="line" className="mb-6 h-auto gap-4 border-b border-border p-0">
            <TabsTrigger value={DIVISION_OPEN} className="rounded-none px-1 py-2 text-base data-active:font-semibold">
              Open Division
            </TabsTrigger>
            <TabsTrigger value={DIVISION_AGE} className="rounded-none px-1 py-2 text-base data-active:font-semibold">
              Age Divisions
            </TabsTrigger>
          </TabsList>

          <TabsContent value={DIVISION_OPEN}>
            <div className="mb-6">
              <GenderToggle value={genderTab} onChange={setGenderTab} />
            </div>
          </TabsContent>

          <TabsContent value={DIVISION_AGE}>
            <div className="mb-6 flex flex-wrap items-center gap-4">
              <div className="flex items-center gap-2">
                <span className="text-sm text-muted-foreground">Age Group:</span>
                <Select value={ageCategory} onValueChange={setAgeCategory}>
                  <SelectTrigger className="w-40">
                    <SelectValue>{(value) => value}</SelectValue>
                  </SelectTrigger>
                  <SelectContent>
                    {AGE_CATEGORIES.map((cat) => (
                      <SelectItem key={cat} value={cat}>{cat}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <GenderToggle value={ageGender} onChange={setAgeGender} />
            </div>
          </TabsContent>
        </Tabs>

        <div className="mb-4">
          <h2 className="text-lg font-semibold">{selectedYear} — {divisionLabel()}</h2>
          <p className="text-sm text-muted-foreground">
            Best 4 races count toward total. <Check className="inline size-3.5" /> = Run the Gamut (7+ races).
          </p>
        </div>

        {loading && (
          <div className="space-y-2">
            <Skeleton className="h-9 w-full" />
            <Skeleton className="h-64 w-full" />
          </div>
        )}
        {error && (
          <Alert variant="destructive">
            <AlertCircle />
            <AlertDescription>Error loading standings: {error}</AlertDescription>
          </Alert>
        )}
        {!loading && !error && (
          <StandingsTable
            standings={standings}
            showAgeCategory={mainTab === DIVISION_AGE}
          />
        )}

        <div className="mt-6 flex gap-6 border-t border-border pt-4 text-sm">
          <Link to="/" className="text-muted-foreground hover:text-foreground">← Home</Link>
        </div>
      </div>
    </div>
  );
}
