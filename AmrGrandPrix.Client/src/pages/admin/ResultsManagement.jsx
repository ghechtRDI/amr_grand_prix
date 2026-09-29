/**
 * Admin Results Management
 * Lists upload batches, allows deletion and GP recalculation, and finalizes (locks) a year's
 * Grand Prix standings once the season is over.
 */

import { useState, useEffect, useCallback } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { AlertCircle, AlertTriangle, CheckCircle2, Lock, LockOpen, Pencil, Trophy, X } from 'lucide-react';
import * as tokenService from '../../services/tokenService';
import * as raceService from '../../services/raceService';
import * as standingsService from '../../services/standingsService';
import { readError } from '../../services/api';
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from '@/components/ui/table';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardHeader, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Alert, AlertDescription, AlertAction } from '@/components/ui/alert';
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
import { cn, formatDateOnly } from '@/lib/utils';

const CURRENT_YEAR = new Date().getFullYear();
const YEAR_OPTIONS = Array.from({ length: 5 }, (_, i) => CURRENT_YEAR - i);
const UNGROUPED = '__ungrouped__';

const BATCH_STATUS_BADGE_CLASSES = {
  Saved: 'bg-success/10 text-success dark:bg-success/20',
  Pending: 'bg-amber-500/10 text-amber-700 dark:bg-amber-500/15 dark:text-amber-400',
  Validated: 'bg-blue-500/10 text-blue-700 dark:bg-blue-500/15 dark:text-blue-400',
  Cancelled: 'bg-muted text-muted-foreground',
};

function fileTypeLabel(fileType) {
  if (typeof fileType === 'number') {
    return ['CSV', 'Excel', 'Text', 'PDF'][fileType] ?? fileType;
  }
  return fileType;
}

function statusLabel(status) {
  if (typeof status === 'number') {
    return ['Pending', 'Validated', 'Saved', 'Cancelled'][status] ?? status;
  }
  return status;
}

function BatchStatusBadge({ status }) {
  const s = statusLabel(status);
  return (
    <Badge variant="outline" className={cn('border-transparent font-medium', BATCH_STATUS_BADGE_CLASSES[s] || 'bg-muted text-muted-foreground')}>
      {s}
    </Badge>
  );
}

