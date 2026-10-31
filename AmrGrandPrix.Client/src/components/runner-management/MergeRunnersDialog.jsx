/**
 * Confirmation modal for merging two runner records. The admin picks which record to keep, sees
 * a server-computed preview of exactly what will change (results moved, resulting names, Grand
 * Prix years recalculated, warnings and blocking issues), and must tick a confirmation box before
 * the merge button enables. Opened with one runner, it first asks which runner to merge with.
 */

import { useEffect, useState } from 'react';
import { AlertCircle, AlertTriangle, ArrowRight, Loader2 } from 'lucide-react';
import * as runnerService from '../../services/runnerService';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { SearchInput } from '@/components/ui/search-input';
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
import { AlternateNameChips, RunnerSummary } from './shared';
import { formatResults } from './format';

function RunnerPicker({ exclude, onPick }) {
  const [search, setSearch] = useState('');
  const [results, setResults] = useState([]);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    if (search.trim().length < 2) {
      setResults([]);
      return;
    }
    let cancelled = false;
    setLoading(true);
    runnerService
      .getRunnersForManagement(search, 10)
      .then((r) => !cancelled && setResults(r.filter((x) => x.runnerId !== exclude.runnerId)))
      .catch(() => !cancelled && setResults([]))
      .finally(() => !cancelled && setLoading(false));
    return () => {
      cancelled = true;
    };
  }, [search, exclude.runnerId]);

  return (
    <div className="flex flex-col gap-2">
      <p className="text-sm">
        Which runner is the same person as <span className="font-medium">{exclude.fullName}</span>?
      </p>
      <SearchInput value={search} onChange={setSearch} placeholder="Search by name…" />
      {loading && <Skeleton className="h-10 w-full" />}
      {!loading && search.trim().length >= 2 && results.length === 0 && (
        <p className="text-sm text-muted-foreground">No other runners match.</p>
      )}
      <div className="flex max-h-64 flex-col gap-1 overflow-y-auto">
        {results.map((r) => (
          <button
            key={r.runnerId}
            type="button"
            onClick={() => onPick(r)}
            className="flex items-center justify-between gap-3 rounded-md border border-border px-3 py-2 text-left text-sm hover:bg-muted"
          >
            <span className="font-medium">{r.fullName}</span>
            <span className="text-muted-foreground">{r.gender} · {formatResults(r)}</span>
          </button>
        ))}
      </div>
    </div>
  );
}

function KeepOption({ runner, selected, onSelect }) {
  return (
    <label
      className={cn(
        'flex cursor-pointer gap-3 rounded-lg border p-3 transition-colors',
        selected ? 'border-primary bg-primary/5 ring-1 ring-primary' : 'border-border hover:bg-muted/50'
      )}
    >
      <input
        type="radio"
        name="keep-runner"
        checked={selected}
        onChange={onSelect}
        className="mt-1 accent-primary"
      />
      <div className="flex min-w-0 flex-col gap-2">
        <Badge variant={selected ? 'default' : 'outline'}>{selected ? 'Keep this record' : 'Merge away'}</Badge>
        <RunnerSummary runner={runner} />
      </div>
    </label>
  );
}

