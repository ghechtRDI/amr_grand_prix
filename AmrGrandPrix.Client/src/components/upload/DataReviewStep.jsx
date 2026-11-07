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
import { AlertTriangle, ArrowLeftRight, CheckCircle2, CircleHelp, Loader2, Save, Sparkles, Trophy, UserCheck, UserPlus, UserSearch, Trash2 } from 'lucide-react';
import * as raceService from '../../services/raceService';
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
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { cn } from '@/lib/utils';
import { AGE_CATEGORIES } from '@/lib/ageCategories';

const columnHelper = createColumnHelper();

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

// Seconds for a time string, read the way the backend's ParseTime reads it (a bare 2-field
// time is H:MM unless the first field can't be hours), or null if it isn't a time.
function timeToSeconds(value) {
  const parts = String(value ?? '').trim().split(':');
  if (parts.length < 2 || parts.length > 3 || parts.some(p => !/^\d+(\.\d+)?$/.test(p))) return null;
  const n = parts.map(Number);
  if (n.length === 3) return n[0] * 3600 + n[1] * 60 + n[2];
  return n[0] < 24 ? n[0] * 3600 + n[1] * 60 : n[0] * 60 + n[1];
}

// Data indices of the rows that may be marked as a course record: each gender's winner
// (fastest Finished time) within each course-variant group. Mirrors the backend's
// CourseRecordRules, which has the final say (it also sees results already in the race).
function courseRecordEligibleIndices(rows) {
  const best = new Map(); // "group|gender" -> { index, seconds, place }
  rows.forEach((row, index) => {
    if (row.status !== 'Finished' || !row.gender) return;
    const seconds = timeToSeconds(row.time);
    if (!seconds) return;
    const place = row.place === '' || row.place == null ? Infinity : Number(row.place);
    const key = `${(row.courseVariant || '').trim()}|${row.gender}`;
    const current = best.get(key);
    if (!current || seconds < current.seconds || (seconds === current.seconds && place < current.place)) {
      best.set(key, { index, seconds, place });
    }
  });
  return new Set(Array.from(best.values(), b => b.index));
}

function CourseRecordCell({ row, table }) {
  const { recordEligible, updateData } = table.options.meta;
  if (!recordEligible.has(row.index)) return null;
  const checked = !!row.original.isCourseRecord;
  return (
    <label
      className={cn(
        'mx-auto flex w-fit cursor-pointer items-center gap-1 text-xs',
        checked ? 'font-medium text-amber-600 dark:text-amber-400' : 'text-muted-foreground'
      )}
      title={`${row.original.gender} winner — mark as a new course record (+10 Open Division bonus in a Grand Prix race)`}
    >
      <input
        type="checkbox"
        className="size-3.5 rounded border-input accent-primary"
        checked={checked}
        onChange={(e) => updateData(row.index, 'isCourseRecord', e.target.checked)}
      />
      <Trophy className="size-3.5" />
    </label>
  );
}

// Version of the draft document saved via PUT /api/results/batch/{id}/draft. Bump (and handle
// the old shape on load) if the row shape below changes incompatibly.
const DRAFT_VERSION = 1;

function selectedMatchOf(row) {
  return (row.runnerMatches || []).find(m => m.runnerId === row.matchedRunnerId) || null;
}

function uploadedAgeOf(row) {
  return row.age === '' || row.age == null ? null : Number(row.age);
}

// Required fields mirror handleSubmit's check (and the backend's ValidateRow()).
function isMissingRequired(row) {
  if (!row.name) return true;
  if (!row.age && !row.ageCategory) return true;
  return row.status === 'Finished' && (!row.place || !row.time);
}

