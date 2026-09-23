/**
 * Race Results Page
 * Shows all results for a single race, with GP points if applicable.
 */

import { useState, useEffect, useCallback, useMemo } from 'react';
import { Link, useParams } from 'react-router-dom';
import { AlertCircle, ArrowDown, ArrowUp, ChevronsUpDown } from 'lucide-react';
import { useAuth } from '../hooks/useAuth';
import * as tokenService from '../services/tokenService';
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from '@/components/ui/table';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
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
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog';
import { cn, formatDateOnly } from '@/lib/utils';
import { AGE_CATEGORIES } from '@/lib/ageCategories';

const STATUS_OPTIONS = ['Finished', 'DNF', 'DNS', 'DQ'];
const GENDER_OPTIONS = ['Male', 'Female', 'Nonbinary'];

const RACE_RESULTS_TAB = 'race-results';
const AGE_GROUP_TAB = 'age-group';

const STATUS_BADGE_CLASSES = {
  Finished: 'bg-success/10 text-success dark:bg-success/20',
  DNF: 'bg-amber-500/10 text-amber-700 dark:bg-amber-500/15 dark:text-amber-400',
  DNS: 'bg-muted text-muted-foreground',
  DQ: 'bg-destructive/10 text-destructive dark:bg-destructive/20',
};

// Shared styling for the native <select> elements used for inline row editing -
// kept as native <select>s (matching the pattern used in DataReviewStep) since
// their onBlur-to-save behavior is load-bearing for the click-to-edit UX.
const SELECT_CLASS =
  'h-8 w-full rounded-md border border-input bg-transparent px-2 text-sm text-foreground outline-none transition-colors focus-visible:border-ring focus-visible:ring-2 focus-visible:ring-ring/40 disabled:opacity-50';

function toStatusLabel(status) {
  return typeof status === 'number' ? STATUS_OPTIONS[status] : status;
}

function toGenderLabel(gender) {
  return typeof gender === 'number' ? GENDER_OPTIONS[gender] : gender;
}

// "1:23:45" or "23:45" formatted TimeSpan (as returned by the API) -> plain string for editing
function timeToEditableString(timeSpan) {
  if (!timeSpan) return '';
  return timeSpan.split('.')[0]; // drop fractional seconds if present
}

function formatTime(timeSpan) {
  if (!timeSpan) return '—';
  const parts = timeSpan.split(':');
  if (parts.length === 3) {
    const h = parseInt(parts[0]);
    const m = parts[1];
    const s = parseFloat(parts[2]).toFixed(0).padStart(2, '0');
    if (h === 0) return `${m}:${s}`;
    return `${h}:${h >= 0 ? m : parts[1]}:${s}`;
  }
  return timeSpan;
}

// TimeSpan strings look like "00:12:34", "1.02:03:04" (day-prefixed), or with fractional seconds.
function isZeroTime(t) {
  if (!t) return true;
  const m = /^(?:(\d+)\.)?(\d+):(\d+):(\d+(?:\.\d+)?)$/.exec(t);
  if (!m) return false; // unrecognized format - don't misclassify a real time as zero
  return m.slice(1).every(part => !part || parseFloat(part) === 0);
}

// DNF/DNS/DQ rows, and legacy rows saved with sentinel time=0:00:00, have no meaningful
// result and should always sort to the bottom regardless of sort direction.
function hasNoResult(r) {
  const status = toStatusLabel(r.status);
  return (status && status !== 'Finished') || isZeroTime(r.time);
}

function genderLabel(g) {
  if (g === 0 || g === 'Male') return 'M';
  if (g === 1 || g === 'Female') return 'F';
  return g;
}

