/**
 * Racer Management (Admin/Manager)
 * Two views: likely duplicate runner records (75%+ identity match) with merge and "not a match"
 * actions (dismissed pairs are hidden but can be restored), and a
 * searchable list of every runner for editing date of birth / alternate names or merging by hand
 * (e.g. a maiden name that's too different to be flagged automatically).
 */

import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { AlertCircle, CheckCircle2, EyeOff, Merge, Pencil, RefreshCw, Undo2, UserX, Users, X } from 'lucide-react';
import * as runnerService from '../../services/runnerService';
import { Alert, AlertAction, AlertDescription } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { SearchInput } from '@/components/ui/search-input';
import { Skeleton } from '@/components/ui/skeleton';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import EditRunnerDialog from '@/components/runner-management/EditRunnerDialog';
import MergeRunnersDialog from '@/components/runner-management/MergeRunnersDialog';
import { AlternateNameChips, RunnerSummary } from '@/components/runner-management/shared';
import { formatBirth, formatResults } from '@/components/runner-management/format';
import { cn } from '@/lib/utils';

const TAB_DUPLICATES = 'duplicates';
const TAB_ALL = 'all';

function SignalBadge({ ok, label }) {
  if (ok === null || ok === undefined) return <Badge variant="outline">{label} unknown</Badge>;
  return <Badge variant={ok ? 'success' : 'destructive'}>{label} {ok ? 'match' : 'differs'}</Badge>;
}

const pairKey = (d) => [d.runnerA.runnerId, d.runnerB.runnerId].sort().join('-');

/** Every name a runner might be searched by: legal, preferred, and alternate. */
const runnerHaystack = (r) => [r.fullName, r.preferredName, ...(r.alternateNames ?? [])].filter(Boolean).join(' ').toLowerCase();

/** True when either runner in the pair matches every search term. */
function pairMatches(d, search) {
  const terms = search.toLowerCase().split(/\s+/).filter(Boolean);
  if (terms.length === 0) return true;
  return [d.runnerA, d.runnerB].some((r) => {
    const haystack = runnerHaystack(r);
    return terms.every((t) => haystack.includes(t));
  });
}

function PairCard({ pair, header, actions }) {
  return (
    <Card>
      <CardContent className="flex flex-col gap-3">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <div className="flex flex-wrap items-center gap-2">{header}</div>
          <div className="flex flex-wrap gap-2">{actions}</div>
        </div>
        <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
          <RunnerSummary runner={pair.runnerA} />
          <RunnerSummary runner={pair.runnerB} />
        </div>
      </CardContent>
    </Card>
  );
}

function EmptyCard({ children }) {
  return (
    <Card>
      <CardContent className="flex flex-col items-center gap-2 py-10 text-center text-muted-foreground">
        <Users className="size-8" aria-hidden="true" />
        <p>{children}</p>
      </CardContent>
    </Card>
  );
}

