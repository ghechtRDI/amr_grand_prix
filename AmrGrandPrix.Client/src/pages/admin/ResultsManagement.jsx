/**
 * Admin Results Management
 * Lists upload batches, allows deletion and GP recalculation.
 */

import { useState, useEffect, useCallback } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { AlertCircle, CheckCircle2, X } from 'lucide-react';
import * as tokenService from '../../services/tokenService';
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from '@/components/ui/table';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardHeader, CardContent } from '@/components/ui/card';
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
import { cn } from '@/lib/utils';

const CURRENT_YEAR = new Date().getFullYear();
const YEAR_OPTIONS = Array.from({ length: 5 }, (_, i) => CURRENT_YEAR - i);

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

  const handleDelete = async (batch) => {
    setDeleting(batch.uploadBatchId);
    setMessage(null);
    try {
      const res = await fetch(`/api/results/batch/${batch.uploadBatchId}`, {
        method: 'DELETE',
        headers: { Authorization: `Bearer ${tokenService.getAccessToken()}` },
      });
      if (!res.ok) {
        const txt = await res.text();
        throw new Error(txt || `HTTP ${res.status}`);
      }
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
      const res = await fetch(`/api/standings/${year}/recalculate`, {
        method: 'POST',
        headers: { Authorization: `Bearer ${tokenService.getAccessToken()}` },
      });
      if (!res.ok) {
        const txt = await res.text();
        throw new Error(txt || `HTTP ${res.status}`);
      }
      const data = await res.json();
      setMessage({ type: 'success', text: data.message || 'Standings recalculated.' });
    } catch (e) {
      setMessage({ type: 'error', text: `Recalculation failed: ${e.message}` });
    } finally {
      setRecalculating(false);
      setRecalculateDialogOpen(false);
    }
  };

  // Group batches by race for easier reading
  const raceGroups = batches.reduce((acc, b) => {
    const key = b.raceId;
    if (!acc[key]) {
      acc[key] = {
        raceId: b.raceId,
        raceName: b.raceName,
        raceDate: b.raceDate,
        isGrandPrixRace: b.isGrandPrixRace,
        batches: [],
      };
    }
    acc[key].batches.push(b);
    return acc;
  }, {});

  const races = Object.values(raceGroups).sort(
    (a, b) => new Date(b.raceDate) - new Date(a.raceDate)
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
              disabled={recalculating}
            >
              {recalculating ? 'Recalculating...' : 'Recalculate GP Standings'}
            </Button>
            <Button onClick={() => navigate('/admin/results/upload')}>
              Upload Results
            </Button>
          </div>
        </div>

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

        {!loading && !error && races.map((race) => (
          <Card key={race.raceId} className="mb-6">
            <CardHeader className="flex-row flex-wrap items-center justify-between gap-2 border-b border-border pb-4">
              <div className="flex flex-wrap items-center gap-3">
                <h3 className="text-base font-semibold">
                  <Link to={`/races/${race.raceId}/results`} className="text-foreground hover:text-primary">
                    {race.raceName}
                  </Link>
                </h3>
                <span className="text-sm text-muted-foreground">
                  {new Date(race.raceDate).toLocaleDateString('en-US', {
                    year: 'numeric', month: 'short', day: 'numeric',
                  })}
                </span>
                {race.isGrandPrixRace && (
                  <Badge variant="secondary" className="text-[10px] font-bold tracking-wide uppercase">Grand Prix</Badge>
                )}
              </div>
              <Button
                size="sm"
                variant="outline"
                nativeButton={false}
                render={<Link to={`/races/${race.raceId}/results`}>View Results</Link>}
              />
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
    </div>
  );
}