// True when a row still needs the admin's attention - what the "Issues only" view shows:
// a validation warning, a missing required field, a likely misread time, an unconfirmed
// partial runner match, or an unresolved age disagreement with the matched runner.
function rowHasIssue(row) {
  if (row.validationIssues?.length > 0) return true;
  if (isMissingRequired(row)) return true;
  if (looksShifted(row.time)) return true;
  if (row.matchStatus === 'needs-confirmation') return true;
  const match = selectedMatchOf(row);
  if (row.matchStatus === 'auto-matched' && match && match.confidence < 1) return true;
  const uploadedAge = uploadedAgeOf(row);
  return !!match && uploadedAge != null && match.age != null && uploadedAge !== match.age && !row.updateRunnerAge;
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

  const selectedMatch = selectedMatchOf(row.original);
  const uploadedAge = uploadedAgeOf(row.original);
  const hasAgeDiscrepancy =
    !!selectedMatch && uploadedAge != null && selectedMatch.age != null && uploadedAge !== selectedMatch.age;
  // The runner has no age on file at all: the backend records the uploaded age on save.
  const willSetMissingAge = !!selectedMatch && uploadedAge != null && selectedMatch.age == null;

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
      {willSetMissingAge && (
        <span
          className="text-xs text-muted-foreground"
          title="This runner has no age on file, so the uploaded age will be saved to their record"
        >
          No age on file &mdash; will be set to {uploadedAge}
        </span>
      )}
    </div>
  );
}

// Race resolver shown for each course-variant group when an upload covers several variants.
// The group's label is matched to one of the series' variants (by name or alias); if that
// variant already has a race in the Step 1 race's year it's auto-selected, otherwise the admin
// can create it in one click. An unrecognized label can be mapped to an existing variant or
// saved as a new one. For a multi-variant upload a missing race isn't created here: the group
// resolves to a pending race ({ raceId: null, pendingRace }) that Confirmation creates on save,
// so only variants that end up with results get a race; the dropdown is limited to the variants
// ticked in Step 1.
const NEW_VARIANT = '__new_variant__';