function DuplicatesPanel({ duplicates, dismissed, loading, error, busyKey, onMerge, onDismiss, onRestore }) {
  const [search, setSearch] = useState('');
  const [showDismissed, setShowDismissed] = useState(false);

  if (loading) {
    return (
      <div className="flex flex-col gap-3">
        {[1, 2, 3].map((n) => <Skeleton key={n} className="h-32 w-full" />)}
      </div>
    );
  }
  if (error) {
    return (
      <Alert variant="destructive">
        <AlertCircle />
        <AlertDescription>{error}</AlertDescription>
      </Alert>
    );
  }

  const source = showDismissed ? dismissed : duplicates;
  const visible = source.filter((d) => pairMatches(d, search));
  const searching = search.trim().length > 0;

  let body;
  if (source.length === 0) {
    body = showDismissed ? (
      <EmptyCard>No pairs have been marked as not a match.</EmptyCard>
    ) : (
      <EmptyCard>No likely duplicates found. Every pair of runners is under a 75% match.</EmptyCard>
    );
  } else if (visible.length === 0) {
    body = <EmptyCard>No pairs match &ldquo;{search}&rdquo;.</EmptyCard>;
  } else if (showDismissed) {
    body = visible.map((d) => (
      <PairCard
        key={pairKey(d)}
        pair={d}
        header={
          <span className="text-sm text-muted-foreground">
            Marked not a match {new Date(d.dismissedAt).toLocaleDateString()}
            {d.dismissedBy && <> by {d.dismissedBy}</>}
          </span>
        }
        actions={
          <Button size="sm" variant="outline" disabled={busyKey === pairKey(d)} onClick={() => onRestore(d)}>
            <Undo2 /> Restore
          </Button>
        }
      />
    ));
  } else {
    body = visible.map((d) => (
      <PairCard
        key={pairKey(d)}
        pair={d}
        header={
          <>
            <Badge variant={d.confidence >= 0.9 ? 'default' : 'secondary'}>
              {Math.round(d.confidence * 100)}% match
            </Badge>
            <Badge variant="outline">Name {Math.round(d.nameSimilarity * 100)}%</Badge>
            <SignalBadge ok={d.genderMatch} label="Gender" />
            <SignalBadge ok={d.birthYearMatch} label="Birth year" />
          </>
        }
        actions={
          <>
            <Button size="sm" variant="outline" disabled={busyKey === pairKey(d)} onClick={() => onDismiss(d)}>
              <UserX /> Not a match
            </Button>
            <Button size="sm" onClick={() => onMerge([d.runnerA, d.runnerB])}>
              <Merge /> Review &amp; Merge
            </Button>
          </>
        }
      />
    ));
  }

  return (
    <div className="flex flex-col gap-3">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <SearchInput
          value={search}
          onChange={setSearch}
          placeholder="Search names, including alternate names…"
          className="w-full max-w-sm"
        />
        <Button variant="ghost" size="sm" onClick={() => setShowDismissed((v) => !v)}>
          {showDismissed ? (
            <><Users /> Back to possible duplicates</>
          ) : (
            <><EyeOff /> Not a match ({dismissed.length})</>
          )}
        </Button>
      </div>
      <p className="text-sm text-muted-foreground">
        {showDismissed ? (
          <>
            {searching ? `${visible.length} of ${source.length}` : source.length} pair{source.length === 1 ? '' : 's'}{' '}
            marked as different people. Restore a pair to put it back on the duplicates list.
          </>
        ) : (
          <>
            {searching ? `${visible.length} of ${source.length}` : source.length} pair{source.length === 1 ? '' : 's'}{' '}
            of runners with a 75%+ match on name, gender, and birth year. Pairs who ran the same race
            aren&rsquo;t shown — one person can&rsquo;t finish a race twice — and neither are pairs
            marked &ldquo;Not a match&rdquo;.
          </>
        )}
      </p>
      {body}
    </div>
  );
}