// Overall/gender/age-group places are computed from finish time rather than trusted from
// the stored `place`/`placeGender` fields, which can reflect a section-relative place (e.g.
// a per-gender results sheet) rather than the true rank - see RaceResults place computation.
function withComputedPlaces(results) {
  const finished = results.filter((r) => !hasNoResult(r));
  const byTime = [...finished].sort((a, b) => (a.time < b.time ? -1 : a.time > b.time ? 1 : 0));

  const overallPlace = new Map();
  byTime.forEach((r, i) => overallPlace.set(r.resultId, i + 1));

  const genderPlace = new Map();
  const byGender = new Map();
  byTime.forEach((r) => {
    const g = toGenderLabel(r.gender);
    if (!byGender.has(g)) byGender.set(g, []);
    byGender.get(g).push(r);
  });
  byGender.forEach((list) => {
    list.forEach((r, i) => genderPlace.set(r.resultId, i + 1));
  });

  const ageGroupPlace = new Map();
  const byGenderAndAge = new Map();
  byTime.forEach((r) => {
    const key = `${toGenderLabel(r.gender)}|${r.ageCategory ?? ''}`;
    if (!r.ageCategory) return;
    if (!byGenderAndAge.has(key)) byGenderAndAge.set(key, []);
    byGenderAndAge.get(key).push(r);
  });
  byGenderAndAge.forEach((list) => {
    list.forEach((r, i) => ageGroupPlace.set(r.resultId, i + 1));
  });

  return results.map((r) => ({
    ...r,
    computedPlace: overallPlace.get(r.resultId) ?? null,
    computedGenderPlace: genderPlace.get(r.resultId) ?? null,
    computedAgeGroupPlace: ageGroupPlace.get(r.resultId) ?? null,
  }));
}

function StatusBadge({ status }) {
  const s = typeof status === 'string' ? status : STATUS_OPTIONS[status] || status;
  return (
    <Badge variant="outline" className={cn('border-transparent font-medium', STATUS_BADGE_CLASSES[s] || 'bg-muted text-muted-foreground')}>
      {s}
    </Badge>
  );
}

