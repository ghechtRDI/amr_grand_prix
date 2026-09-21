/**
 * Step 5: Confirmation & Save
 * Final review and save results to database
 */

import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { AlertCircle, CheckCircle2, Loader2 } from 'lucide-react';
import * as tokenService from '../../services/tokenService';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { cn, formatDateOnly } from '@/lib/utils';

// Row's courseVariant, normalized the same way DataReviewStep groups rows.
const groupKeyFor = (row) => (row.courseVariant || '').trim();

const toResultPayload = (row) => ({
  name: row.name,
  age: row.age ? parseInt(row.age) : null,
  ageCategory: row.age ? null : (row.ageCategory || null),
  place: row.place ? parseInt(row.place) : null,
  timeString: row.time,
  gender: row.gender,
  bib: row.bib ? parseInt(row.bib) : null,
  status: row.status,
  matchedRunnerId: row.matchedRunnerId || null,
  updateRunnerAge: !!row.updateRunnerAge,
});

function StatBlock({ label, value, className }) {
  return (
    <div className="rounded-lg border border-border bg-muted/30 px-4 py-3 text-center">
      <div className="text-xs font-medium uppercase tracking-wide text-muted-foreground">{label}</div>
      <div className={cn('mt-1 text-2xl font-semibold tabular-nums', className)}>{value}</div>
    </div>
  );
}