function MergePreview({ preview }) {
  const { target, source, merged } = preview;
  const years = preview.grandPrixYearsToRecalculate;

  return (
    <div className="flex flex-col gap-3 text-sm">
      {preview.blockingIssues.length > 0 && (
        <Alert variant="destructive">
          <AlertCircle />
          <AlertDescription>
            <p className="font-medium">This merge can&rsquo;t be done:</p>
            <ul className="mt-1 list-disc pl-4">
              {preview.blockingIssues.map((i) => <li key={i}>{i}</li>)}
            </ul>
          </AlertDescription>
        </Alert>
      )}

      {preview.warnings.length > 0 && (
        <div className="flex gap-2 rounded-lg border border-amber-500/30 bg-amber-500/10 px-3 py-2">
          <AlertTriangle className="mt-0.5 size-4 shrink-0 text-amber-600 dark:text-amber-400" aria-hidden="true" />
          <ul className="list-disc pl-4">
            {preview.warnings.map((w) => <li key={w}>{w}</li>)}
          </ul>
        </div>
      )}

      <div className="rounded-lg border border-border p-3">
        <p className="mb-2 font-medium">What this will do</p>
        <ul className="flex list-disc flex-col gap-1 pl-4">
          <li>
            Move <span className="font-medium">{preview.resultsToMove} result{preview.resultsToMove === 1 ? '' : 's'}</span>{' '}
            from <span className="font-medium">{source.fullName}</span> to{' '}
            <span className="font-medium">{target.fullName}</span>, for {preview.resultsToMove + target.resultCount} in total.
          </li>
          <li>
            Delete the <span className="font-medium">{source.fullName}</span> record. Links to its runner page will stop working.
          </li>
          <li>
            {years.length > 0 ? (
              <>Recalculate Grand Prix standings for <span className="font-medium">{years.join(', ')}</span>, combining both records&rsquo; points.</>
            ) : (
              <>No Grand Prix standings change — {source.fullName} has no Grand Prix points.</>
            )}
          </li>
          {preview.movesAccountLink && (
            <li>
              Re-link the user account <span className="font-medium">{source.linkedAccountEmail}</span> to the kept record.
            </li>
          )}
        </ul>
      </div>

      <div className="rounded-lg border border-border p-3">
        <p className="mb-2 font-medium">Combined record</p>
        <dl className="grid grid-cols-[auto_1fr] gap-x-3 gap-y-1 text-muted-foreground">
          <dt>Name</dt>
          <dd className="text-foreground">{merged.firstName} {merged.lastName}</dd>
          <dt>Alternate names</dt>
          <dd><AlternateNameChips names={merged.alternateNames} /></dd>
          {merged.preferredName && (
            <>
              <dt>Preferred</dt>
              <dd className="text-foreground">{merged.preferredName}</dd>
            </>
          )}
          <dt>Gender</dt>
          <dd className="text-foreground">{merged.gender}</dd>
          <dt>Born</dt>
          <dd className="text-foreground">
            {merged.dateOfBirth
              ? formatDateOnly(merged.dateOfBirth, { year: 'numeric', month: 'short', day: 'numeric' })
              : merged.estimatedBirthYear
                ? `~${merged.estimatedBirthYear} (est.)`
                : '—'}
          </dd>
          {merged.email && (
            <>
              <dt>Email</dt>
              <dd className="text-foreground">{merged.email}</dd>
            </>
          )}
        </dl>
      </div>
    </div>
  );
}

/**
 * @param runners  [runner] or [runnerA, runnerB] (RunnerAdminDto); the first is kept by default.
 * @param onMerged called with the RunnerMergeResultDto after a successful merge.
 */
