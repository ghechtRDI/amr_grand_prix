/**
 * Step 4: Data Review & Validation
 * Editable table to review and fix data before saving
 */

import { useState, useMemo, useCallback, useEffect } from 'react';
import {
  useReactTable,
  getCoreRowModel,
  flexRender,
  createColumnHelper,
} from '@tanstack/react-table';
import { AlertTriangle, ArrowLeftRight, CheckCircle2, Sparkles, UserCheck, UserPlus, UserSearch, Trash2 } from 'lucide-react';
import * as tokenService from '../../services/tokenService';
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from '@/components/ui/table';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Skeleton } from '@/components/ui/skeleton';
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
import { cn } from '@/lib/utils';

const columnHelper = createColumnHelper();

// Must match AmrGrandPrix.API.Models.GrandPrixConstants.AgeCategories
const AGE_CATEGORIES = [
  '17 and Under', '18-29', '30-39', '40-49', '50-59', '60-69', '70-79', '80-89',
];

// Columns whose values are numbers/times - right-aligned with tabular figures
// for scannability, matching the rest of the app's data tables.
const NUMERIC_COLUMN_IDS = new Set(['place', 'bib', 'age', 'time']);

// Must match AmrGrandPrix.API.Models.GrandPrixConstants.MaxPlausibleRaceHours. Longer than
// this, a parsed H:MM:SS time is more likely an LLM misread of a MM:SS time (e.g. "30:21"
// read as 30h21m instead of 30m21s) than a real AMR race result.
const MAX_PLAUSIBLE_RACE_HOURS = 10;

// Splits a "H:MM" or "H:MM:SS" time string into its fields, or null if it isn't in
// one of those shapes.
function parseTimeParts(value) {
  const parts = String(value ?? '').trim().split(':');
  if (parts.length < 2 || parts.length > 3 || parts.some(p => !/^\d+$/.test(p))) return null;
  return parts;
}

// True when a time looks like a misread MM:SS. The LLM extracts the raw source string
// as-is (e.g. "34:21"), but the backend's time parser reads a bare 2-field string as
// H:MM before it ever tries MM:SS, so a MM:SS race time silently becomes "34 hours
// 21 minutes". A first field over MAX_PLAUSIBLE_RACE_HOURS is the tell.
function looksShifted(value) {
  const parts = parseTimeParts(value);
  return !!parts && Number(parts[0]) > MAX_PLAUSIBLE_RACE_HOURS;
}

// Makes an ambiguous time string unambiguous as MM:SS.
function shiftTimeFields(value) {
  const parts = parseTimeParts(value);
  if (!parts) return value;
  // 2-field "H:MM" - add an explicit "0" hours field so the backend's H:MM:SS parser
  // reads it correctly instead of misreading it as hours:minutes.
  if (parts.length === 2) return `0:${parts[0]}:${parts[1]}`;
  // 3-field "H:MM:SS" where the LLM fabricated a spurious seconds field - drop it.
  return `${parts[0]}:${parts[1]}`;
}

const STATUS_BADGE_CLASSES = {
  Finished: 'bg-success/10 text-success dark:bg-success/20',
  DNF: 'bg-amber-500/10 text-amber-700 dark:bg-amber-500/15 dark:text-amber-400',
  DNS: 'bg-muted text-muted-foreground',
  DQ: 'bg-destructive/10 text-destructive dark:bg-destructive/20',
};

// Per-row runner-match state, derived in the `data` state below from the backend's
// suggested matches / auto-match, and updated live as the admin uses the dropdown.
const MATCH_STATUS = {
  'auto-matched': {
    label: 'Auto-matched',
    icon: Sparkles,
    cls: 'bg-indigo-500/10 text-indigo-700 dark:bg-indigo-500/15 dark:text-indigo-400',
  },
  'needs-confirmation': {
    label: 'Confirm match',
    icon: UserSearch,
    cls: 'bg-purple-500/10 text-purple-700 dark:bg-purple-500/15 dark:text-purple-400',
  },
  confirmed: {
    label: 'Confirmed',
    icon: UserCheck,
    cls: 'bg-success/10 text-success dark:bg-success/20',
  },
  'new-runner': {
    label: 'New runner',
    icon: UserPlus,
    cls: 'bg-sky-500/10 text-sky-700 dark:bg-sky-500/15 dark:text-sky-400',
  },
};