export default function RaceResults() {
  const { raceId } = useParams();
  const { hasRole, hasAnyRole, isAuthenticated } = useAuth();
  const [race, setRace] = useState(null);
  const [results, setResults] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [mainTab, setMainTab] = useState(RACE_RESULTS_TAB);
  const [genderFilter, setGenderFilter] = useState('all');
  const [sortField, setSortField] = useState('computedPlace');
  const [sortDir, setSortDir] = useState('asc');
  const [editingResultId, setEditingResultId] = useState(null);
  const [editValues, setEditValues] = useState({});
  const [saving, setSaving] = useState(false);
  const [deletingId, setDeletingId] = useState(null);
  const [deleteTarget, setDeleteTarget] = useState(null);
  const [actionError, setActionError] = useState(null);
  const [ageGroupGender, setAgeGroupGender] = useState('Male');
  const [ageGroupCategory, setAgeGroupCategory] = useState(AGE_CATEGORIES[2]); // default 30-39

  const canEdit = isAuthenticated() && hasAnyRole(['Admin', 'Manager']);
  const canDelete = isAuthenticated() && hasRole('Admin');

  const fetchData = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [raceRes, resultsRes] = await Promise.all([
        fetch(`/api/races/detail/${raceId}`),
        fetch(`/api/results/race/${raceId}`),
      ]);

      if (!raceRes.ok) throw new Error('Race not found');
      if (!resultsRes.ok) throw new Error('Failed to load results');

      const [raceData, resultsData] = await Promise.all([
        raceRes.json(),
        resultsRes.json(),
      ]);

      setRace(raceData);
      setResults(resultsData);
    } catch (e) {
      setError(e.message);
    } finally {
      setLoading(false);
    }
  }, [raceId]);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  const augmentedResults = useMemo(() => withComputedPlaces(results), [results]);

  const hasNonbinary = useMemo(
    () => augmentedResults.some((r) => toGenderLabel(r.gender) === 'Nonbinary'),
    [augmentedResults]
  );

  const genderFilters = hasNonbinary ? ['all', 'Male', 'Female', 'Nonbinary'] : ['all', 'Male', 'Female'];
  const ageGroupGenders = hasNonbinary ? ['Male', 'Female', 'Nonbinary'] : ['Male', 'Female'];

  const startEdit = (r) => {
    setActionError(null);
    setEditingResultId(r.resultId);
    setEditValues({
      bib: r.bib ?? '',
      place: r.place ?? '',
      time: timeToEditableString(r.time),
      age: r.age ?? '',
      ageCategory: r.ageCategory ?? '',
      gender: toGenderLabel(r.gender) ?? '',
      status: toStatusLabel(r.status) ?? 'Finished',
      notes: r.notes ?? '',
    });
  };

  const cancelEdit = () => {
    setEditingResultId(null);
    setEditValues({});
  };

  const saveEdit = async (resultId) => {
    setSaving(true);
    setActionError(null);
    try {
      const res = await fetch(`/api/results/${resultId}`, {
        method: 'PUT',
        headers: {
          'Content-Type': 'application/json',
          Authorization: `Bearer ${tokenService.getAccessToken()}`,
        },
        body: JSON.stringify({
          bib: editValues.bib === '' ? null : parseInt(editValues.bib),
          place: editValues.place === '' ? null : parseInt(editValues.place),
          timeString: editValues.time || null,
          age: editValues.age === '' ? null : parseInt(editValues.age),
          ageCategory: editValues.age === '' ? (editValues.ageCategory || null) : null,
          gender: editValues.gender,
          status: editValues.status,
          notes: editValues.notes || null,
        }),
      });

      if (!res.ok) {
        const txt = await res.text();
        throw new Error(txt || `HTTP ${res.status}`);
      }

      setEditingResultId(null);
      setEditValues({});
      await fetchData();
    } catch (e) {
      setActionError(`Failed to save changes: ${e.message}`);
    } finally {
      setSaving(false);
    }
  };

  const handleDelete = async (r) => {
    setDeletingId(r.resultId);
    setActionError(null);
    try {
      const res = await fetch(`/api/results/${r.resultId}`, {
        method: 'DELETE',
        headers: { Authorization: `Bearer ${tokenService.getAccessToken()}` },
      });

      if (!res.ok) {
        const txt = await res.text();
        throw new Error(txt || `HTTP ${res.status}`);
      }

      await fetchData();
    } catch (e) {
      setActionError(`Failed to delete result: ${e.message}`);
    } finally {
      setDeletingId(null);
      setDeleteTarget(null);
    }
  };

  const handleSort = (field) => {
    if (sortField === field) {
      setSortDir((d) => (d === 'asc' ? 'desc' : 'asc'));
    } else {
      setSortField(field);
      setSortDir('asc');
    }
  };

  const SortIcon = ({ field }) => {
    if (sortField !== field) return <ChevronsUpDown className="ml-1 inline size-3.5 text-muted-foreground/60" />;
    return sortDir === 'asc'
      ? <ArrowUp className="ml-1 inline size-3.5" />
      : <ArrowDown className="ml-1 inline size-3.5" />;
  };

  const filteredResults = augmentedResults
    .filter((r) => {
      if (genderFilter === 'all') return true;
      return toGenderLabel(r.gender) === genderFilter;
    })
    .sort((a, b) => {
      const aNo = hasNoResult(a);
      const bNo = hasNoResult(b);
      if (aNo !== bNo) return aNo ? 1 : -1; // no-result rows always last, regardless of sort direction

      let valA = a[sortField];
      let valB = b[sortField];
      // nulls last
      if (valA == null) return 1;
      if (valB == null) return -1;
      if (typeof valA === 'string') valA = valA.toLowerCase();
      if (typeof valB === 'string') valB = valB.toLowerCase();
      const cmp = valA < valB ? -1 : valA > valB ? 1 : 0;
      return sortDir === 'asc' ? cmp : -cmp;
    });

  const ageGroupResults = augmentedResults
    .filter((r) => toGenderLabel(r.gender) === ageGroupGender && r.ageCategory === ageGroupCategory)
    .sort((a, b) => {
      const aNo = hasNoResult(a);
      const bNo = hasNoResult(b);
      if (aNo !== bNo) return aNo ? 1 : -1;
      if (aNo) return 0;
      return a.computedAgeGroupPlace - b.computedAgeGroupPlace;
    });

  const finishersCount = results.filter((r) => {
    const s = typeof r.status === 'number' ? r.status : STATUS_OPTIONS.indexOf(r.status);
    return s === 0;
  }).length;

  const sortableHeaderClass = 'cursor-pointer select-none whitespace-nowrap hover:text-white';

  if (loading) {
    return (
      <div className="min-h-svh bg-background px-4 py-8 md:py-12">
        <div className="mx-auto max-w-6xl space-y-4">
          <Skeleton className="h-8 w-64" />
          <Skeleton className="h-4 w-96" />
          <Skeleton className="h-64 w-full" />
        </div>
      </div>
    );
  }

  if (error) {
    return (
      <div className="min-h-svh bg-background px-4 py-8 md:py-12">
        <div className="mx-auto max-w-6xl">
          <Alert variant="destructive">
            <AlertCircle />
            <AlertDescription>{error}</AlertDescription>
          </Alert>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-svh bg-background px-4 py-8 md:py-12">
      <div className="mx-auto max-w-6xl">
        <div className="mb-2 text-sm text-muted-foreground">
          <Link to="/" className="hover:text-foreground">Home</Link>
          <span className="mx-1.5">/</span>
          <span>Race Results</span>
        </div>

        <h1 className="bg-gradient-to-r from-primary to-secondary bg-clip-text text-2xl font-semibold tracking-tight text-transparent md:text-3xl">
          {race?.name}
        </h1>

        <div className="mt-2 flex flex-wrap items-center gap-x-4 gap-y-1.5 text-sm text-muted-foreground">
          {race?.date && (
            <span>{formatDateOnly(race.date)}</span>
          )}
          {race?.location && <span>{race.location}</span>}
          {race?.courseVariant && <span>Course: {race.courseVariant}</span>}
          {race?.isGrandPrixRace && (
            <Badge variant="secondary" className="text-[10px] font-bold tracking-wide uppercase">Grand Prix</Badge>
          )}
        </div>

        <div className="mt-1 text-sm text-muted-foreground">
          {results.length} results
          {finishersCount !== results.length && <span> ({finishersCount} finishers)</span>}
        </div>

        {actionError && (
          <Alert variant="destructive" className="mt-4">
            <AlertCircle />
            <AlertDescription>{actionError}</AlertDescription>
          </Alert>
        )}

        <Tabs value={mainTab} onValueChange={setMainTab} className="mt-6">
          <TabsList variant="line" className="mb-4 h-auto gap-4 border-b border-border p-0">
            <TabsTrigger value={RACE_RESULTS_TAB} className="rounded-none px-1 py-2 text-base data-active:font-semibold">
              Race Results
            </TabsTrigger>
            <TabsTrigger value={AGE_GROUP_TAB} className="rounded-none px-1 py-2 text-base data-active:font-semibold">
              Age Group Results
            </TabsTrigger>
          </TabsList>

          <TabsContent value={RACE_RESULTS_TAB}>
            <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
              <div className="inline-flex overflow-hidden rounded-lg border border-border">
                {genderFilters.map((g, i) => (
                  <Button
                    key={g}
                    type="button"
                    size="sm"
                    variant={genderFilter === g ? 'default' : 'ghost'}
                    className={cn('rounded-none', i > 0 && 'border-l border-border')}
                    onClick={() => setGenderFilter(g)}
                  >
                    {g === 'all' ? 'Overall' : g}
                  </Button>
                ))}
              </div>
              <span className="text-sm text-muted-foreground">{filteredResults.length} shown</span>
            </div>

            <div className="overflow-hidden rounded-xl border border-border">
              <div className="max-h-[70vh] overflow-auto">
                <Table>
                  <TableHeader>
                    <TableRow className="hover:bg-transparent">
                      <TableHead
                        onClick={() => handleSort('computedPlace')}
                        className={cn('sticky top-0 z-10 h-9 bg-zinc-900 text-right text-[11px] font-semibold tracking-wider text-zinc-300 uppercase dark:bg-black', sortableHeaderClass)}
                      >
                        Place<SortIcon field="computedPlace" />
                      </TableHead>
                      <TableHead
                        onClick={() => handleSort('computedGenderPlace')}
                        className={cn('sticky top-0 z-10 h-9 bg-zinc-900 text-right text-[11px] font-semibold tracking-wider text-zinc-300 uppercase dark:bg-black', sortableHeaderClass)}
                      >
                        {genderFilter === 'all' ? 'Gender Place' : 'Place'}<SortIcon field="computedGenderPlace" />
                      </TableHead>
                      <TableHead
                        onClick={() => handleSort('runnerName')}
                        className={cn('sticky top-0 z-10 h-9 bg-zinc-900 text-[11px] font-semibold tracking-wider text-zinc-300 uppercase dark:bg-black', sortableHeaderClass)}
                      >
                        Name<SortIcon field="runnerName" />
                      </TableHead>
                      <TableHead
                        onClick={() => handleSort('age')}
                        className={cn('sticky top-0 z-10 h-9 bg-zinc-900 text-right text-[11px] font-semibold tracking-wider text-zinc-300 uppercase dark:bg-black', sortableHeaderClass)}
                      >
                        Age<SortIcon field="age" />
                      </TableHead>
                      <TableHead className="sticky top-0 z-10 h-9 bg-zinc-900 text-[11px] font-semibold tracking-wider text-zinc-300 uppercase dark:bg-black">
                        Gender
                      </TableHead>
                      <TableHead
                        onClick={() => handleSort('time')}
                        className={cn('sticky top-0 z-10 h-9 bg-zinc-900 text-right text-[11px] font-semibold tracking-wider text-zinc-300 uppercase dark:bg-black', sortableHeaderClass)}
                      >
                        Time<SortIcon field="time" />
                      </TableHead>
                      <TableHead className="sticky top-0 z-10 h-9 bg-zinc-900 text-[11px] font-semibold tracking-wider text-zinc-300 uppercase dark:bg-black">
                        Status
                      </TableHead>
                      {(canEdit || canDelete) && (
                        <TableHead className="sticky top-0 z-10 h-9 bg-zinc-900 text-right text-[11px] font-semibold tracking-wider text-zinc-300 uppercase dark:bg-black">
                          Actions
                        </TableHead>
                      )}
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {filteredResults.map((r, idx) => {
                      const status = toStatusLabel(r.status);
                      const isEditing = editingResultId === r.resultId;
                      const isDnf = status !== 'Finished';

                      if (isEditing) {
                        return (
                          <TableRow key={r.resultId} className="bg-primary/5 hover:bg-primary/5">
                            <TableCell className="text-right font-bold tabular-nums text-primary">
                              {r.computedPlace ?? '—'}
                            </TableCell>
                            <TableCell className="text-right tabular-nums">{r.computedGenderPlace ?? '—'}</TableCell>
                            <TableCell className="font-medium text-foreground">
                              <Link to={`/runners/${r.runnerId}`} className="hover:text-primary">{r.runnerName}</Link>
                            </TableCell>
                            <TableCell>
                              <Input
                                type="text"
                                value={editValues.age}
                                placeholder="Age"
                                className="h-8 text-right text-sm tabular-nums"
                                onChange={(e) => setEditValues(v => ({ ...v, age: e.target.value }))}
                              />
                            </TableCell>
                            <TableCell>
                              <select
                                value={editValues.gender}
                                className={SELECT_CLASS}
                                onChange={(e) => setEditValues(v => ({ ...v, gender: e.target.value }))}
                              >
                                {GENDER_OPTIONS.map(g => <option key={g} value={g}>{g}</option>)}
                              </select>
                            </TableCell>
                            <TableCell>
                              <Input
                                type="text"
                                value={editValues.time}
                                placeholder="h:mm:ss"
                                className="h-8 text-right text-sm tabular-nums"
                                onChange={(e) => setEditValues(v => ({ ...v, time: e.target.value }))}
                              />
                            </TableCell>
                            <TableCell>
                              <select
                                value={editValues.status}
                                className={SELECT_CLASS}
                                onChange={(e) => setEditValues(v => ({ ...v, status: e.target.value }))}
                              >
                                {STATUS_OPTIONS.map(s => <option key={s} value={s}>{s}</option>)}
                              </select>
                            </TableCell>
                            <TableCell className="text-right whitespace-nowrap">
                              <div className="flex justify-end gap-1.5">
                                <Button size="sm" disabled={saving} onClick={() => saveEdit(r.resultId)}>
                                  {saving ? 'Saving...' : 'Save'}
                                </Button>
                                <Button size="sm" variant="outline" disabled={saving} onClick={cancelEdit}>
                                  Cancel
                                </Button>
                              </div>
                            </TableCell>
                          </TableRow>
                        );
                      }

                      return (
                        <TableRow
                          key={r.resultId}
                          className={cn(idx % 2 === 1 && 'bg-muted/20', isDnf && 'opacity-70')}
                        >
                          <TableCell className="text-right font-bold tabular-nums text-primary">
                            {r.computedPlace ?? '—'}
                          </TableCell>
                          <TableCell className="text-right tabular-nums">{r.computedGenderPlace ?? '—'}</TableCell>
                          <TableCell className="font-medium text-foreground">
                            <Link to={`/runners/${r.runnerId}`} className="hover:text-primary">{r.runnerName}</Link>
                          </TableCell>
                          <TableCell className="text-right tabular-nums">{r.age ?? r.ageCategory ?? '—'}</TableCell>
                          <TableCell>{genderLabel(r.gender)}</TableCell>
                          <TableCell className="text-right font-mono text-sm tabular-nums">{formatTime(r.time)}</TableCell>
                          <TableCell><StatusBadge status={status} /></TableCell>
                          {(canEdit || canDelete) && (
                            <TableCell className="text-right whitespace-nowrap">
                              <div className="flex justify-end gap-1.5">
                                {canEdit && (
                                  <Button size="sm" variant="outline" onClick={() => startEdit(r)}>
                                    Edit
                                  </Button>
                                )}
                                {canDelete && (
                                  <Button
                                    size="sm"
                                    variant="destructive"
                                    disabled={deletingId === r.resultId}
                                    onClick={() => setDeleteTarget(r)}
                                  >
                                    {deletingId === r.resultId ? 'Deleting...' : 'Delete'}
                                  </Button>
                                )}
                              </div>
                            </TableCell>
                          )}
                        </TableRow>
                      );
                    })}
                  </TableBody>
                </Table>
              </div>
            </div>
          </TabsContent>

          <TabsContent value={AGE_GROUP_TAB}>
            <div className="mb-4 flex flex-wrap items-center gap-4">
              <div className="inline-flex overflow-hidden rounded-lg border border-border">
                {ageGroupGenders.map((g, i) => (
                  <Button
                    key={g}
                    type="button"
                    size="sm"
                    variant={ageGroupGender === g ? 'default' : 'ghost'}
                    className={cn('rounded-none', i > 0 && 'border-l border-border')}
                    onClick={() => setAgeGroupGender(g)}
                  >
                    {g}
                  </Button>
                ))}
              </div>
              <div className="flex items-center gap-2">
                <span className="text-sm text-muted-foreground">Age Group:</span>
                <Select value={ageGroupCategory} onValueChange={setAgeGroupCategory}>
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
              <span className="text-sm text-muted-foreground">{ageGroupResults.length} shown</span>
            </div>

            {ageGroupResults.length === 0 ? (
              <div className="rounded-xl border border-border bg-card px-6 py-16 text-center text-sm text-muted-foreground">
                No {ageGroupGender} {ageGroupCategory} results for this race.
              </div>
            ) : (
              <div className="overflow-hidden rounded-xl border border-border">
                <div className="max-h-[70vh] overflow-auto">
                  <Table>
                    <TableHeader>
                      <TableRow className="hover:bg-transparent">
                        <TableHead className="sticky top-0 z-10 h-9 bg-zinc-900 text-right text-[11px] font-semibold tracking-wider text-zinc-300 uppercase dark:bg-black">
                          Age Group Place
                        </TableHead>
                        <TableHead className="sticky top-0 z-10 h-9 bg-zinc-900 text-[11px] font-semibold tracking-wider text-zinc-300 uppercase dark:bg-black">
                          Name
                        </TableHead>
                        <TableHead className="sticky top-0 z-10 h-9 bg-zinc-900 text-right text-[11px] font-semibold tracking-wider text-zinc-300 uppercase dark:bg-black">
                          Age
                        </TableHead>
                        <TableHead className="sticky top-0 z-10 h-9 bg-zinc-900 text-right text-[11px] font-semibold tracking-wider text-zinc-300 uppercase dark:bg-black">
                          Time
                        </TableHead>
                        <TableHead className="sticky top-0 z-10 h-9 bg-zinc-900 text-[11px] font-semibold tracking-wider text-zinc-300 uppercase dark:bg-black">
                          Status
                        </TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {ageGroupResults.map((r, idx) => {
                        const status = toStatusLabel(r.status);
                        const isDnf = status !== 'Finished';
                        return (
                          <TableRow
                            key={r.resultId}
                            className={cn(idx % 2 === 1 && 'bg-muted/20', isDnf && 'opacity-70')}
                          >
                            <TableCell className="text-right font-bold tabular-nums text-primary">
                              {r.computedAgeGroupPlace ?? '—'}
                            </TableCell>
                            <TableCell className="font-medium text-foreground">
                              <Link to={`/runners/${r.runnerId}`} className="hover:text-primary">{r.runnerName}</Link>
                            </TableCell>
                            <TableCell className="text-right tabular-nums">{r.age ?? '—'}</TableCell>
                            <TableCell className="text-right font-mono text-sm tabular-nums">{formatTime(r.time)}</TableCell>
                            <TableCell><StatusBadge status={status} /></TableCell>
                          </TableRow>
                        );
                      })}
                    </TableBody>
                  </Table>
                </div>
              </div>
            )}
          </TabsContent>
        </Tabs>

        <div className="mt-6 flex flex-wrap gap-6 border-t border-border pt-4 text-sm">
          <Link to="/" className="text-muted-foreground hover:text-foreground">← Home</Link>
          {race?.isGrandPrixRace && (
            <Link to={`/standings/${race.year}`} className="text-muted-foreground hover:text-foreground">View GP Standings →</Link>
          )}
          {race?.raceSeriesId && (
            <Link to={`/race-series/${race.raceSeriesId}?tab=stats`} className="text-muted-foreground hover:text-foreground">
              View Race Statistics →
            </Link>
          )}
          <Link to="/report" className="text-muted-foreground hover:text-foreground">Report an issue with these results</Link>
        </div>
      </div>

      <AlertDialog open={!!deleteTarget} onOpenChange={(open) => !open && setDeleteTarget(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Delete this result?</AlertDialogTitle>
            <AlertDialogDescription>
              Delete {deleteTarget?.runnerName}&rsquo;s result from this race? This cannot be undone.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction variant="destructive" onClick={() => handleDelete(deleteTarget)}>
              Delete
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