export default function ResultsManagement() {
  const navigate = useNavigate();
  const [batches, setBatches] = useState([]);
  const [year, setYear] = useState(CURRENT_YEAR);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [deleting, setDeleting] = useState(null);
  const [resuming, setResuming] = useState(null);
  const [recalculating, setRecalculating] = useState(false);
  const [message, setMessage] = useState(null);
  const [deleteTarget, setDeleteTarget] = useState(null);
  const [recalculateDialogOpen, setRecalculateDialogOpen] = useState(false);

  // Finalization status of the selected year's Grand Prix
  const [season, setSeason] = useState(null);
  const [seasonDialog, setSeasonDialog] = useState(null); // 'finalize' | 'unfinalize' | null
  const [seasonBusy, setSeasonBusy] = useState(false);

  const [raceSeriesList, setRaceSeriesList] = useState([]);
  const [editTarget, setEditTarget] = useState(null);
  const [editForm, setEditForm] = useState({ raceSeriesId: '', raceVariantId: '', date: '', isGrandPrixRace: false });
  const [editVariants, setEditVariants] = useState([]);
  const [editLoading, setEditLoading] = useState(false);
  const [editSaving, setEditSaving] = useState(false);
  const [editError, setEditError] = useState(null);

  const loadBatches = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await fetch(`/api/results/batches?year=${year}`, {
        headers: { Authorization: `Bearer ${tokenService.getAccessToken()}` },
      });
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      setBatches(await res.json());
    } catch (e) {
      setError(e.message);
    } finally {
      setLoading(false);
    }
  }, [year]);

  useEffect(() => {
    loadBatches();
  }, [loadBatches]);

  useEffect(() => {
    setSeason(null);
    standingsService.getSeason(year).then(setSeason).catch(() => setSeason(null));
  }, [year]);

  useEffect(() => {
    fetch('/api/race-series')
      .then((r) => (r.ok ? r.json() : []))
      .then(setRaceSeriesList)
      .catch(() => setRaceSeriesList([]));
  }, []);

  const loadEditVariants = async (seriesId) => {
    const series = await raceService.getSeriesDetail(seriesId);
    setEditVariants(series.variants);
    return series.variants;
  };

  const openEdit = async (race) => {
    setEditTarget(race);
    setEditError(null);
    setEditLoading(true);
    try {
      const full = await raceService.getRace(race.raceId);
      await loadEditVariants(full.raceSeriesId);
      setEditForm({
        raceSeriesId: full.raceSeriesId,
        raceVariantId: full.raceVariantId,
        date: full.date,
        isGrandPrixRace: full.isGrandPrixRace,
        // Carried through untouched so saving doesn't wipe fields this dialog doesn't edit.
        location: full.location,
      });
    } catch (e) {
      setEditError(e.message);
    } finally {
      setEditLoading(false);
    }
  };

  // Moving a race to another series means picking one of that series' variants.
  const handleEditSeriesChange = async (seriesId) => {
    setEditForm((f) => ({ ...f, raceSeriesId: seriesId, raceVariantId: '' }));
    try {
      const variants = await loadEditVariants(seriesId);
      if (variants.length === 1) setEditForm((f) => ({ ...f, raceVariantId: variants[0].raceVariantId }));
    } catch (e) {
      setEditError(e.message);
    }
  };

  const closeEdit = () => {
    setEditTarget(null);
    setEditError(null);
  };

  const handleEditSave = async () => {
    if (!editForm.raceVariantId) {
      setEditError('Please select a course variant');
      return;
    }
    setEditSaving(true);
    setEditError(null);
    try {
      const updated = await raceService.updateRace(editTarget.raceId, editForm);
      setMessage({ type: 'success', text: `Updated ${updated.name}${updated.courseVariant ? ` – ${updated.courseVariant}` : ''} ${updated.year}.` });
      closeEdit();
      await loadBatches();
    } catch (e) {
      setEditError(e.message);
    } finally {
      setEditSaving(false);
    }
  };

  const handleDelete = async (batch) => {
    setDeleting(batch.uploadBatchId);
    setMessage(null);
    try {
      const res = await fetch(`/api/results/batch/${batch.uploadBatchId}`, {
        method: 'DELETE',
        headers: { Authorization: `Bearer ${tokenService.getAccessToken()}` },
      });
      if (!res.ok) throw new Error(await readError(res));
      setMessage({ type: 'success', text: `Deleted ${batch.recordsUploaded} results from ${batch.raceName}.` });
      await loadBatches();
    } catch (e) {
      setMessage({ type: 'error', text: `Delete failed: ${e.message}` });
    } finally {
      setDeleting(null);
      setDeleteTarget(null);
    }
  };

  const handleResume = async (batch) => {
    setResuming(batch.uploadBatchId);
    setMessage(null);
    try {
      const res = await fetch(`/api/results/batch/${batch.uploadBatchId}/resume`, {
        headers: { Authorization: `Bearer ${tokenService.getAccessToken()}` },
      });
      if (!res.ok) {
        const txt = await res.text();
        throw new Error(txt || `HTTP ${res.status}`);
      }
      const data = await res.json();
      navigate('/admin/results/upload', {
        state: {
          resume: {
            raceSelection: {
              raceId: data.raceId,
              raceName: data.raceName,
              raceDate: data.raceDate,
              isGrandPrixRace: data.isGrandPrixRace,
              courseVariant: data.courseVariant || '',
              raceSeriesId: data.raceSeriesId,
              raceVariantId: data.raceVariantId,
              includedVariantIds: data.includedVariantIds,
              knownVariants: data.includedVariantNames,
            },
            uploadBatchId: data.uploadBatchId,
            parsedResults: data.parsedResults,
            totalRows: data.totalRows,
            validRows: data.validRows,
            rowsWithIssues: data.rowsWithIssues,
          },
        },
      });
    } catch (e) {
      setMessage({ type: 'error', text: `Resume failed: ${e.message}` });
    } finally {
      setResuming(null);
    }
  };

  const handleRecalculate = async () => {
    setRecalculating(true);
    setMessage(null);
    try {
      const data = await standingsService.recalculateStandings(year);
      setMessage({ type: 'success', text: data.message || 'Standings recalculated.' });
    } catch (e) {
      setMessage({ type: 'error', text: `Recalculation failed: ${e.message}` });
    } finally {
      setRecalculating(false);
      setRecalculateDialogOpen(false);
    }
  };

  const handleSeasonChange = async () => {
    const finalizing = seasonDialog === 'finalize';
    setSeasonBusy(true);
    setMessage(null);
    try {
      const updated = finalizing
        ? await standingsService.finalizeSeason(year)
        : await standingsService.unfinalizeSeason(year);
      setSeason(updated);
      setMessage({
        type: 'success',
        text: finalizing
          ? `The ${year} Grand Prix is finalized. Its standings are now locked.`
          : `The ${year} Grand Prix is un-finalized. Make your corrections, then finalize it again.`,
      });
    } catch (e) {
      setMessage({ type: 'error', text: `${finalizing ? 'Finalize' : 'Un-finalize'} failed: ${e.message}` });
    } finally {
      setSeasonBusy(false);
      setSeasonDialog(null);
    }
  };

  // Group batches by race for easier reading
  // A pending multi-variant upload has no race until it's saved, so it gets a card of its own.
  const raceGroups = batches.reduce((acc, b) => {
    const key = b.raceId ?? `pending:${b.uploadBatchId}`;
    if (!acc[key]) {
      acc[key] = {
        key,
        raceId: b.raceId,
        raceName: b.raceName,
        raceDate: b.raceDate,
        isGrandPrixRace: b.isGrandPrixRace,
        raceSeriesId: b.raceSeriesId,
        raceSeriesName: b.raceSeriesName,
        batches: [],
      };
    }
    acc[key].batches.push(b);
    return acc;
  }, {});

  const races = Object.values(raceGroups).sort(
    (a, b) => new Date(b.raceDate) - new Date(a.raceDate)
  );

  // Group races by series (races without one fall into a single "Ungrouped" section),
  // sections ordered by each one's most recent race so active series bubble to the top.
  const seriesGroups = races.reduce((acc, race) => {
    const key = race.raceSeriesId || UNGROUPED;
    if (!acc[key]) {
      acc[key] = {
        seriesId: race.raceSeriesId,
        seriesName: race.raceSeriesName || 'Ungrouped Races',
        races: [],
      };
    }
    acc[key].races.push(race);
    return acc;
  }, {});

  const seriesSections = Object.values(seriesGroups).sort(
    (a, b) => new Date(b.races[0].raceDate) - new Date(a.races[0].raceDate)
  );

  return (
    <div className="min-h-svh bg-background px-4 py-8 md:py-12">
      <div className="mx-auto max-w-6xl">
        <div className="mb-6 flex flex-wrap items-center justify-between gap-4">
          <h1 className="bg-gradient-to-r from-primary to-secondary bg-clip-text text-2xl font-semibold tracking-tight text-transparent md:text-3xl">
            Results Management
          </h1>
          <div className="flex flex-wrap items-center gap-3">
            <Select value={String(year)} onValueChange={(v) => setYear(parseInt(v))}>
              <SelectTrigger className="w-24">
                <SelectValue>{(value) => value}</SelectValue>
              </SelectTrigger>
              <SelectContent>
                {YEAR_OPTIONS.map((y) => (
                  <SelectItem key={y} value={String(y)}>{y}</SelectItem>
                ))}
              </SelectContent>
            </Select>
            <Button
              variant="outline"
              onClick={() => setRecalculateDialogOpen(true)}
              disabled={recalculating || !!season?.isFinalized}
              title={season?.isFinalized ? `The ${year} Grand Prix is finalized` : undefined}
            >
              {recalculating ? 'Recalculating...' : 'Recalculate GP Standings'}
            </Button>
            <Button onClick={() => navigate('/admin/results/upload')}>
              Upload Results
            </Button>
          </div>
        </div>

        {season && (
          <div
            className={cn(
              'mb-6 flex flex-wrap items-center justify-between gap-3 rounded-lg border px-4 py-3 text-sm',
              season.isFinalized
                ? 'border-success/30 bg-success/10'
                : 'border-amber-500/30 bg-amber-500/10'
            )}
          >
            <div className="flex items-center gap-2">
              {season.isFinalized ? (
                <>
                  <Lock className="size-4 text-success" aria-hidden="true" />
                  <span>
                    <span className="font-semibold">{year} Grand Prix finalized</span>
                    {season.finalizedAt && (
                      <span className="text-muted-foreground">
                        {' '}on {new Date(season.finalizedAt).toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' })}
                      </span>
                    )}
                    <span className="text-muted-foreground"> · Standings are locked.</span>
                  </span>
                </>
              ) : (
                <>
                  <LockOpen className="size-4 text-amber-600 dark:text-amber-400" aria-hidden="true" />
                  <span>
                    <span className="font-semibold">{year} Grand Prix in progress</span>
                    <span className="text-muted-foreground"> · Standings are shown as tentative until finalized.</span>
                  </span>
                </>
              )}
            </div>
            <Button
              size="sm"
              variant={season.isFinalized ? 'outline' : 'default'}
              onClick={() => setSeasonDialog(season.isFinalized ? 'unfinalize' : 'finalize')}
              disabled={seasonBusy}
            >
              {season.isFinalized ? 'Un-finalize' : `Finalize ${year} Grand Prix`}
            </Button>
          </div>
        )}

        {message && (
          <Alert variant={message.type === 'success' ? 'success' : 'destructive'} className="mb-4">
            {message.type === 'success' ? <CheckCircle2 /> : <AlertCircle />}
            <AlertDescription className={message.type === 'success' ? 'text-success' : undefined}>
              {message.text}
            </AlertDescription>
            <AlertAction>
              <Button
                variant="ghost"
                size="icon-sm"
                onClick={() => setMessage(null)}
                aria-label="Dismiss"
              >
                <X className="size-4" />
              </Button>
            </AlertAction>
          </Alert>
        )}

        {loading && (
          <div className="space-y-4">
            <Skeleton className="h-32 w-full" />
            <Skeleton className="h-32 w-full" />
          </div>
        )}

        {!loading && error && (
          <Alert variant="destructive">
            <AlertCircle />
            <AlertDescription>{error}</AlertDescription>
          </Alert>
        )}

        {!loading && !error && races.length === 0 && (
          <div className="rounded-xl border border-border bg-card px-6 py-16 text-center text-sm text-muted-foreground">
            No uploaded results for {year}.{' '}
            <Link to="/admin/results/upload" className="text-primary hover:underline">Upload some results.</Link>
          </div>
        )}

        {!loading && !error && seriesSections.map((section) => (
          <div key={section.seriesId || UNGROUPED} className="mb-8">
            <div className="mb-3 flex items-center gap-2 border-b border-border pb-2">
              <h2 className="text-lg font-semibold">{section.seriesName}</h2>
              {section.races.some((r) => r.isGrandPrixRace) && (
                <Trophy className="size-4 text-secondary" aria-hidden="true" />
              )}
              <span className="text-sm text-muted-foreground">
                ({section.races.length} race{section.races.length === 1 ? '' : 's'})
              </span>
            </div>

            {section.races.map((race) => (
              <Card key={race.key} className="mb-6">
                <CardHeader className="flex-row flex-wrap items-center justify-between gap-2 border-b border-border pb-4">
                  <div className="flex flex-wrap items-center gap-3">
                    <h3 className="text-base font-semibold">
                      {race.raceId ? (
                        <Link to={`/races/${race.raceId}/results`} className="text-foreground hover:text-primary">
                          {race.raceName}
                        </Link>
                      ) : (
                        race.raceName
                      )}
                    </h3>
                    <span className="text-sm text-muted-foreground">
                      {formatDateOnly(race.raceDate, { year: 'numeric', month: 'short', day: 'numeric' })}
                    </span>
                    {race.isGrandPrixRace && (
                      <Badge variant="secondary" className="text-[10px] font-bold tracking-wide uppercase">Grand Prix</Badge>
                    )}
                  </div>
                  {race.raceId && (
                    <div className="flex gap-2">
                      <Button
                        size="sm"
                        variant="outline"
                        onClick={() => openEdit(race)}
                      >
                        <Pencil className="size-3.5" /> Edit
                      </Button>
                      <Button
                        size="sm"
                        variant="outline"
                        nativeButton={false}
                        render={<Link to={`/races/${race.raceId}/results`}>View Results</Link>}
                      />
                    </div>
                  )}
                </CardHeader>
                <CardContent className="px-0 pb-0">
              <Table>
                <TableHeader>
                  <TableRow className="hover:bg-transparent">
                    <TableHead>File</TableHead>
                    <TableHead>Type</TableHead>
                    <TableHead className="text-right">Records</TableHead>
                    <TableHead>Uploaded</TableHead>
                    <TableHead>Status</TableHead>
                    <TableHead className="text-right">Actions</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {race.batches.map((b, idx) => (
                    <TableRow key={b.uploadBatchId} className={cn(idx % 2 === 1 && 'bg-muted/20')}>
                      <TableCell className="max-w-[220px] truncate" title={b.fileName}>
                        {b.fileName}
                      </TableCell>
                      <TableCell>{fileTypeLabel(b.fileType)}</TableCell>
                      <TableCell className="text-right tabular-nums">{b.recordsUploaded}</TableCell>
                      <TableCell className="text-muted-foreground">
                        {new Date(b.uploadedAt).toLocaleString()}
                      </TableCell>
                      <TableCell><BatchStatusBadge status={b.status} /></TableCell>
                      <TableCell className="text-right whitespace-nowrap">
                        <div className="flex justify-end gap-1.5">
                          {statusLabel(b.status) === 'Pending' && (
                            <Button
                              size="sm"
                              variant="outline"
                              onClick={() => handleResume(b)}
                              disabled={resuming === b.uploadBatchId}
                            >
                              {resuming === b.uploadBatchId ? 'Opening...' : 'Resume'}
                            </Button>
                          )}
                          <Button
                            size="sm"
                            variant="destructive"
                            onClick={() => setDeleteTarget(b)}
                            disabled={deleting === b.uploadBatchId}
                          >
                            {deleting === b.uploadBatchId ? 'Deleting...' : 'Delete'}
                          </Button>
                        </div>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
                </CardContent>
              </Card>
            ))}
          </div>
        ))}

        <div className="mt-6 flex gap-6 border-t border-border pt-4 text-sm">
          <Link to="/" className="text-muted-foreground hover:text-foreground">← Home</Link>
          <Link to={`/standings/${year}`} className="text-muted-foreground hover:text-foreground">View Standings →</Link>
        </div>
      </div>

      <AlertDialog open={!!deleteTarget} onOpenChange={(open) => !open && setDeleteTarget(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Delete this batch?</AlertDialogTitle>
            <AlertDialogDescription>
              Delete all {deleteTarget?.recordsUploaded} results from &ldquo;{deleteTarget?.raceName}&rdquo; ({deleteTarget?.fileName})? This cannot be undone.
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

      <AlertDialog open={recalculateDialogOpen} onOpenChange={setRecalculateDialogOpen}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Recalculate standings?</AlertDialogTitle>
            <AlertDialogDescription>
              Recalculate all Grand Prix standings for {year}? This may take a moment.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction onClick={handleRecalculate}>
              Recalculate
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      <AlertDialog open={!!seasonDialog} onOpenChange={(open) => !open && !seasonBusy && setSeasonDialog(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>
              {seasonDialog === 'finalize' ? `Finalize the ${year} Grand Prix?` : `Un-finalize the ${year} Grand Prix?`}
            </AlertDialogTitle>
            <AlertDialogDescription>
              {seasonDialog === 'finalize'
                ? `Standings will be recalculated one last time and then locked. While the ${year} Grand Prix is finalized, standings can't be recalculated, and results for its Grand Prix races can't be added, edited, or deleted. Runner pages will show ${year} as final instead of tentative. You can un-finalize later if results need correcting.`
                : `The ${year} standings will unlock and show as tentative again, so results can be corrected and standings recalculated. Remember to finalize the ${year} Grand Prix again once the corrections are done.`}
            </AlertDialogDescription>
          </AlertDialogHeader>
          {seasonDialog === 'finalize' && season?.grandPrixRacesWithoutResults?.length > 0 && (
            <Alert variant="destructive">
              <AlertTriangle />
              <AlertDescription>
                {season.grandPrixRacesWithoutResults.length} of {season.grandPrixRaceCount} Grand Prix races in {year} have no results yet:{' '}
                {season.grandPrixRacesWithoutResults.join(', ')}.
              </AlertDescription>
            </Alert>
          )}
          <AlertDialogFooter>
            <AlertDialogCancel disabled={seasonBusy}>Cancel</AlertDialogCancel>
            <AlertDialogAction
              variant={seasonDialog === 'finalize' ? 'default' : 'destructive'}
              onClick={handleSeasonChange}
              disabled={seasonBusy}
            >
              {seasonBusy
                ? (seasonDialog === 'finalize' ? 'Finalizing…' : 'Un-finalizing…')
                : (seasonDialog === 'finalize' ? 'Finalize' : 'Un-finalize')}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      <AlertDialog open={!!editTarget} onOpenChange={(open) => !open && closeEdit()}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Edit Race</AlertDialogTitle>
          </AlertDialogHeader>

          {editLoading ? (
            <div className="flex flex-col gap-3">
              <Skeleton className="h-8 w-full" />
              <Skeleton className="h-8 w-full" />
              <Skeleton className="h-8 w-full" />
            </div>
          ) : (
            <div className="flex flex-col gap-3 text-left">
              {editError && (
                <Alert variant="destructive">
                  <AlertCircle />
                  <AlertDescription>{editError}</AlertDescription>
                </Alert>
              )}
              <div className="flex flex-col gap-1.5">
                <Label htmlFor="editSeries">Race Series</Label>
                <Select value={editForm.raceSeriesId || undefined} onValueChange={handleEditSeriesChange}>
                  <SelectTrigger id="editSeries" className="w-full">
                    <SelectValue placeholder="-- Select a series --">
                      {(value) => raceSeriesList.find((s) => s.raceSeriesId === value)?.name || '-- Select a series --'}
                    </SelectValue>
                  </SelectTrigger>
                  <SelectContent>
                    {raceSeriesList.map((s) => (
                      <SelectItem key={s.raceSeriesId} value={s.raceSeriesId}>{s.name}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              {editVariants.length > 1 && (
                <div className="flex flex-col gap-1.5">
                  <Label htmlFor="editVariant">Course Variant</Label>
                  <Select
                    value={editForm.raceVariantId || undefined}
                    onValueChange={(v) => setEditForm((f) => ({ ...f, raceVariantId: v }))}
                  >
                    <SelectTrigger id="editVariant" className="w-full">
                      <SelectValue placeholder="-- Select a variant --">
                        {(value) => editVariants.find((v) => v.raceVariantId === value)?.name || '-- Select a variant --'}
                      </SelectValue>
                    </SelectTrigger>
                    <SelectContent>
                      {editVariants.map((v) => (
                        <SelectItem key={v.raceVariantId} value={v.raceVariantId}>{v.name}</SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
              )}
              <div className="flex flex-col gap-1.5">
                <Label htmlFor="editDate">Race Date</Label>
                <Input
                  id="editDate"
                  type="date"
                  value={editForm.date}
                  onChange={(e) => setEditForm((f) => ({ ...f, date: e.target.value }))}
                />
              </div>
              <label className="flex items-center gap-2 text-sm font-medium">
                <input
                  type="checkbox"
                  className="size-4 rounded border-input accent-primary"
                  checked={!!editForm.isGrandPrixRace}
                  onChange={(e) => setEditForm((f) => ({ ...f, isGrandPrixRace: e.target.checked }))}
                />
                Counts toward the Grand Prix this year
              </label>
              <p className="text-xs text-muted-foreground">
                Changing the Grand Prix flag recalculates points and standings for the year.
              </p>
            </div>
          )}

          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction onClick={handleEditSave} disabled={editLoading || editSaving}>
              {editSaving ? 'Saving...' : 'Save'}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