function GroupRaceSelector({ groupKey, primaryRaceSelection, resolved, onResolve }) {
  const raceSeriesId = primaryRaceSelection.raceSeriesId;
  const year = raceService.yearOf(primaryRaceSelection.raceDate);
  const includedVariantIds = primaryRaceSelection.includedVariantIds || [];
  const deferCreate = includedVariantIds.length > 0;

  const [series, setSeries] = useState(null);
  const [loading, setLoading] = useState(true);
  const [variantChoice, setVariantChoice] = useState('');
  const [newVariantName, setNewVariantName] = useState(groupKey);
  const [date, setDate] = useState(primaryRaceSelection.raceDate || new Date().toISOString().split('T')[0]);
  const [isGrandPrixRace, setIsGrandPrixRace] = useState(false);
  const [autoMatched, setAutoMatched] = useState(false);
  const [creating, setCreating] = useState(false);
  const [error, setError] = useState(null);
  // The choice this group already had when mounted - from a resumed draft, or from coming
  // Back from Confirmation - restored instead of re-guessing the variant from the label.
  const [initialResolved] = useState(resolved);

  const variantById = (id) => series?.variants.find(v => v.raceVariantId === id);
  // A multi-variant upload's sections can only go to the variants ticked in Step 1.
  const selectableVariants = (s) =>
    s.variants.filter(v => !deferCreate || includedVariantIds.includes(v.raceVariantId));
  const raceFor = (s, variantId) => s?.races.find(r => r.raceVariantId === variantId && r.year === year);

  const resolveTo = (s, variant, race) => onResolve({
    raceId: race.raceId,
    raceName: raceService.raceLabel(s, variant),
    isGrandPrixRace: !!race.isGrandPrixRace,
  });

  const resolvePending = (s, variant, raceDate, gp) => onResolve({
    raceId: null,
    pendingRace: { raceVariantId: variant.raceVariantId, date: raceDate, isGrandPrixRace: gp },
    raceName: raceService.raceLabel(s, variant),
    isGrandPrixRace: gp,
  });

  const chooseVariant = (s, value) => {
    setVariantChoice(value);
    const variant = s.variants.find(v => v.raceVariantId === value);
    // Step 1's per-variant Grand Prix choices (multi-variant upload) win over the variant default.
    const gp = primaryRaceSelection.variantGrandPrix?.[value] ?? !!variant?.isGrandPrixByDefault;
    setIsGrandPrixRace(gp);
    const race = variant && raceFor(s, variant.raceVariantId);
    if (race) resolveTo(s, variant, race);
    else if (variant && deferCreate) resolvePending(s, variant, date, gp);
    else onResolve(null);
  };

  const pendingVariant = deferCreate && !raceFor(series, variantChoice) ? variantById(variantChoice) : null;

  const handleDateChange = (value) => {
    setDate(value);
    if (pendingVariant) resolvePending(series, pendingVariant, value, isGrandPrixRace);
  };

  const handleGrandPrixChange = (value) => {
    setIsGrandPrixRace(value);
    if (pendingVariant) resolvePending(series, pendingVariant, date, value);
  };

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        setLoading(true);
        const s = await raceService.getSeriesDetail(raceSeriesId);
        if (cancelled) return;
        setSeries(s);

        const saved = initialResolved;
        const savedVariantId = saved?.pendingRace?.raceVariantId
          ?? (saved?.raceId ? s.races.find(r => r.raceId === saved.raceId)?.raceVariantId : null);
        const savedVariant = savedVariantId
          ? selectableVariants(s).find(v => v.raceVariantId === savedVariantId)
          : null;
        if (savedVariant) {
          const race = raceFor(s, savedVariant.raceVariantId);
          setVariantChoice(savedVariant.raceVariantId);
          if (race) {
            resolveTo(s, savedVariant, race);
          } else if (saved.pendingRace && deferCreate) {
            setDate(saved.pendingRace.date);
            setIsGrandPrixRace(!!saved.pendingRace.isGrandPrixRace);
            onResolve(saved);
          } else {
            chooseVariant(s, savedVariant.raceVariantId);
          }
          return;
        }

        // Untagged rows belong to the Step 1 race's variant.
        const variant = groupKey
          ? raceService.findVariant(selectableVariants(s), groupKey)
          : s.variants.find(v => v.raceVariantId === primaryRaceSelection.raceVariantId);
        if (!variant) {
          setVariantChoice(NEW_VARIANT);
          return;
        }
        chooseVariant(s, variant.raceVariantId);
        setAutoMatched(!!raceFor(s, variant.raceVariantId));
      } catch (err) {
        if (!cancelled) setError(err.message);
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => { cancelled = true; };
    // Runs once per group when its series context is known; onResolve is intentionally
    // excluded (a fresh function identity every parent render would otherwise re-fire this).
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [raceSeriesId, groupKey]);

  const handleCreate = async () => {
    try {
      setCreating(true);
      setError(null);
      let s = series;
      let variant = variantById(variantChoice);
      if (variantChoice === NEW_VARIANT) {
        variant = await raceService.createVariant(raceSeriesId, {
          name: newVariantName.trim(),
          isGrandPrixByDefault: isGrandPrixRace,
        });
        s = { ...s, variants: [...s.variants, variant] };
      }
      const race = await raceService.createRace({ raceVariantId: variant.raceVariantId, date, isGrandPrixRace });
      s = { ...s, races: [...s.races, { ...race, resultsCount: 0 }] };
      setSeries(s);
      setVariantChoice(variant.raceVariantId);
      resolveTo(s, variant, race);
    } catch (err) {
      setError(err.message);
    } finally {
      setCreating(false);
    }
  };

  // base-ui's Select doesn't resolve a selected item's label from its children the
  // way Radix does - it needs an explicit lookup to render anything but the raw value.
  const variantOptionLabel = (value) => {
    if (!value) return null;
    if (value === NEW_VARIANT) return `+ New variant`;
    return variantById(value)?.name ?? value;
  };

  const existingRace = variantChoice && variantChoice !== NEW_VARIANT ? raceFor(series, variantChoice) : null;
  const needsCreate = !loading && variantChoice && !existingRace && !resolved;
  const isPending = !loading && !!resolved?.pendingRace;

  return (
    <div className="flex flex-wrap items-center gap-2">
      <Label className="text-sm font-normal text-muted-foreground">
        Variant for &ldquo;{groupKey || 'untagged rows'}&rdquo;:
      </Label>
      {loading ? (
        <Skeleton className="h-8 w-56" />
      ) : series && (
        <Select
          value={variantChoice || undefined}
          onValueChange={(value) => { setAutoMatched(false); chooseVariant(series, value); }}
        >
          <SelectTrigger className="min-w-56">
            <SelectValue placeholder="-- Select a variant --">
              {(value) => variantOptionLabel(value) || '-- Select a variant --'}
            </SelectValue>
          </SelectTrigger>
          <SelectContent>
            {selectableVariants(series).map(v => {
              const race = raceFor(series, v.raceVariantId);
              return (
                <SelectItem key={v.raceVariantId} value={v.raceVariantId}>
                  {v.name}{race ? ` (${year}: ${race.resultsCount} results)` : ` (no ${year} race yet)`}
                </SelectItem>
              );
            })}
            <SelectItem value={NEW_VARIANT}>+ New variant</SelectItem>
          </SelectContent>
        </Select>
      )}
      {autoMatched && resolved && (
        <span className="text-xs text-success">Matched {year} race</span>
      )}
      {resolved && !autoMatched && existingRace && (
        <span className="text-xs text-muted-foreground">&rarr; {existingRace.date}</span>
      )}
      {isPending && (
        <span className="text-xs text-muted-foreground">New race &mdash; created when saved</span>
      )}
      {(needsCreate || isPending) && (
        <div className="mt-2 flex w-full flex-wrap items-center gap-2">
          {variantChoice === NEW_VARIANT && (
            <Input
              type="text"
              value={newVariantName}
              onChange={(e) => setNewVariantName(e.target.value)}
              placeholder="Variant name"
              className="w-56"
            />
          )}
          <Input
            type="date"
            value={date}
            onChange={(e) => handleDateChange(e.target.value)}
            className="w-40"
          />
          <label className="flex items-center gap-1.5 text-sm text-muted-foreground">
            <input
              type="checkbox"
              className="size-4 rounded border-input accent-primary"
              checked={isGrandPrixRace}
              onChange={(e) => handleGrandPrixChange(e.target.checked)}
            />
            Grand Prix race
          </label>
          {needsCreate && (
            <Button
              type="button"
              variant="secondary"
              size="sm"
              onClick={handleCreate}
              disabled={creating || (variantChoice === NEW_VARIANT && !newVariantName.trim())}
            >
              {creating ? 'Creating…' : `Create ${raceService.yearOf(date)} race`}
            </Button>
          )}
        </div>
      )}
      {error && <span className="text-sm text-destructive">{error}</span>}
    </div>
  );
}