export default function MergeRunnersDialog({ open, onOpenChange, runners, onMerged }) {
  const [pair, setPair] = useState([]);
  const [keepId, setKeepId] = useState(null);
  const [preview, setPreview] = useState(null);
  const [previewError, setPreviewError] = useState(null);
  const [loadingPreview, setLoadingPreview] = useState(false);
  const [confirmed, setConfirmed] = useState(false);
  const [merging, setMerging] = useState(false);
  const [mergeError, setMergeError] = useState(null);

  useEffect(() => {
    if (!open) return;
    setPair(runners?.filter(Boolean) ?? []);
    setKeepId(runners?.[0]?.runnerId ?? null);
    setPreview(null);
    setPreviewError(null);
    setConfirmed(false);
    setMergeError(null);
  }, [open, runners]);

  const keep = pair.find((r) => r.runnerId === keepId);
  const other = pair.find((r) => r.runnerId !== keepId);

  useEffect(() => {
    if (!open || !keep || !other) return;
    let cancelled = false;
    setLoadingPreview(true);
    setPreviewError(null);
    setConfirmed(false);
    runnerService
      .getMergePreview(keep.runnerId, other.runnerId)
      .then((p) => !cancelled && setPreview(p))
      .catch((err) => !cancelled && setPreviewError(err.message))
      .finally(() => !cancelled && setLoadingPreview(false));
    return () => {
      cancelled = true;
    };
  }, [open, keep, other]);

  const handleMerge = async () => {
    setMerging(true);
    setMergeError(null);
    try {
      const result = await runnerService.mergeRunners(keep.runnerId, other.runnerId);
      onMerged?.(result, other);
      onOpenChange(false);
    } catch (err) {
      setMergeError(err.message);
    } finally {
      setMerging(false);
    }
  };

  const canMerge = preview?.canMerge && confirmed && !loadingPreview && !merging;

  return (
    <AlertDialog open={open} onOpenChange={(o) => !merging && onOpenChange(o)}>
      <AlertDialogContent
        size="lg"
        className="max-h-[calc(100svh-2rem)] w-[calc(100%-2rem)] max-w-2xl overflow-y-auto"
      >
        <AlertDialogHeader className="place-items-start text-left">
          <AlertDialogTitle>Merge runner records</AlertDialogTitle>
          <AlertDialogDescription>
            Combine two records for the same person into one. All of their results end up on the
            kept record, and both names are saved as alternate names so future uploads match it.
          </AlertDialogDescription>
        </AlertDialogHeader>

        {pair.length === 1 && (
          <RunnerPicker exclude={pair[0]} onPick={(r) => setPair([pair[0], r])} />
        )}

        {pair.length === 2 && (
          <>
            <div className="flex items-stretch gap-2 max-sm:flex-col">
              <div className="flex-1">
                <KeepOption runner={pair[0]} selected={keepId === pair[0].runnerId} onSelect={() => setKeepId(pair[0].runnerId)} />
              </div>
              <ArrowRight className="size-4 self-center text-muted-foreground max-sm:hidden" aria-hidden="true" />
              <div className="flex-1">
                <KeepOption runner={pair[1]} selected={keepId === pair[1].runnerId} onSelect={() => setKeepId(pair[1].runnerId)} />
              </div>
            </div>

            {loadingPreview && (
              <div className="flex items-center gap-2 text-sm text-muted-foreground">
                <Loader2 className="size-4 animate-spin" /> Checking what this merge will change…
              </div>
            )}
            {previewError && (
              <Alert variant="destructive">
                <AlertCircle />
                <AlertDescription>{previewError}</AlertDescription>
              </Alert>
            )}
            {preview && !loadingPreview && <MergePreview preview={preview} />}

            {preview?.canMerge && !loadingPreview && (
              <label className="flex cursor-pointer items-start gap-2 text-sm">
                <input
                  type="checkbox"
                  checked={confirmed}
                  onChange={(e) => setConfirmed(e.target.checked)}
                  className="mt-0.5 accent-primary"
                />
                <span>
                  I&rsquo;ve checked that <span className="font-medium">{keep.fullName}</span> and{' '}
                  <span className="font-medium">{other.fullName}</span> are the same person. This can&rsquo;t be undone.
                </span>
              </label>
            )}

            {mergeError && (
              <Alert variant="destructive">
                <AlertCircle />
                <AlertDescription>{mergeError}</AlertDescription>
              </Alert>
            )}
          </>
        )}

        <AlertDialogFooter>
          <AlertDialogCancel disabled={merging}>Cancel</AlertDialogCancel>
          {pair.length === 2 && (
            <AlertDialogAction variant="destructive" disabled={!canMerge} onClick={handleMerge}>
              {merging ? 'Merging…' : 'Merge Runners'}
            </AlertDialogAction>
          )}
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