// Shared styling for the native <select> elements used for inline table-cell
// editing. These stay native <select>s (rather than the shadcn Select popover)
// because their autoFocus-on-open / blur-to-save behavior is load-bearing for
// the click-to-edit table UX and shouldn't change during a restyle.
const SELECT_CLASS =
  'h-7 w-full rounded-md border border-input bg-transparent px-1.5 text-sm text-foreground outline-none transition-colors focus-visible:border-ring focus-visible:ring-2 focus-visible:ring-ring/40 disabled:opacity-50';

function StatusBadge({ status }) {
  return (
    <Badge
      variant="outline"
      className={cn('border-transparent font-medium', STATUS_BADGE_CLASSES[status] || 'bg-muted text-muted-foreground')}
    >
      {status}
    </Badge>
  );
}

// Defined at module scope so it never changes reference between renders.
// Reads editingCell / setEditingCell / updateData from table.options.meta.
function EditableCell({ getValue, row, column, table }) {
  const { editingCell, setEditingCell, updateData } = table.options.meta;
  const initialValue = getValue();
  const [value, setValue] = useState(initialValue);
  const isEditing = editingCell === `${row.index}-${column.id}`;

  // Bulk actions (e.g. "Shift All") write the row's value directly via updateData,
  // bypassing this cell's own onBlur, so the displayed value must be resynced whenever
  // it changes underneath us. Skipped while this cell is being actively edited so it
  // doesn't clobber in-progress input.
  useEffect(() => {
    if (!isEditing) setValue(initialValue);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [initialValue]);

  const onBlur = () => {
    updateData(row.index, column.id, value);
    setEditingCell(null);
  };

  const onKeyDown = (e) => {
    if (e.key === 'Enter') {
      onBlur();
    } else if (e.key === 'Escape') {
      setValue(initialValue);
      setEditingCell(null);
    }
  };

  if (isEditing) {
    if (column.id === 'gender') {
      return (
        <select
          value={value}
          onChange={(e) => setValue(e.target.value)}
          onBlur={onBlur}
          autoFocus
          className={SELECT_CLASS}
        >
          <option value="">-- Select --</option>
          <option value="Male">Male</option>
          <option value="Female">Female</option>
          <option value="Nonbinary">Nonbinary</option>
        </select>
      );
    }

    if (column.id === 'status') {
      return (
        <select
          value={value}
          onChange={(e) => setValue(e.target.value)}
          onBlur={onBlur}
          autoFocus
          className={SELECT_CLASS}
        >
          <option value="Finished">Finished</option>
          <option value="DNF">DNF</option>
          <option value="DNS">DNS</option>
          <option value="DQ">DQ</option>
        </select>
      );
    }

    if (column.id === 'ageCategory') {
      return (
        <select
          value={value}
          onChange={(e) => setValue(e.target.value)}
          onBlur={onBlur}
          autoFocus
          className={SELECT_CLASS}
        >
          <option value="">-- Unknown --</option>
          {AGE_CATEGORIES.map(cat => (
            <option key={cat} value={cat}>{cat}</option>
          ))}
        </select>
      );
    }

    return (
      <Input
        type="text"
        value={value}
        onChange={(e) => setValue(e.target.value)}
        onBlur={onBlur}
        onKeyDown={onKeyDown}
        autoFocus
        className={cn('h-7 px-1.5 text-sm', NUMERIC_COLUMN_IDS.has(column.id) && 'text-right')}
      />
    );
  }

  return (
    <div
      onClick={() => setEditingCell(`${row.index}-${column.id}`)}
      className={cn(
        'flex min-h-7 cursor-pointer items-center rounded px-1.5 py-1 transition-colors hover:bg-muted/60',
        NUMERIC_COLUMN_IDS.has(column.id) && 'justify-end tabular-nums'
      )}
    >
      {column.id === 'status' && value ? <StatusBadge status={value} /> : (value || '-')}
    </div>
  );
}

// Time column cell: wraps EditableCell with a "Shift Fields" button that appears when the
// value looks like a MM:SS time that will be misread as H:MM (e.g. "30:21" instead of "0:30:21").
function TimeCell(props) {
  const { row, table } = props;
  const { editingCell, updateData } = table.options.meta;
  const value = props.getValue();
  const isEditing = editingCell === `${row.index}-time`;
  const shiftedValue = shiftTimeFields(value);

  return (
    <div className="flex items-center justify-end gap-1">
      {!isEditing && looksShifted(value) && (
        <button
          type="button"
          title={`This looks like a misread MM:SS time. Shift fields: "${value}" → "${shiftedValue}"`}
          onClick={() => updateData(row.index, 'time', shiftedValue)}
          className="flex size-5 shrink-0 items-center justify-center rounded text-amber-600 transition-colors hover:bg-amber-500/15 dark:text-amber-400"
        >
          <ArrowLeftRight className="size-3.5" />
        </button>
      )}
      <div className="min-w-0 flex-1">
        <EditableCell {...props} />
      </div>
    </div>
  );
}

// Short, concrete explanation of *why* a row has the match status it does -
// shown as the pill's tooltip so the admin doesn't have to guess.
function matchStatusTooltip(matchStatus, matches, selectedMatch) {
  switch (matchStatus) {
    case 'auto-matched':
      return selectedMatch
        ? `Auto-matched to ${selectedMatch.firstName} ${selectedMatch.lastName} (${Math.round(selectedMatch.confidence * 100)}% confidence) — review or change below`
        : 'Auto-matched — review or change below';
    case 'needs-confirmation':
      return `${matches.length} possible match${matches.length === 1 ? '' : 'es'} found (≥70% confidence) — none selected yet`;
    case 'confirmed':
      return selectedMatch
        ? `Confirmed match to ${selectedMatch.firstName} ${selectedMatch.lastName}`
        : 'Confirmed match';
    case 'new-runner':
    default:
      return matches.length > 0
        ? 'Marked as a new runner instead of the suggested match(es) below'
        : 'No matching runner found — a new runner record will be created';
  }
}

// Dropdown for confirming/selecting which existing runner a row matches.
// Always shows "+ New Runner" plus any suggested matches, sorted by confidence.
function RunnerMatchCell({ row, table }) {
  const { updateRunnerMatch, updateData } = table.options.meta;
  const matches = row.original.runnerMatches || [];
  const selected = row.original.matchedRunnerId || '';
  const matchStatus = row.original.matchStatus;
  const statusInfo = MATCH_STATUS[matchStatus];
  const StatusIcon = statusInfo?.icon;

  const selectedMatch = matches.find(m => m.runnerId === selected);
  const uploadedAge = row.original.age === '' ? null : Number(row.original.age);
  const hasAgeDiscrepancy =
    !!selectedMatch && uploadedAge != null && selectedMatch.age != null && uploadedAge !== selectedMatch.age;

  const statusPill = statusInfo && (
    <span
      title={matchStatusTooltip(matchStatus, matches, selectedMatch)}
      className={cn('inline-flex w-fit items-center gap-1 rounded-full px-2 py-0.5 text-xs font-medium', statusInfo.cls)}
    >
      {StatusIcon && <StatusIcon className="size-3" />}
      {statusInfo.label}
    </span>
  );

  if (matches.length === 0) {
    return (
      <div className="flex flex-col gap-1.5">
        {statusPill}
        <span className="text-sm text-muted-foreground">Will be created as a new runner</span>
      </div>
    );
  }

  return (
    <div className="flex flex-col gap-1.5">
      {statusPill}
      <select
        value={selected}
        onChange={(e) => updateRunnerMatch(row.index, e.target.value || null)}
        className={SELECT_CLASS}
      >
        <option value="">+ New Runner</option>
        {matches.map(m => (
          <option key={m.runnerId} value={m.runnerId}>
            {m.firstName} {m.lastName} · age {m.age ?? '?'}{m.age != null && !m.hasVerifiedDateOfBirth ? ' (est.)' : ''} · {Math.round(m.confidence * 100)}%
          </option>
        ))}
      </select>
      {hasAgeDiscrepancy && (
        selectedMatch.hasVerifiedDateOfBirth ? (
          <span
            className="text-xs text-muted-foreground"
            title="This runner has a verified date of birth and can't be overwritten here"
          >
            Verified age on file ({selectedMatch.age}) — uploaded age {uploadedAge} won&apos;t change it
          </span>
        ) : (
          <label
            className="flex items-center gap-1.5 text-xs text-amber-600 dark:text-amber-400"
            title="The uploaded age differs from this runner's estimated age"
          >
            <input
              type="checkbox"
              className="size-3.5 rounded border-input accent-primary"
              checked={!!row.original.updateRunnerAge}
              onChange={(e) => updateData(row.index, 'updateRunnerAge', e.target.checked)}
            />
            Update estimated age ({selectedMatch.age} &rarr; {uploadedAge})
          </label>
        )
      )}
    </div>
  );
}

// Race resolver shown for each course-variant group that isn't the primary
// (pre-selected in Step 1) race — pick an existing race or create a new one.
// Candidates are restricted to races on the same date (and, when Step 1 picked a series, within
// that series), since that's the only pool a same-event, same-day variant could belong to. If one
// of them already has a matching course variant, it's auto-selected.
function GroupRaceSelector({ groupKey, primaryRaceSelection, resolved, onResolve }) {
  const raceSeriesId = primaryRaceSelection.raceSeriesId;
  const raceDate = primaryRaceSelection.raceDate;

  const [races, setRaces] = useState([]);
  const [loading, setLoading] = useState(true);
  const [mode, setMode] = useState('select');
  const [autoMatched, setAutoMatched] = useState(false);
  const [newRace, setNewRace] = useState({
    name: primaryRaceSelection.raceName || groupKey,
    date: raceDate || new Date().toISOString().split('T')[0],
    isGrandPrixRace: false,
    courseVariant: groupKey,
  });
  const [creating, setCreating] = useState(false);
  const [error, setError] = useState(null);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        setLoading(true);
        let candidates = [];

        if (raceSeriesId) {
          const res = await fetch(`/api/race-series/${raceSeriesId}`);
          if (res.ok) {
            const series = await res.json();
            candidates = series.races.filter(r => r.date === raceDate);
          }
        } else {
          const token = tokenService.getAccessToken();
          const year = raceDate ? new Date(raceDate).getFullYear() : new Date().getFullYear();
          const res = await fetch(`/api/races/${year}`, {
            headers: { Authorization: `Bearer ${token}` },
          });
          if (res.ok) {
            const list = await res.json();
            candidates = list.filter(r => r.date === raceDate);
          }
        }

        if (cancelled) return;
        setRaces(candidates);

        // Auto-select an existing race for this date whose variant already matches.
        const match = candidates.find(
          r => (r.courseVariant || '').trim().toLowerCase() === groupKey.trim().toLowerCase()
        );
        if (match) {
          setAutoMatched(true);
          onResolve({
            raceId: match.raceId,
            raceName: primaryRaceSelection.raceName || groupKey,
            isGrandPrixRace: !!match.isGrandPrixRace,
          });
        }
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => { cancelled = true; };
    // Runs once per group when its date/series context is known; onResolve is intentionally
    // excluded (a fresh function identity every parent render would otherwise re-fire this).
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [raceSeriesId, raceDate, groupKey]);

  // base-ui's Select doesn't resolve a selected item's label from its children the
  // way Radix does - it needs an explicit lookup to render anything but the raw value.
  const raceOptionLabel = (value) => {
    if (!value) return null;
    if (value === 'new') return '+ Create New Race';
    const race = races.find(r => r.raceId === value);
    if (!race) return value;
    return `${race.courseVariant || 'Standard'}${race.resultsCount != null ? ` (${race.resultsCount} results)` : ''}`;
  };

  const handleSelect = (value) => {
    setAutoMatched(false);
    if (value === 'new') {
      setMode('new');
      onResolve(null);
    } else if (value) {
      setMode('select');
      const race = races.find(r => r.raceId === value);
      onResolve({ raceId: value, raceName: primaryRaceSelection.raceName || groupKey, isGrandPrixRace: !!race?.isGrandPrixRace });
    } else {
      setMode('select');
      onResolve(null);
    }
  };

  const handleCreate = async () => {
    try {
      setCreating(true);
      setError(null);
      const token = tokenService.getAccessToken();
      const res = await fetch('/api/races', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${token}` },
        body: JSON.stringify({
          name: newRace.name,
          date: newRace.date,
          isGrandPrixRace: newRace.isGrandPrixRace,
          courseVariant: newRace.courseVariant || null,
          location: null,
          raceSeriesId: raceSeriesId || null,
        }),
      });
      if (!res.ok) throw new Error('Failed to create race');
      const created = await res.json();
      onResolve({ raceId: created.raceId, raceName: created.name, isGrandPrixRace: !!created.isGrandPrixRace });
    } catch (err) {
      setError(err.message);
    } finally {
      setCreating(false);
    }
  };

  return (
    <div className="flex flex-wrap items-center gap-2">
      <Label className="text-sm font-normal text-muted-foreground">
        Race for &ldquo;{groupKey}&rdquo;:
      </Label>
      {loading ? (
        <Skeleton className="h-8 w-56" />
      ) : (
        <Select
          value={mode === 'new' ? 'new' : (resolved?.raceId || undefined)}
          onValueChange={handleSelect}
        >
          <SelectTrigger className="min-w-56">
            <SelectValue placeholder="-- Select a race --">
              {(value) => raceOptionLabel(value) || '-- Select a race --'}
            </SelectValue>
          </SelectTrigger>
          <SelectContent>
            {races.map(r => (
              <SelectItem key={r.raceId} value={r.raceId}>
                {r.courseVariant || 'Standard'}
                {r.resultsCount != null ? ` (${r.resultsCount} results)` : ''}
              </SelectItem>
            ))}
            <SelectItem value="new">+ Create New Race</SelectItem>
          </SelectContent>
        </Select>
      )}
      {autoMatched && mode !== 'new' && (
        <span className="text-xs text-success">Matched by variant name</span>
      )}
      {mode === 'new' && (
        <div className="mt-2 flex w-full flex-wrap items-center gap-2">
          <Input
            type="text"
            value={newRace.name}
            onChange={(e) => setNewRace({ ...newRace, name: e.target.value })}
            placeholder="Race name"
            disabled={!!resolved}
            className="w-48"
          />
          <Input
            type="date"
            value={newRace.date}
            onChange={(e) => setNewRace({ ...newRace, date: e.target.value })}
            disabled={!!resolved}
            className="w-40"
          />
          <label className="flex items-center gap-1.5 text-sm text-muted-foreground">
            <input
              type="checkbox"
              className="size-4 rounded border-input accent-primary"
              checked={newRace.isGrandPrixRace}
              onChange={(e) => setNewRace({ ...newRace, isGrandPrixRace: e.target.checked })}
              disabled={!!resolved}
            />
            Grand Prix race
          </label>
          <Input
            type="text"
            value={newRace.courseVariant}
            onChange={(e) => setNewRace({ ...newRace, courseVariant: e.target.value })}
            placeholder="Course variant"
            disabled={!!resolved}
            className="w-40"
          />
          <Button type="button" variant="secondary" size="sm" onClick={handleCreate} disabled={creating || !!resolved}>
            {resolved ? 'Race Created ✓' : (creating ? 'Creating…' : 'Create Race')}
          </Button>
          {error && <span className="text-sm text-destructive">{error}</span>}
        </div>
      )}
    </div>
  );
}

const COLUMNS = [
  columnHelper.accessor('place',  { header: 'Place',  cell: EditableCell, size: 80 }),
  columnHelper.accessor('bib',    { header: 'Bib',    cell: EditableCell, size: 80 }),
  columnHelper.accessor('name',   { header: 'Name',   cell: EditableCell, size: 200 }),
  columnHelper.accessor('age',    { header: 'Age',    cell: EditableCell, size: 80 }),
  columnHelper.accessor('ageCategory', { header: 'Age Category', cell: EditableCell, size: 120 }),
  columnHelper.accessor('gender', { header: 'Gender', cell: EditableCell, size: 100 }),
  columnHelper.accessor('time',   { header: 'Time',   cell: TimeCell, size: 120 }),
  columnHelper.accessor('status', { header: 'Status', cell: EditableCell, size: 100 }),
  columnHelper.accessor('courseVariant', { header: 'Course Variant', cell: EditableCell, size: 140 }),
  columnHelper.display({
    id: 'runnerMatch',
    header: 'Runner Match',
    cell: RunnerMatchCell,
    size: 220,
  }),
  columnHelper.display({
    id: 'validation',
    header: 'Validation',
    cell: ({ row }) => {
      const issues = row.original.validationIssues || [];

      if (issues.length === 0) {
        return <CheckCircle2 className="mx-auto size-4 text-success" />;
      }

      return (
        <div className="flex items-center justify-center gap-1">
          {issues.map((issue, idx) => {
            const severity = (issue.severity || '').toLowerCase();
            return (
              <span
                key={idx}
                title={issue.message}
                className={cn(
                  'flex size-5 items-center justify-center rounded',
                  severity === 'error' && 'bg-destructive/15 text-destructive',
                  severity === 'warning' && 'bg-amber-500/15 text-amber-600 dark:text-amber-400',
                  severity === 'info' && 'bg-sky-500/15 text-sky-600 dark:text-sky-400',
                  !['error', 'warning', 'info'].includes(severity) && 'bg-muted text-muted-foreground'
                )}
              >
                <AlertTriangle className="size-3.5" />
              </span>
            );
          })}
        </div>
      );
    },
    size: 90,
  }),
  columnHelper.display({
    id: 'actions',
    header: '',
    cell: ({ row, table }) => {
      const { requestDeleteRow } = table.options.meta;
      return (
        <button
          type="button"
          title="Remove this row"
          onClick={() => requestDeleteRow(row.index, row.original.name)}
          className="mx-auto flex size-6 items-center justify-center rounded text-muted-foreground transition-colors hover:bg-destructive/10 hover:text-destructive"
        >
          <Trash2 className="size-3.5" />
        </button>
      );
    },
    size: 48,
  }),
];

export default function DataReviewStep({ wizardData, onNext, onBack, onCancel }) {
  const [data, setData] = useState(() => {
    const parsedResults = wizardData.parsedResults || [];

    return parsedResults.map((row, idx) => ({
      id: idx,
      name:   row.name   || '',
      age:    row.age    ?? '',
      ageCategory: row.ageCategory ?? '',
      place:  row.place  ?? '',
      // Use timeString (original LLM-extracted string) for display/editing
      time:   row.timeString || '',
      gender: row.gender || '',
      bib:    row.bib    ?? '',
      status: row.status || 'Finished',
      courseVariant: row.courseVariant || '',
      validationIssues: row.validationIssues || [],
      runnerMatches: row.runnerMatches || [],
      // 'auto-matched': backend pre-selected via ≥95% confidence, unconfirmed by the admin.
      // 'needs-confirmation': candidate matches (≥70%) exist, none selected yet.
      // 'new-runner': no candidates - will be created as new.
      matchStatus: (() => {
        const matches = row.runnerMatches || [];
        if (matches.length === 0) return 'new-runner';
        if (row.matchedRunnerId) return 'auto-matched';
        return 'needs-confirmation';
      })(),
      matchedRunnerId: row.matchedRunnerId || null,
      updateRunnerAge: false,
    }));
  });

  const [editingCell, setEditingCell] = useState(null);
  // Course-variant group key -> { raceId, raceName } chosen via GroupRaceSelector.
  // The "primary" group (pre-selected race from Step 1) never needs an entry here.
  const [groupRaces, setGroupRaces] = useState(wizardData.groupRaces || {});

  const updateData = useCallback((rowIndex, columnId, value) => {
    setData(old =>
      old.map((row, index) =>
        index === rowIndex ? { ...row, [columnId]: value } : row
      )
    );
  }, []);

  // Applies shiftTimeFields to every row whose time looks misread, in one update.
  const shiftAllTimes = useCallback(() => {
    setData(old =>
      old.map(row => (looksShifted(row.time) ? { ...row, time: shiftTimeFields(row.time) } : row))
    );
  }, []);

  // Called when the user picks a suggested runner (or "+ New Runner") from the dropdown.
  const updateRunnerMatch = useCallback((rowIndex, runnerId) => {
    setData(old =>
      old.map((row, index) =>
        index === rowIndex
          ? { ...row, matchedRunnerId: runnerId, matchStatus: runnerId ? 'confirmed' : 'new-runner', updateRunnerAge: false }
          : row
      )
    );
  }, []);

  const [deleteTarget, setDeleteTarget] = useState(null); // { rowIndex, name }

  const requestDeleteRow = useCallback((rowIndex, name) => {
    setDeleteTarget({ rowIndex, name });
  }, []);

  const confirmDeleteRow = useCallback(() => {
    setData(old => old.filter((_, i) => i !== deleteTarget.rowIndex));
    // Row indices shift after a delete - drop any in-progress edit rather than risk
    // it landing on the wrong (reindexed) row.
    setEditingCell(null);
    setDeleteTarget(null);
  }, [deleteTarget]);

  const table = useReactTable({
    data,
    columns: COLUMNS,
    getCoreRowModel: getCoreRowModel(),
    meta: { editingCell, setEditingCell, updateData, updateRunnerMatch, requestDeleteRow },
  });

  const stats = useMemo(() => ({
    totalResults: data.length,
    warnings: data.filter(row => row.validationIssues?.length > 0).length,
    newRunners: data.filter(row => row.matchStatus === 'new-runner').length,
    needsReview: data.filter(row => row.matchStatus === 'needs-confirmation').length,
    shiftableTimes: data.filter(row => looksShifted(row.time)).length,
  }), [data]);

  const primaryRaceSelection = wizardData.raceSelection || {};

  // Group rows by detected course variant.
  const rowGroupsMap = new Map();
  for (const row of table.getRowModel().rows) {
    const key = (row.original.courseVariant || '').trim();
    if (!rowGroupsMap.has(key)) rowGroupsMap.set(key, []);
    rowGroupsMap.get(key).push(row);
  }
  const rowGroups = Array.from(rowGroupsMap.entries());
  const hasMultipleGroups = rowGroups.length > 1;
  // Only a single detected group (no variant ambiguity) is automatically the Step 1
  // selection. When multiple variants are detected, none of them can be assumed to
  // match Step 1's race - which variant is which isn't derivable from upload order
  // (e.g. a junior race section can appear before the main race in the source file),
  // so the admin must explicitly resolve every group below, including whichever one
  // is actually the Step 1 race.
  const primaryKey = hasMultipleGroups ? null : (rowGroups[0]?.[0] ?? '');

  const tableHead = (
    <TableHeader>
      {table.getHeaderGroups().map(headerGroup => (
        <TableRow key={headerGroup.id} className="hover:bg-transparent">
          {headerGroup.headers.map(header => (
            <TableHead
              key={header.id}
              style={{ width: header.getSize() }}
              className={cn(
                'sticky top-0 z-10 h-9 whitespace-nowrap bg-zinc-900 text-[11px] font-semibold uppercase tracking-wider text-zinc-300 dark:bg-black',
                NUMERIC_COLUMN_IDS.has(header.column.id) && 'text-right'
              )}
            >
              {flexRender(header.column.columnDef.header, header.getContext())}
            </TableHead>
          ))}
        </TableRow>
      ))}
    </TableHeader>
  );

  const renderTableBody = (rows) => (
    <TableBody>
      {rows.map(row => {
        const rowState = row.original.validationIssues?.length > 0
          ? 'warning'
          : row.original.matchStatus === 'needs-confirmation'
            ? 'needs-confirmation'
            : row.original.matchStatus === 'new-runner'
              ? 'new-runner'
              : null;
        return (
          <TableRow
            key={row.id}
            className={cn(
              row.index % 2 === 1 && !rowState && 'bg-muted/20',
              rowState === 'warning' && 'bg-amber-500/10 hover:bg-amber-500/15',
              rowState === 'needs-confirmation' && 'bg-purple-500/10 hover:bg-purple-500/15',
              rowState === 'new-runner' && 'bg-sky-500/10 hover:bg-sky-500/15'
            )}
          >
            {row.getVisibleCells().map(cell => (
              <TableCell
                key={cell.id}
                className={cn(NUMERIC_COLUMN_IDS.has(cell.column.id) && 'text-right tabular-nums')}
              >
                {flexRender(cell.column.columnDef.cell, cell.getContext())}
              </TableCell>
            ))}
          </TableRow>
        );
      })}
    </TableBody>
  );

  const renderTable = (rows) => (
    <div className="overflow-hidden rounded-xl border border-border">
      <div className="max-h-[32rem] overflow-y-auto">
        <Table>
          {tableHead}
          {renderTableBody(rows)}
        </Table>
      </div>
    </div>
  );

  const handleSubmit = () => {
    // Place/Time are only required for Finished results - DNF/DNS/DQ rows legitimately
    // have neither, mirroring the backend's ValidateRow().
    const invalidRows = data.filter(row => {
      if (!row.name) return true;
      if (!row.age && !row.ageCategory) return true;
      if (row.status === 'Finished' && (!row.place || !row.time)) return true;
      return false;
    });

    if (invalidRows.length > 0) {
      alert(
        `${invalidRows.length} row(s) are missing required fields (Name, Age or Age Category, and — for Finished results — Place and Time). Please fix these issues before continuing.`
      );
      return;
    }

    const unresolvedGroups = rowGroups
      .map(([key]) => key)
      .filter(key => key !== primaryKey && !groupRaces[key]?.raceId);

    if (unresolvedGroups.length > 0) {
      alert(
        `Please select or create a race for the following detected course variant(s): ${unresolvedGroups.join(', ')}`
      );
      return;
    }

    onNext({ reviewedData: data, groupRaces, primaryGroupKey: primaryKey });
  };

  return (
    <div>
      <div className="mb-6 flex items-center justify-between border-b border-border pb-4">
        <h2 className="text-xl font-semibold">Step 3: Data Review</h2>
        <span className="text-sm text-muted-foreground">Step 3 of {wizardData.totalSteps || 4}</span>
      </div>

      <div className="mb-6">
        <div className="mb-3 flex flex-wrap gap-3">
          <div className="rounded-lg border border-border bg-muted/30 px-4 py-2">
            <div className="text-xs font-medium uppercase tracking-wide text-muted-foreground">Total Results</div>
            <div className="text-xl font-semibold tabular-nums">{stats.totalResults}</div>
          </div>
          {stats.warnings > 0 && (
            <div className="rounded-lg border border-amber-500/30 bg-amber-500/10 px-4 py-2">
              <div className="text-xs font-medium uppercase tracking-wide text-amber-700 dark:text-amber-400">Warnings</div>
              <div className="text-xl font-semibold tabular-nums text-amber-700 dark:text-amber-400">{stats.warnings}</div>
            </div>
          )}
          {stats.newRunners > 0 && (
            <div className="rounded-lg border border-sky-500/30 bg-sky-500/10 px-4 py-2">
              <div className="text-xs font-medium uppercase tracking-wide text-sky-700 dark:text-sky-400">New Runners</div>
              <div className="text-xl font-semibold tabular-nums text-sky-700 dark:text-sky-400">{stats.newRunners}</div>
            </div>
          )}
          {stats.needsReview > 0 && (
            <div className="rounded-lg border border-purple-500/30 bg-purple-500/10 px-4 py-2">
              <div className="text-xs font-medium uppercase tracking-wide text-purple-700 dark:text-purple-400">
                Possible Matches to Confirm
              </div>
              <div className="text-xl font-semibold tabular-nums text-purple-700 dark:text-purple-400">{stats.needsReview}</div>
            </div>
          )}
        </div>
        <p className="text-sm text-muted-foreground">
          Click any cell to edit. Press Enter to save, Escape to cancel.
          {stats.needsReview > 0 && ' Use the Runner Match column to confirm or reject suggested matches.'}
        </p>
        {stats.shiftableTimes > 0 && (
          <div className="mt-3 flex items-center gap-2 rounded-lg border border-amber-500/30 bg-amber-500/10 px-4 py-2">
            <span className="text-sm text-amber-700 dark:text-amber-400">
              {stats.shiftableTimes} time{stats.shiftableTimes === 1 ? '' : 's'} look{stats.shiftableTimes === 1 ? 's' : ''} like a misread MM:SS.
            </span>
            <Button type="button" variant="outline" size="sm" onClick={shiftAllTimes} className="gap-1.5 border-amber-500/40">
              <ArrowLeftRight className="size-3.5" />
              Shift All
            </Button>
          </div>
        )}
      </div>

      {!hasMultipleGroups && renderTable(rowGroups[0]?.[1] || [])}

      {hasMultipleGroups && rowGroups.map(([key, rows]) => (
        <div className="mb-8 rounded-xl border border-border bg-muted/20 p-4" key={key || '(primary)'}>
          <div className="mb-3 flex flex-wrap items-center justify-between gap-3">
            <h3 className="font-semibold">{key || primaryRaceSelection.raceName || 'Primary race'}</h3>
            {key === primaryKey ? (
              <span className="text-sm text-success">
                &rarr; {primaryRaceSelection.raceName} (Step 1 selection)
              </span>
            ) : (
              <GroupRaceSelector
                groupKey={key}
                primaryRaceSelection={primaryRaceSelection}
                resolved={groupRaces[key] || null}
                onResolve={(resolved) =>
                  setGroupRaces(prev => {
                    const next = { ...prev };
                    if (resolved) next[key] = resolved;
                    else delete next[key];
                    return next;
                  })
                }
              />
            )}
          </div>
          {renderTable(rows)}
        </div>
      ))}

      <div className="mt-8 flex flex-col-reverse gap-3 border-t border-border pt-6 sm:flex-row sm:justify-end">
        <Button type="button" variant="outline" onClick={onBack}>&larr; Back</Button>
        <Button type="button" variant="outline" onClick={onCancel}>Cancel</Button>
        <Button type="button" onClick={handleSubmit}>Next &rarr;</Button>
      </div>

      <AlertDialog open={!!deleteTarget} onOpenChange={(open) => !open && setDeleteTarget(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Remove this row?</AlertDialogTitle>
            <AlertDialogDescription>
              Remove {deleteTarget?.name || 'this'}&rsquo;s row from this upload? It won&rsquo;t be saved. This can&rsquo;t be undone.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction variant="destructive" onClick={confirmDeleteRow}>Remove</AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