export default function ConfirmationStep({ wizardData, onBack, onCancel }) {
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState(null);
  const [success, setSuccess] = useState(false);
  const [groupResults, setGroupResults] = useState([]);
  const navigate = useNavigate();

  const raceSelection = wizardData.raceSelection || {};
  const reviewedData = wizardData.reviewedData || [];
  const uploadBatchId = wizardData.uploadBatchId;
  const groupRaces = wizardData.groupRaces || {};
  // null when Data Review detected multiple course variants - in that case every
  // group (including whichever one is actually the Step 1 race) was explicitly
  // resolved via groupRaces, and none is auto-matched to the Step 1 selection.
  const primaryGroupKey = wizardData.primaryGroupKey ?? null;

  // Partition reviewed rows by detected course variant, resolving each group to the
  // race it should be saved against (the Step 1 selection for the primary group, or
  // the race picked/created per group in Data Review for any others).
  const saveGroups = (() => {
    const byKey = new Map();
    for (const row of reviewedData) {
      const key = groupKeyFor(row);
      if (!byKey.has(key)) byKey.set(key, []);
      byKey.get(key).push(row);
    }
    return Array.from(byKey.entries()).map(([key, rows], index) => {
      const isPrimary = primaryGroupKey != null && key === primaryGroupKey;
      // Whichever group's save call carries the real uploadBatchId (marking the
      // original UploadBatch row Saved) vs. a sourceUploadBatchId (cloning an audit
      // row for the others) - independent of which race each group maps to. When
      // there's a primary group it owns the batch; otherwise the first group does,
      // arbitrarily, since batch ownership doesn't need to track any particular race.
      const isBatchOwner = primaryGroupKey != null ? isPrimary : index === 0;
      return {
        key,
        rows,
        raceId: isPrimary ? raceSelection.raceId : groupRaces[key]?.raceId,
        raceName: isPrimary ? raceSelection.raceName : (groupRaces[key]?.raceName || key),
        isGrandPrixRace: isPrimary ? !!raceSelection.isGrandPrixRace : !!groupRaces[key]?.isGrandPrixRace,
        uploadBatchId: isBatchOwner ? uploadBatchId : null,
        sourceUploadBatchId: isBatchOwner ? null : uploadBatchId,
      };
    });
  })();

  // Calculate statistics
  const stats = {
    totalResults: reviewedData.length,
    newRunners: reviewedData.filter(r => r.matchStatus === 'new-runner').length,
    dnfCount: reviewedData.filter(r => r.status === 'DNF').length,
    dnsCount: reviewedData.filter(r => r.status === 'DNS').length,
    dqCount: reviewedData.filter(r => r.status === 'DQ').length,
    finishers: reviewedData.filter(r => r.status === 'Finished').length,
  };

  const handleSave = async () => {
    setSaving(true);
    setError(null);

    const token = tokenService.getAccessToken();
    const results = [];

    // Save sequentially (not in parallel) so a failure on one course-variant group
    // doesn't race with, or get lost alongside, a concurrent write to another race.
    for (const group of saveGroups) {
      try {
        const payload = {
          raceId: group.raceId,
          uploadBatchId: group.uploadBatchId,
          sourceUploadBatchId: group.sourceUploadBatchId,
          results: group.rows.map(toResultPayload),
        };

        const response = await fetch('/api/results/save', {
          method: 'POST',
          headers: {
            'Authorization': `Bearer ${token}`,
            'Content-Type': 'application/json',
          },
          body: JSON.stringify(payload),
        });

        if (!response.ok) {
          const errorData = await response.json();
          throw new Error(errorData.message || 'Failed to save results');
        }

        const result = await response.json();
        results.push({ group, success: true, result });
      } catch (err) {
        console.error(`Save error for group "${group.key}":`, err);
        results.push({ group, success: false, error: err.message });
      }
    }

    setGroupResults(results);
    setSaving(false);

    const anySuccess = results.some(r => r.success);
    const anyFailure = results.some(r => !r.success);
    if (anyFailure && !anySuccess) {
      setError(results[0]?.error || 'Failed to save results');
    } else {
      setSuccess(true);
    }
  };

  const handleViewResults = (raceId) => {
    if (raceId) {
      navigate(`/races/${raceId}/results`);
    }
  };

  const handleViewStandings = () => {
    const year = new Date(raceSelection.raceDate).getFullYear();
    navigate(`/standings/${year}`);
  };

  if (success) {
    const anyGpRace = groupResults.some(r => r.success && r.group.isGrandPrixRace);
    const primaryResult = groupResults.find(r => r.group.key === primaryGroupKey);

    return (
      <div>
        <div className="mb-6 flex items-center justify-between border-b border-border pb-4">
          <h2 className="text-xl font-semibold">Success!</h2>
          <span className="text-sm text-muted-foreground">Step 4 of 4</span>
        </div>

        <div className="flex flex-col items-center gap-4 py-8 text-center">
          <div className="flex size-16 items-center justify-center rounded-full bg-success/15 text-success">
            <CheckCircle2 className="size-9" />
          </div>
          <h3 className="text-lg font-semibold">
            {groupResults.length > 1 ? 'Results saved for all course variants!' : 'Results saved successfully!'}
          </h3>
        </div>

        <div className="flex flex-col gap-6">
          {groupResults.map(({ group, success: groupSuccess, result, error: groupError }) => (
            <div key={group.key || '(primary)'} className="rounded-xl border border-border p-5">
              <h4 className="mb-3 font-semibold text-primary">
                {group.raceName}{group.key ? ` (${group.key})` : ''}
              </h4>
              {groupSuccess ? (
                <>
                  <p className="text-sm text-muted-foreground">
                    {result.resultsSaved} result(s) saved.
                    {group.isGrandPrixRace && (
                      <span> Grand Prix standings have been updated automatically.</span>
                    )}
                  </p>
                  {result.skippedResults?.length > 0 && (
                    <Alert className="mt-4 border-amber-500/30 bg-amber-500/10 text-left text-amber-700 dark:text-amber-400">
                      <AlertCircle />
                      <AlertDescription className="text-amber-700 dark:text-amber-400">
                        <strong>{result.skippedResults.length} result(s) were not saved:</strong>
                        <ul className="mt-1 list-disc pl-5 text-sm">
                          {result.skippedResults.map((row) => (
                            <li key={row.rowNumber}>
                              Row {row.rowNumber} — {row.name || '(no name)'}: {row.reason}
                            </li>
                          ))}
                        </ul>
                      </AlertDescription>
                    </Alert>
                  )}
                  <Button
                    type="button"
                    variant="outline"
                    className="mt-4"
                    onClick={() => handleViewResults(result.raceId)}
                  >
                    View {group.raceName} Results
                  </Button>
                </>
              ) : (
                <Alert variant="destructive">
                  <AlertCircle />
                  <AlertDescription>
                    <strong>Error saving this group:</strong> {groupError}
                  </AlertDescription>
                </Alert>
              )}
            </div>
          ))}
        </div>

        <div className="mt-8 flex flex-wrap justify-center gap-3">
          {primaryResult?.success && (
            <Button type="button" onClick={() => handleViewResults(primaryResult.result.raceId)}>
              View Race Results
            </Button>
          )}
          {anyGpRace && (
            <Button type="button" variant="outline" onClick={handleViewStandings}>
              View GP Standings
            </Button>
          )}
          <Button type="button" variant="outline" onClick={() => navigate('/admin/results')}>
            Upload More Results
          </Button>
        </div>
      </div>
    );
  }

  return (
    <div>
      <div className="mb-6 flex items-center justify-between border-b border-border pb-4">
        <h2 className="text-xl font-semibold">Step 4: Confirmation</h2>
        <span className="text-sm text-muted-foreground">Step 4 of 4</span>
      </div>

      <div className="flex flex-col gap-6 rounded-xl border border-border bg-muted/30 p-6">
        <h3 className="text-lg font-semibold">Review Summary</h3>

        <div>
          <h4 className="mb-3 text-sm font-semibold uppercase tracking-wide text-muted-foreground">
            {saveGroups.length > 1 ? 'Races (by course variant)' : 'Race Information'}
          </h4>
          {saveGroups.length > 1 ? (
            <ul className="flex flex-col gap-2 text-sm">
              {saveGroups.map(group => (
                <li key={group.key || '(primary)'} className="flex flex-wrap items-center gap-2">
                  {group.key || '(no variant tag)'} &rarr; <strong>{group.raceName}</strong>
                  <span className="text-muted-foreground">
                    ({group.rows.length} result{group.rows.length === 1 ? '' : 's'})
                  </span>
                  {group.isGrandPrixRace && <Badge variant="secondary">Grand Prix</Badge>}
                </li>
              ))}
            </ul>
          ) : (
            <dl className="grid grid-cols-[auto_1fr] gap-x-6 gap-y-2 text-sm">
              <dt className="font-medium text-muted-foreground">Race:</dt>
              <dd className="flex items-center gap-2">
                {raceSelection.raceName}
                {raceSelection.isGrandPrixRace && <Badge variant="secondary">Grand Prix</Badge>}
              </dd>

              <dt className="font-medium text-muted-foreground">Date:</dt>
              <dd>{formatDateOnly(raceSelection.raceDate)}</dd>

              {raceSelection.courseVariant && (
                <>
                  <dt className="font-medium text-muted-foreground">Course Variant:</dt>
                  <dd>{raceSelection.courseVariant}</dd>
                </>
              )}
            </dl>
          )}
        </div>

        <div>
          <h4 className="mb-3 text-sm font-semibold uppercase tracking-wide text-muted-foreground">
            Results Statistics
          </h4>
          <div className="grid grid-cols-2 gap-3 sm:grid-cols-3 md:grid-cols-6">
            <StatBlock label="Total" value={stats.totalResults} />
            <StatBlock label="Finishers" value={stats.finishers} className="text-success" />
            {stats.newRunners > 0 && <StatBlock label="New Runners" value={stats.newRunners} />}
            {stats.dnfCount > 0 && (
              <StatBlock label="DNF" value={stats.dnfCount} className="text-amber-600 dark:text-amber-400" />
            )}
            {stats.dnsCount > 0 && <StatBlock label="DNS" value={stats.dnsCount} className="text-muted-foreground" />}
            {stats.dqCount > 0 && <StatBlock label="DQ" value={stats.dqCount} className="text-destructive" />}
          </div>
        </div>

        {raceSelection.isGrandPrixRace && (
          <Alert>
            <AlertDescription>
              <strong>Note:</strong> Grand Prix points and standings will be
              calculated automatically after saving.
            </AlertDescription>
          </Alert>
        )}
      </div>

      {error && (
        <Alert variant="destructive" className="mt-6">
          <AlertCircle />
          <AlertDescription>
            <strong>Error saving results:</strong> {error}
          </AlertDescription>
        </Alert>
      )}

      <div className="mt-8 flex flex-col-reverse gap-3 border-t border-border pt-6 sm:flex-row sm:justify-end">
        <Button type="button" variant="outline" onClick={onBack} disabled={saving}>
          &larr; Back
        </Button>
        <Button type="button" variant="outline" onClick={onCancel} disabled={saving}>
          Cancel
        </Button>
        <Button type="button" size="lg" onClick={handleSave} disabled={saving}>
          {saving ? (
            <>
              <Loader2 className="animate-spin" />
              Saving...
            </>
          ) : (
            'Save Results'
          )}
        </Button>
      </div>
    </div>
  );
}