function AllRunnersPanel({ refreshKey, onEdit, onMerge }) {
  const [search, setSearch] = useState('');
  const [runners, setRunners] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);
    runnerService
      .getRunnersForManagement(search, 200)
      .then((r) => !cancelled && setRunners(r))
      .catch((err) => !cancelled && setError(err.message))
      .finally(() => !cancelled && setLoading(false));
    return () => {
      cancelled = true;
    };
  }, [search, refreshKey]);

  return (
    <div className="flex flex-col gap-4">
      <SearchInput
        value={search}
        onChange={setSearch}
        placeholder="Search names, including alternate names…"
        className="max-w-sm"
      />
      {error && (
        <Alert variant="destructive">
          <AlertCircle />
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      )}
      <Card className="py-0">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Name</TableHead>
              <TableHead>Alternate Names</TableHead>
              <TableHead>Gender</TableHead>
              <TableHead>Born</TableHead>
              <TableHead>Results</TableHead>
              <TableHead className="text-right">Actions</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {loading &&
              [1, 2, 3, 4, 5].map((n) => (
                <TableRow key={n}>
                  <TableCell colSpan={6}><Skeleton className="h-5 w-full" /></TableCell>
                </TableRow>
              ))}
            {!loading && runners.length === 0 && (
              <TableRow>
                <TableCell colSpan={6} className="py-8 text-center text-muted-foreground">
                  No runners found.
                </TableCell>
              </TableRow>
            )}
            {!loading &&
              runners.map((r) => (
                <TableRow key={r.runnerId}>
                  <TableCell>
                    <Link
                      to={`/runners/${r.runnerId}`}
                      target="_blank"
                      rel="noopener noreferrer"
                      className="font-medium hover:underline"
                    >
                      {r.fullName}
                    </Link>
                    {r.preferredName && <span className="block text-xs text-muted-foreground">Prefers {r.preferredName}</span>}
                    {r.linkedAccountEmail && <Badge variant="secondary" className="mt-1">Linked account</Badge>}
                  </TableCell>
                  <TableCell className="max-w-xs whitespace-normal">
                    <AlternateNameChips names={r.alternateNames} emptyText="—" />
                  </TableCell>
                  <TableCell>{r.gender}</TableCell>
                  <TableCell>{formatBirth(r)}</TableCell>
                  <TableCell>{formatResults(r)}</TableCell>
                  <TableCell className="text-right">
                    <div className="flex justify-end gap-1">
                      <Button variant="ghost" size="sm" onClick={() => onEdit(r)}>
                        <Pencil /> Edit
                      </Button>
                      <Button variant="ghost" size="sm" onClick={() => onMerge([r])}>
                        <Merge /> Merge…
                      </Button>
                    </div>
                  </TableCell>
                </TableRow>
              ))}
          </TableBody>
        </Table>
      </Card>
      {!loading && runners.length === 200 && (
        <p className="text-sm text-muted-foreground">Showing the first 200 runners — search to narrow the list.</p>
      )}
    </div>
  );
}

