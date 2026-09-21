/**
 * Race Results Page
 * Shows all results for a single race, with GP points if applicable.
 */

import { useState, useEffect, useCallback } from 'react';
import { Link, useParams } from 'react-router-dom';
import { AlertCircle, ArrowDown, ArrowUp, ChevronsUpDown, Medal } from 'lucide-react';
import { useAuth } from '../hooks/useAuth';
import * as tokenService from '../services/tokenService';
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from '@/components/ui/table';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Skeleton } from '@/components/ui/skeleton';
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

const STATUS_OPTIONS = ['Finished', 'DNF', 'DNS', 'DQ'];
const GENDER_OPTIONS = ['Male', 'Female', 'Nonbinary'];
const GENDER_FILTERS = ['all', 'Male', 'Female'];

// Columns whose values are numbers/times - right-aligned with tabular figures.
const NUMERIC_COLUMNS = new Set(['place', 'placeGender', 'age', 'time']);

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

// DNF/DNS/DQ rows, and legacy rows saved with sentinel place=0/time=0:00:00, have no
// meaningful result and should always sort to the bottom regardless of sort direction.
function hasNoResult(r) {
  const status = toStatusLabel(r.status);
  if (status && status !== 'Finished') return true;
  if (r.place == null || r.place === 0) return true;
  return isZeroTime(r.time);
}

function genderLabel(g) {
  if (g === 0 || g === 'Male') return 'M';
  if (g === 1 || g === 'Female') return 'F';
  return g;
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
  const [genderFilter, setGenderFilter] = useState('all');
  const [sortField, setSortField] = useState('place');
  const [sortDir, setSortDir] = useState('asc');
  const [editingResultId, setEditingResultId] = useState(null);
  const [editValues, setEditValues] = useState({});
  const [saving, setSaving] = useState(false);
  const [deletingId, setDeletingId] = useState(null);
  const [deleteTarget, setDeleteTarget] = useState(null);
  const [actionError, setActionError] = useState(null);

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

  const filteredResults = results
    .filter((r) => {
      if (genderFilter === 'all') return true;
      const g = typeof r.gender === 'number'
        ? ['Male', 'Female', 'Nonbinary'][r.gender]
        : r.gender;
      return g === genderFilter;
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

        <div className="mt-6 mb-4 flex flex-wrap items-center justify-between gap-3">
          <div className="inline-flex overflow-hidden rounded-lg border border-border">
            {GENDER_FILTERS.map((g, i) => (
              <Button
                key={g}
                type="button"
                size="sm"
                variant={genderFilter === g ? 'default' : 'ghost'}
                className={cn('rounded-none', i > 0 && 'border-l border-border')}
                onClick={() => setGenderFilter(g)}
              >
                {g === 'all' ? 'All' : g}
              </Button>
            ))}
          </div>
          <span className="text-sm text-muted-foreground">{filteredResults.length} shown</span>
        </div>

        {actionError && (
          <Alert variant="destructive" className="mb-4">
            <AlertCircle />
            <AlertDescription>{actionError}</AlertDescription>
          </Alert>
        )}

        <div className="overflow-hidden rounded-xl border border-border">
          <div className="max-h-[70vh] overflow-auto">
            <Table>
              <TableHeader>
                <TableRow className="hover:bg-transparent">
                  <TableHead
                    onClick={() => handleSort('place')}
                    className={cn('sticky top-0 z-10 h-9 bg-zinc-900 text-right text-[11px] font-semibold tracking-wider text-zinc-300 uppercase dark:bg-black', sortableHeaderClass)}
                  >
                    Place<SortIcon field="place" />
                  </TableHead>
                  <TableHead
                    onClick={() => handleSort('placeGender')}
                    className={cn('sticky top-0 z-10 h-9 bg-zinc-900 text-right text-[11px] font-semibold tracking-wider text-zinc-300 uppercase dark:bg-black', sortableHeaderClass)}
                  >
                    {genderFilter === 'all' ? 'Gender Place' : 'Place'}<SortIcon field="placeGender" />
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
                  {race?.isGrandPrixRace && (
                    <TableHead className="sticky top-0 z-10 h-9 bg-zinc-900 text-[11px] font-semibold tracking-wider text-zinc-300 uppercase dark:bg-black">
                      Record
                    </TableHead>
                  )}
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
                        <TableCell>
                          <Input
                            type="text"
                            value={editValues.place}
                            className="h-8 text-right text-sm tabular-nums"
                            onChange={(e) => setEditValues(v => ({ ...v, place: e.target.value }))}
                          />
                        </TableCell>
                        <TableCell className="text-right tabular-nums">{r.placeGender ?? '—'}</TableCell>
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
                        {race?.isGrandPrixRace && <TableCell></TableCell>}
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
                      <TableCell className={cn('text-right font-bold tabular-nums', NUMERIC_COLUMNS.has('place') && 'text-primary')}>
                        {r.place ?? '—'}
                      </TableCell>
                      <TableCell className="text-right tabular-nums">{r.placeGender ?? '—'}</TableCell>
                      <TableCell className="font-medium text-foreground">
                        <Link to={`/runners/${r.runnerId}`} className="hover:text-primary">{r.runnerName}</Link>
                      </TableCell>
                      <TableCell className="text-right tabular-nums">{r.age ?? r.ageCategory ?? '—'}</TableCell>
                      <TableCell>{genderLabel(r.gender)}</TableCell>
                      <TableCell className="text-right font-mono text-sm tabular-nums">{formatTime(r.time)}</TableCell>
                      <TableCell><StatusBadge status={status} /></TableCell>
                      {race?.isGrandPrixRace && (
                        <TableCell>
                          {r.isNewRecord && (
                            <Badge className="gap-1 text-[10px] font-bold tracking-wide uppercase">
                              <Medal className="size-3" /> CR
                            </Badge>
                          )}
                        </TableCell>
                      )}
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
