/**
 * Grand Prix Standings Dashboard
 * Shows standings for Open and Age divisions, a per-race points breakdown, and the year's
 * Run the Gamut finishers, filterable by year.
 */

import { useState, useEffect, useCallback } from 'react';
import { Link, useParams, useNavigate } from 'react-router-dom';
import { AlertCircle, Check } from 'lucide-react';
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from '@/components/ui/table';
import { Badge } from '@/components/ui/badge';
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
import { SearchInput } from '@/components/ui/search-input';
import { cn } from '@/lib/utils';
import { AGE_CATEGORIES } from '@/lib/ageCategories';
import { GenderToggle, RankBadge } from '@/components/standings/shared';
import { STICKY_HEAD, genderLabel } from '@/components/standings/constants';
import PointsBreakdown from '@/components/standings/PointsBreakdown';
import GamutFinishers from '@/components/standings/GamutFinishers';

const DIVISION_OPEN = 'open';
const DIVISION_AGE = 'age';
const VIEW_BREAKDOWN = 'breakdown';
const VIEW_GAMUT = 'gamut';

// Tabs that fetch their own data rather than a standings list
const isStandingsTab = (tab) => tab === DIVISION_OPEN || tab === DIVISION_AGE;

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
  // One gender for all tabs, so switching tabs keeps the selection
  const [gender, setGender] = useState('female');
  const [ageCategory, setAgeCategory] = useState(AGE_CATEGORIES[2]); // default 30-39

  const [standings, setStandings] = useState([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);
  const [searchTerm, setSearchTerm] = useState('');

  const filteredStandings = standings.filter((s) =>
    s.runnerName?.toLowerCase().includes(searchTerm.trim().toLowerCase())
  );

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
    if (!isStandingsTab(mainTab)) return; // PointsBreakdown / GamutFinishers fetch their own data
    setLoading(true);
    setError(null);
    try {
      let url;
      if (mainTab === DIVISION_OPEN) {
        url = `/api/standings/${selectedYear}/open/${gender}`;
      } else {
        const encoded = encodeURIComponent(ageCategory);
        url = `/api/standings/${selectedYear}/age/${encoded}/${gender}`;
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
  }, [selectedYear, mainTab, gender, ageCategory]);

  useEffect(() => {
    loadStandings();
  }, [loadStandings]);

  const handleYearChange = (y) => {
    setSelectedYear(parseInt(y));
    navigate(`/standings/${y}`, { replace: true });
  };

  // The Grand Prix doesn't have an official Nonbinary division yet, even though individual
  // races do report a nonbinary gender place (see RaceResults.jsx) and the backend already
  // computes Nonbinary Open/Age Grand Prix standings for whenever this division is added here.
  const divisionLabel = () => {
    if (mainTab === DIVISION_OPEN) return `Open ${genderLabel(gender)} Division`;
    if (mainTab === VIEW_BREAKDOWN) return `${genderLabel(gender)} Points Breakdown`;
    if (mainTab === VIEW_GAMUT) return 'Run the Gamut';
    return `${ageCategory} ${genderLabel(gender)}`;
  };

  return (
    <div className="min-h-svh bg-background px-4 py-8 md:py-12">
      <div className={cn('mx-auto', isStandingsTab(mainTab) ? 'max-w-5xl' : 'max-w-7xl')}>
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
            <TabsTrigger value={VIEW_BREAKDOWN} className="rounded-none px-1 py-2 text-base data-active:font-semibold">
              Points Breakdown
            </TabsTrigger>
            <TabsTrigger value={VIEW_GAMUT} className="rounded-none px-1 py-2 text-base data-active:font-semibold">
              Run the Gamut
            </TabsTrigger>
          </TabsList>

          <TabsContent value={DIVISION_OPEN}>
            <div className="mb-6">
              <GenderToggle value={gender} onChange={setGender} />
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
              <GenderToggle value={gender} onChange={setGender} />
            </div>
          </TabsContent>

          <TabsContent value={VIEW_BREAKDOWN}>
            <div className="mb-6">
              <GenderToggle value={gender} onChange={setGender} />
            </div>
          </TabsContent>
        </Tabs>

        <div className="mb-4 flex flex-wrap items-end justify-between gap-4">
          <div>
            <h2 className="text-lg font-semibold">{selectedYear} — {divisionLabel()}</h2>
            {isStandingsTab(mainTab) && (
              <p className="text-sm text-muted-foreground">
                Best 4 races count toward total. <Check className="inline size-3.5" /> = Run the Gamut (7+ events).
              </p>
            )}
          </div>
          <SearchInput
            value={searchTerm}
            onChange={setSearchTerm}
            placeholder="Search runners…"
            className="w-full sm:w-64"
          />
        </div>

        {mainTab === VIEW_BREAKDOWN && (
          <PointsBreakdown year={selectedYear} gender={gender} searchTerm={searchTerm} />
        )}
        {mainTab === VIEW_GAMUT && <GamutFinishers year={selectedYear} searchTerm={searchTerm} />}
        {isStandingsTab(mainTab) && loading && (
          <div className="space-y-2">
            <Skeleton className="h-9 w-full" />
            <Skeleton className="h-64 w-full" />
          </div>
        )}
        {isStandingsTab(mainTab) && error && (
          <Alert variant="destructive">
            <AlertCircle />
            <AlertDescription>Error loading standings: {error}</AlertDescription>
          </Alert>
        )}
        {isStandingsTab(mainTab) && !loading && !error && (
          <StandingsTable
            standings={filteredStandings}
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