// Per-section switch between every row and just the rows that still need attention.
function IssueFilterToggle({ issuesOnly, onChange, total, issueCount }) {
  const option = (active, label, onClick) => (
    <button
      type="button"
      aria-pressed={active}
      onClick={onClick}
      className={cn(
        'rounded-md px-3 py-1 text-sm font-medium transition-colors',
        active ? 'bg-primary text-primary-foreground shadow-sm' : 'text-muted-foreground hover:text-foreground'
      )}
    >
      {label}
    </button>
  );
  return (
    <div role="group" aria-label="Rows to show" className="inline-flex rounded-lg border border-border bg-background p-0.5">
      {option(!issuesOnly, `All rows (${total})`, () => onChange(false))}
      {option(issuesOnly, `Issues only (${issueCount})`, () => onChange(true))}
    </div>
  );
}

function HelpSection({ title, children }) {
  return (
    <section className="flex flex-col gap-1.5">
      <h3 className="text-sm font-semibold text-foreground">{title}</h3>
      <div className="flex flex-col gap-1.5 text-sm text-muted-foreground">{children}</div>
    </section>
  );
}

function RowSwatch({ className, children }) {
  return (
    <li className="flex items-center gap-2">
      <span className={cn('inline-block size-3.5 shrink-0 rounded-sm border border-border', className)} />
      <span>{children}</span>
    </li>
  );
}