export default function RunnerManagement() {
  const [tab, setTab] = useState(TAB_DUPLICATES);
  const [duplicates, setDuplicates] = useState([]);
  const [dismissed, setDismissed] = useState([]);
  const [busyKey, setBusyKey] = useState(null);
  const [duplicatesLoading, setDuplicatesLoading] = useState(true);
  const [duplicatesError, setDuplicatesError] = useState(null);
  const [refreshKey, setRefreshKey] = useState(0);
  const [message, setMessage] = useState(null);
  const [editing, setEditing] = useState(null);
  const [mergeRunners, setMergeRunners] = useState(null);

  // `quiet` reloads without the skeleton, e.g. after restoring a pair.
  const loadDuplicates = useCallback(async ({ quiet = false } = {}) => {
    if (!quiet) setDuplicatesLoading(true);
    setDuplicatesError(null);
    try {
      const [open, notMatches] = await Promise.all([
        runnerService.getPotentialDuplicates(),
        runnerService.getDismissedDuplicates(),
      ]);
      setDuplicates(open);
      setDismissed(notMatches);
    } catch (err) {
      setDuplicatesError(err.message);
    } finally {
      setDuplicatesLoading(false);
    }
  }, []);

  useEffect(() => {
    loadDuplicates();
  }, [loadDuplicates]);

  const refreshAll = () => {
    loadDuplicates();
    setRefreshKey((k) => k + 1);
  };

  const handleMerged = (result, removed) => {
    const years = result.yearsRecalculated;
    setMessage({
      type: 'success',
      text:
        `Merged ${removed.fullName} into ${result.runner.fullName}: ${result.resultsMoved} result` +
        `${result.resultsMoved === 1 ? '' : 's'} moved` +
        (years.length ? `, Grand Prix standings recalculated for ${years.join(', ')}.` : '.'),
    });
    refreshAll();
  };

  const restorePair = async (pair, { fromUndo = false } = {}) => {
    const key = pairKey(pair);
    setBusyKey(key);
    try {
      await runnerService.restoreDuplicate(pair.runnerA.runnerId, pair.runnerB.runnerId);
      setMessage(fromUndo ? null : {
        type: 'success',
        text: `${pair.runnerA.fullName} and ${pair.runnerB.fullName} are back on the possible duplicates list.`,
      });
      await loadDuplicates({ quiet: true });
    } catch (err) {
      setMessage({ type: 'error', text: err.message });
    } finally {
      setBusyKey(null);
    }
  };

  const handleDismiss = async (pair) => {
    const key = pairKey(pair);
    setBusyKey(key);
    try {
      await runnerService.dismissDuplicate(pair.runnerA.runnerId, pair.runnerB.runnerId);
      setDuplicates((list) => list.filter((d) => pairKey(d) !== key));
      setDismissed((list) => [{ ...pair, dismissedAt: new Date().toISOString(), dismissedBy: null }, ...list]);
      setMessage({
        type: 'success',
        text: `Marked ${pair.runnerA.fullName} and ${pair.runnerB.fullName} as not a match.`,
        undo: () => restorePair(pair, { fromUndo: true }),
      });
    } catch (err) {
      setMessage({ type: 'error', text: err.message });
    } finally {
      setBusyKey(null);
    }
  };

  const handleSaved = () => {
    setMessage({ type: 'success', text: `Saved ${editing.fullName}.` });
    refreshAll();
  };

  return (
    <div className="min-h-svh bg-background px-4 py-8 md:py-12">
      <div className="mx-auto max-w-6xl">
        <div className="mb-6 flex flex-wrap items-center justify-between gap-4">
          <h1 className="bg-gradient-to-r from-primary to-secondary bg-clip-text text-2xl font-semibold tracking-tight text-transparent md:text-3xl">
            Racer Management
          </h1>
          <Button variant="outline" onClick={refreshAll}>
            <RefreshCw /> Refresh
          </Button>
        </div>

        {message && (
          <Alert
            variant={message.type === 'success' ? 'success' : 'destructive'}
            className={cn('mb-4', message.undo && 'has-data-[slot=alert-action]:pr-32')}
          >
            {message.type === 'success' ? <CheckCircle2 /> : <AlertCircle />}
            <AlertDescription className={message.type === 'success' ? 'text-success' : undefined}>
              {message.text}
            </AlertDescription>
            <AlertAction className="flex items-center gap-1">
              {message.undo && (
                <Button variant="outline" size="sm" onClick={message.undo}>
                  <Undo2 /> Undo
                </Button>
              )}
              <Button variant="ghost" size="icon-sm" onClick={() => setMessage(null)} aria-label="Dismiss">
                <X />
              </Button>
            </AlertAction>
          </Alert>
        )}

        <Tabs value={tab} onValueChange={setTab}>
          <TabsList variant="line" className="mb-6 h-auto gap-4 border-b border-border p-0">
            <TabsTrigger value={TAB_DUPLICATES} className="rounded-none px-1 py-2 text-base data-active:font-semibold">
              Possible Duplicates
              {!duplicatesLoading && duplicates.length > 0 && (
                <Badge variant="secondary" className="ml-1">{duplicates.length}</Badge>
              )}
            </TabsTrigger>
            <TabsTrigger value={TAB_ALL} className="rounded-none px-1 py-2 text-base data-active:font-semibold">
              All Racers
            </TabsTrigger>
          </TabsList>

          <TabsContent value={TAB_DUPLICATES}>
            <DuplicatesPanel
              duplicates={duplicates}
              dismissed={dismissed}
              loading={duplicatesLoading}
              error={duplicatesError}
              busyKey={busyKey}
              onMerge={setMergeRunners}
              onDismiss={handleDismiss}
              onRestore={(pair) => restorePair(pair)}
            />
          </TabsContent>
          <TabsContent value={TAB_ALL}>
            <AllRunnersPanel refreshKey={refreshKey} onEdit={setEditing} onMerge={setMergeRunners} />
          </TabsContent>
        </Tabs>
      </div>

      <EditRunnerDialog
        runner={editing}
        open={!!editing}
        onOpenChange={(open) => !open && setEditing(null)}
        onSaved={handleSaved}
      />

      <MergeRunnersDialog
        runners={mergeRunners}
        open={!!mergeRunners}
        onOpenChange={(open) => !open && setMergeRunners(null)}
        onMerged={handleMerged}
      />
    </div>
  );
}