function DataReviewHelpDialog({ open, onOpenChange }) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[85svh] overflow-y-auto sm:max-w-2xl">
        <DialogHeader>
          <DialogTitle>How to review uploaded results</DialogTitle>
          <DialogDescription>
            Every row below was read from the uploaded file automatically. Check it against the
            original before saving &mdash; nothing is written to the race or the Grand Prix
            standings until you click Save Results on the Confirmation step.
          </DialogDescription>
        </DialogHeader>

        <div className="flex flex-col gap-5">
          <HelpSection title="1. Check each section's race">
            <p>
              When the file has more than one course (e.g. Adult and Junior), rows are grouped into
              sections. Make sure each section is assigned to the right variant. If the file
              repeated a section by mistake, use <strong>Remove section</strong>.
            </p>
          </HelpSection>

          <HelpSection title="2. Focus on the rows with issues">
            <p>
              Each section has an <strong>All rows / Issues only</strong> toggle. Switch to
              <strong> Issues only</strong> to see just the rows that still need attention. A row
              leaves that view as soon as you fix it. Switch back to <strong>All rows</strong> for a
              final look before moving on.
            </p>
            <p>Row colors:</p>
            <ul className="flex flex-col gap-1">
              <RowSwatch className="bg-amber-500/25">Amber: a validation warning (hover the warning icon in the Validation column for details)</RowSwatch>
              <RowSwatch className="bg-purple-500/25">Purple: possible runner matches to confirm</RowSwatch>
              <RowSwatch className="bg-sky-500/25">Blue: will be created as a new runner</RowSwatch>
            </ul>
          </HelpSection>

          <HelpSection title="3. Fix misread times">
            <p>
              Times are shown as <strong>hh:mm:ss</strong>. A 34-minute finish sometimes gets read
              as <code>34:21</code>, meaning 34 <em>hours</em>. These rows show an amber
              <ArrowLeftRight className="mx-1 inline size-3.5 text-amber-600 dark:text-amber-400" />
              button next to the time. Click it to change the value to <code>0:34:21</code>, or use
              <strong> Shift All</strong> above the table to fix them all at once.
            </p>
          </HelpSection>

          <HelpSection title="4. Confirm runner matches">
            <ul className="flex list-disc flex-col gap-1 pl-5">
              <li><strong>Auto-matched</strong>: a strong match was picked for you. Check it, especially if it&rsquo;s under 100%.</li>
              <li><strong>Confirm match</strong>: one or more possible matches were found, but none was picked. Choose the right runner, or <strong>+ New Runner</strong> if none of them is this person.</li>
              <li><strong>New runner</strong>: no existing runner looked similar, so a new record will be created.</li>
            </ul>
            <p>Each option in the dropdown shows the runner&rsquo;s age on file and the match confidence.</p>
          </HelpSection>

          <HelpSection title="5. Resolve age disagreements">
            <p>
              If the uploaded age doesn&rsquo;t match the selected runner&rsquo;s estimated age, tick
              <strong> Update estimated age</strong> when the uploaded age is right. A runner who
              entered their own date of birth can&rsquo;t be changed here. A runner with no age on file
              gets the uploaded age automatically.
            </p>
          </HelpSection>

          <HelpSection title="6. Edit or remove rows">
            <p>
              Click any cell to edit it. Press <kbd className="rounded border border-border px-1 text-xs">Enter</kbd> to
              keep the change or <kbd className="rounded border border-border px-1 text-xs">Esc</kbd> to undo it.
              Use the trash icon to remove a row that shouldn&rsquo;t be saved.
            </p>
            <p>
              Every row needs a <strong>Name</strong> and an <strong>Age</strong> or
              <strong> Age Category</strong>. Finished rows also need a <strong>Place</strong> and
              <strong> Time</strong>. Rows without a <strong>Gender</strong> are skipped when saving.
            </p>
          </HelpSection>

          <HelpSection title="7. Mark course records">
            <p>
              Each gender&rsquo;s winner in each section (the fastest Finished time) gets a checkbox in
              the <strong>CR</strong> column. Tick it if that result set a new course record. In a Grand
              Prix race it earns a +10 Open Division bonus.
            </p>
          </HelpSection>

          <HelpSection title="8. Save a draft or continue">
            <p>
              <strong>Save Draft</strong> stores your edits without saving any results or recalculating
              the Grand Prix. To pick up where you left off later, click <strong>Resume</strong> on
              this upload in Results Management. When you&rsquo;re done, click <strong>Next</strong> to
              go to Confirmation.
            </p>
          </HelpSection>
        </div>

        <DialogFooter>
          <Button type="button" onClick={() => onOpenChange(false)}>Got it</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

const COLUMNS = [
  columnHelper.accessor('place',  { header: 'Place',  cell: EditableCell, size: 80 }),
  columnHelper.accessor('bib',    { header: 'Bib',    cell: EditableCell, size: 80 }),
  columnHelper.accessor('name',   { header: 'Name',   cell: EditableCell, size: 200 }),
  columnHelper.accessor('age',    { header: 'Age',    cell: EditableCell, size: 80 }),
  columnHelper.accessor('ageCategory', { header: 'Age Category', cell: EditableCell, size: 120 }),
  columnHelper.accessor('gender', { header: 'Gender', cell: EditableCell, size: 100 }),
  columnHelper.accessor('time', {
    // The unit makes a misread obvious: "34:21:00" reads as 34 hours, not 34 minutes.
    header: () => (
      <>Time <span className="font-normal normal-case tracking-normal text-zinc-400">(hh:mm:ss)</span></>
    ),
    cell: TimeCell,
    size: 140,
  }),
  columnHelper.accessor('status', { header: 'Status', cell: EditableCell, size: 100 }),
  columnHelper.display({
    id: 'courseRecord',
    header: () => <span title="Course record — available on each gender's winner">CR</span>,
    cell: CourseRecordCell,
    size: 60,
  }),
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
  // A draft saved earlier (handed over by Resume) is restored only on a fresh entry to this step;
  // coming Back from Confirmation restores this session's own edits (reviewedData) instead.
  const savedDraft = wizardData.reviewDraft?.version === DRAFT_VERSION ? wizardData.reviewDraft : null;

  const [data, setData] = useState(() => {
    if (wizardData.reviewedData) return wizardData.reviewedData;
    if (Array.isArray(savedDraft?.rows)) {
      // Drafts saved before "no runner match" stopped being a validation issue still carry it.
      return savedDraft.rows.map(row => ({
        ...row,
        validationIssues: (row.validationIssues || []).filter(i => i.field?.toLowerCase() !== 'runner'),
      }));
    }

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
      isCourseRecord: false,
    }));
  });

  const [editingCell, setEditingCell] = useState(null);
  // Course-variant group key -> { raceId, raceName } chosen via GroupRaceSelector.
  // The "primary" group (pre-selected race from Step 1) never needs an entry here.
  const [groupRaces, setGroupRaces] = useState(wizardData.groupRaces || savedDraft?.groupRaces || {});
  // Group key -> true when that section shows only rows with issues. Default: all rows.
  const [issuesOnly, setIssuesOnly] = useState({});
  const [helpOpen, setHelpOpen] = useState(false);
  const [draftState, setDraftState] = useState({
    saving: false,
    savedAt: wizardData.reviewDraftSavedAt || null,
    error: null,
  });

  const saveDraft = async () => {
    setDraftState(prev => ({ ...prev, saving: true, error: null }));
    try {
      const res = await fetch(`/api/results/batch/${wizardData.uploadBatchId}/draft`, {
        method: 'PUT',
        headers: {
          Authorization: `Bearer ${tokenService.getAccessToken()}`,
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({ version: DRAFT_VERSION, rows: data, groupRaces }),
      });
      if (!res.ok) throw new Error((await res.text()) || `HTTP ${res.status}`);
      const { savedAt } = await res.json();
      setDraftState({ saving: false, savedAt, error: null });
    } catch (err) {
      setDraftState(prev => ({ ...prev, saving: false, error: err.message }));
    }
  };

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
  // A whole course-variant section to drop, e.g. one the extractor duplicated: { key, count }.
  const [sectionDeleteTarget, setSectionDeleteTarget] = useState(null);

  const confirmDeleteSection = () => {
    const { key } = sectionDeleteTarget;
    setData(old => old.filter(row => (row.courseVariant || '').trim() !== key));
    setGroupRaces(prev => {
      const next = { ...prev };
      delete next[key];
      return next;
    });
    setSectionDeleteTarget(null);
  };

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

  const recordEligible = useMemo(() => courseRecordEligibleIndices(data), [data]);

  const table = useReactTable({
    data,
    columns: COLUMNS,
    getCoreRowModel: getCoreRowModel(),
    meta: { editingCell, setEditingCell, updateData, updateRunnerMatch, requestDeleteRow, recordEligible },
  });

  const stats = useMemo(() => ({
    totalResults: data.length,
    warnings: data.filter(row => row.validationIssues?.length > 0).length,
    newRunners: data.filter(row => row.matchStatus === 'new-runner').length,
    needsReview: data.filter(row => row.matchStatus === 'needs-confirmation').length,
    shiftableTimes: data.filter(row => looksShifted(row.time)).length,
  }), [data]);

  // Rows for one section, narrowed to the ones with issues when its toggle is on.
  const visibleRows = (key, rows) => (issuesOnly[key] ? rows.filter(r => rowHasIssue(r.original)) : rows);
  const issueCountOf = (rows) => rows.filter(r => rowHasIssue(r.original)).length;
  const filterToggle = (key, rows) => (
    <IssueFilterToggle
      issuesOnly={!!issuesOnly[key]}
      onChange={(value) => setIssuesOnly(prev => ({ ...prev, [key]: value }))}
      total={rows.length}
      issueCount={issueCountOf(rows)}
    />
  );

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
  // A multi-variant upload has no Step 1 race, so it's resolved per group even if only one
  // variant came back.
  const includedVariantNames = primaryRaceSelection.includedVariantIds?.length
    ? primaryRaceSelection.knownVariants || []
    : null;
  const showGroups = hasMultipleGroups || !!includedVariantNames;
  const primaryKey = showGroups ? null : (rowGroups[0]?.[0] ?? '');

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

  const renderTable = (key, allRows) => {
    const rows = visibleRows(key, allRows);
    if (rows.length === 0 && issuesOnly[key]) {
      return (
        <div className="flex items-center justify-center gap-2 rounded-xl border border-dashed border-border px-4 py-8 text-sm text-muted-foreground">
          <CheckCircle2 className="size-4 text-success" />
          No rows in this section have issues. Switch to All rows to see everything.
        </div>
      );
    }
    return (
      <div className="overflow-hidden rounded-xl border border-border">
        <div className="max-h-[32rem] overflow-y-auto">
          <Table>
            {tableHead}
            {renderTableBody(rows)}
          </Table>
        </div>
      </div>
    );
  };

  const handleSubmit = () => {
    // Place/Time are only required for Finished results - DNF/DNS/DQ rows legitimately
    // have neither, mirroring the backend's ValidateRow().
    const invalidRows = data.filter(isMissingRequired);

    if (invalidRows.length > 0) {
      alert(
        `${invalidRows.length} row(s) are missing required fields (Name, Age or Age Category, and — for Finished results — Place and Time). Please fix these issues before continuing.`
      );
      return;
    }

    const unresolvedGroups = rowGroups
      .map(([key]) => key)
      .filter(key => key !== primaryKey && !groupRaces[key]?.raceId && !groupRaces[key]?.pendingRace);

    if (unresolvedGroups.length > 0) {
      alert(
        `Please select or create a race for the following detected course variant(s): ${unresolvedGroups.join(', ')}`
      );
      return;
    }

    // A row marked as a course record and then edited out of first place (or into another
    // variant) loses the mark - only a gender winner can hold one.
    const reviewedData = data.map((row, i) =>
      row.isCourseRecord && !recordEligible.has(i) ? { ...row, isCourseRecord: false } : row
    );
    onNext({ reviewedData, groupRaces, primaryGroupKey: primaryKey });
  };

  return (
    <div>
      <div className="mb-6 flex items-center justify-between border-b border-border pb-4">
        <h2 className="text-xl font-semibold">Step 3: Data Review</h2>
        <div className="flex items-center gap-3">
          <Button
            type="button"
            variant="outline"
            size="sm"
            title="Click here to view instructions"
            onClick={() => setHelpOpen(true)}
          >
            <CircleHelp className="size-4" />
            Help
          </Button>
          <span className="text-sm text-muted-foreground">Step 3 of {wizardData.totalSteps || 4}</span>
        </div>
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

      {!showGroups && (
        <>
          <div className="mb-3 flex justify-end">{filterToggle(primaryKey ?? '', rowGroups[0]?.[1] || [])}</div>
          {renderTable(primaryKey ?? '', rowGroups[0]?.[1] || [])}
        </>
      )}

      {showGroups && rowGroups.map(([key, rows]) => (
        <div className="mb-8 rounded-xl border border-border bg-muted/20 p-4" key={key || '(primary)'}>
          <div className="mb-3 flex flex-wrap items-center justify-between gap-3">
            <div className="flex flex-wrap items-center gap-2">
              <h3 className="font-semibold">{key || primaryRaceSelection.raceName || 'Primary race'}</h3>
              <span className="text-sm text-muted-foreground">
                ({rows.length} result{rows.length === 1 ? '' : 's'})
              </span>
              {includedVariantNames && !includedVariantNames.some(n => n.toLowerCase() === key.toLowerCase()) && (
                <Badge variant="outline" className="border-amber-500/50 text-amber-600 dark:text-amber-400">
                  <AlertTriangle className="size-3" /> Not one of the selected variants
                </Badge>
              )}
              <Button
                type="button"
                variant="ghost"
                size="sm"
                className="text-muted-foreground hover:text-destructive"
                onClick={() => setSectionDeleteTarget({ key, count: rows.length })}
              >
                <Trash2 className="size-3.5" /> Remove section
              </Button>
            </div>
            {filterToggle(key, rows)}
          </div>
          <div className="mb-3">
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
          {renderTable(key, rows)}
        </div>
      ))}

      <div className="mt-8 flex flex-col gap-3 border-t border-border pt-6 sm:flex-row sm:items-center sm:justify-between">
        <div className="text-sm" aria-live="polite">
          {draftState.error ? (
            <span className="text-destructive">Couldn&rsquo;t save draft: {draftState.error}</span>
          ) : draftState.savedAt ? (
            <span className="text-muted-foreground">
              Draft saved {new Date(draftState.savedAt).toLocaleString()} &mdash; resume it from Results Management
            </span>
          ) : null}
        </div>
        <div className="flex flex-col-reverse gap-3 sm:flex-row">
          <Button type="button" variant="outline" onClick={onBack}>&larr; Back</Button>
          <Button type="button" variant="outline" onClick={onCancel}>Cancel</Button>
          <Button
            type="button"
            variant="secondary"
            onClick={saveDraft}
            disabled={draftState.saving || !wizardData.uploadBatchId}
            title="Save your review edits without saving results or recalculating the Grand Prix"
          >
            {draftState.saving ? <Loader2 className="size-4 animate-spin" /> : <Save className="size-4" />}
            {draftState.saving ? 'Saving…' : 'Save Draft'}
          </Button>
          <Button type="button" onClick={handleSubmit}>Next &rarr;</Button>
        </div>
      </div>

      <DataReviewHelpDialog open={helpOpen} onOpenChange={setHelpOpen} />

      <AlertDialog open={!!sectionDeleteTarget} onOpenChange={(open) => !open && setSectionDeleteTarget(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Remove this whole section?</AlertDialogTitle>
            <AlertDialogDescription>
              Remove all {sectionDeleteTarget?.count} result{sectionDeleteTarget?.count === 1 ? '' : 's'} in
              &ldquo;{sectionDeleteTarget?.key || 'untagged rows'}&rdquo; from this upload? Use this when the
              extractor duplicated results under the wrong variant. They won&rsquo;t be saved, and this
              can&rsquo;t be undone.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction variant="destructive" onClick={confirmDeleteSection}>Remove section</AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

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
