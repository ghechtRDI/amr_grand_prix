/**
 * Step 5: Confirmation & Save
 * Final review and save results to database
 */

import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import * as tokenService from '../../services/tokenService';

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
  const primaryGroupKey = wizardData.primaryGroupKey ?? '';

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
    return Array.from(byKey.entries()).map(([key, rows]) => {
      const isPrimary = key === primaryGroupKey;
      return {
        key,
        rows,
        raceId: isPrimary ? raceSelection.raceId : groupRaces[key]?.raceId,
        raceName: isPrimary ? raceSelection.raceName : (groupRaces[key]?.raceName || key),
        isGrandPrixRace: isPrimary ? !!raceSelection.isGrandPrixRace : !!groupRaces[key]?.isGrandPrixRace,
        uploadBatchId: isPrimary ? uploadBatchId : null,
        sourceUploadBatchId: isPrimary ? null : uploadBatchId,
      };
    });
  })();

  // Calculate statistics
  const stats = {
    totalResults: reviewedData.length,
    newRunners: reviewedData.filter(r => r.isNewRunner).length,
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
      <div className="wizard-step">
        <div className="step-header">
          <h2>Success!</h2>
          <span className="step-indicator">Step 4 of 4</span>
        </div>

        <div className="success-message">
          <div className="success-icon">✓</div>
          <h3>
            {groupResults.length > 1 ? 'Results saved for all course variants!' : 'Results saved successfully!'}
          </h3>
        </div>

        {groupResults.map(({ group, success: groupSuccess, result, error: groupError }) => (
          <div key={group.key || '(primary)'} className="summary-section">
            <h4>{group.raceName}{group.key ? ` (${group.key})` : ''}</h4>
            {groupSuccess ? (
              <>
                <p>{result.resultsSaved} result(s) saved.{group.isGrandPrixRace && (
                  <span className="gp-notice"> Grand Prix standings have been updated automatically.</span>
                )}</p>
                {result.skippedResults?.length > 0 && (
                  <div className="summary-notice skipped-results-notice">
                    <strong>{result.skippedResults.length} result(s) were not saved:</strong>
                    <ul className="skipped-results-list">
                      {result.skippedResults.map((row) => (
                        <li key={row.rowNumber}>
                          Row {row.rowNumber} — {row.name || '(no name)'}: {row.reason}
                        </li>
                      ))}
                    </ul>
                  </div>
                )}
                <button
                  type="button"
                  onClick={() => handleViewResults(result.raceId)}
                  className="btn-secondary"
                >
                  View {group.raceName} Results
                </button>
              </>
            ) : (
              <div className="error-message">
                <strong>Error saving this group:</strong> {groupError}
              </div>
            )}
          </div>
        ))}

        <div className="success-actions">
          {primaryResult?.success && (
            <button
              type="button"
              onClick={() => handleViewResults(primaryResult.result.raceId)}
              className="btn-primary"
            >
              View Race Results
            </button>
          )}
          {anyGpRace && (
            <button
              type="button"
              onClick={handleViewStandings}
              className="btn-secondary"
            >
              View GP Standings
            </button>
          )}
          <button
            type="button"
            onClick={() => navigate('/admin/results')}
            className="btn-secondary"
          >
            Upload More Results
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="wizard-step">
      <div className="step-header">
        <h2>Step 5: Confirmation</h2>
        <span className="step-indicator">Step 4 of 4</span>
      </div>

      <div className="confirmation-summary">
        <h3>Review Summary</h3>

        <div className="summary-section">
          <h4>{saveGroups.length > 1 ? 'Races (by course variant)' : 'Race Information'}</h4>
          {saveGroups.length > 1 ? (
            <ul className="skipped-results-list">
              {saveGroups.map(group => (
                <li key={group.key || '(primary)'}>
                  {group.key || '(no variant tag)'} → <strong>{group.raceName}</strong>
                  {' '}({group.rows.length} result{group.rows.length === 1 ? '' : 's'})
                  {group.isGrandPrixRace && <span className="gp-badge">Grand Prix</span>}
                </li>
              ))}
            </ul>
          ) : (
            <dl className="summary-details">
              <dt>Race:</dt>
              <dd>
                {raceSelection.raceName}
                {raceSelection.isGrandPrixRace && (
                  <span className="gp-badge">Grand Prix</span>
                )}
              </dd>

              <dt>Date:</dt>
              <dd>{new Date(raceSelection.raceDate).toLocaleDateString()}</dd>

              {raceSelection.courseVariant && (
                <>
                  <dt>Course Variant:</dt>
                  <dd>{raceSelection.courseVariant}</dd>
                </>
              )}
            </dl>
          )}
        </div>

        <div className="summary-section">
          <h4>Results Statistics</h4>
          <dl className="summary-details">
            <dt>Total Results:</dt>
            <dd>{stats.totalResults}</dd>

            <dt>Finishers:</dt>
            <dd>{stats.finishers}</dd>

            {stats.newRunners > 0 && (
              <>
                <dt>New Runners:</dt>
                <dd className="info">{stats.newRunners}</dd>
              </>
            )}

            {stats.dnfCount > 0 && (
              <>
                <dt>DNF:</dt>
                <dd>{stats.dnfCount}</dd>
              </>
            )}

            {stats.dnsCount > 0 && (
              <>
                <dt>DNS:</dt>
                <dd>{stats.dnsCount}</dd>
              </>
            )}

            {stats.dqCount > 0 && (
              <>
                <dt>DQ:</dt>
                <dd>{stats.dqCount}</dd>
              </>
            )}
          </dl>
        </div>

        {raceSelection.isGrandPrixRace && (
          <div className="summary-notice gp-notice">
            <strong>Note:</strong> Grand Prix points and standings will be
            calculated automatically after saving.
          </div>
        )}
      </div>

      {error && (
        <div className="error-message">
          <strong>Error saving results:</strong> {error}
        </div>
      )}

      <div className="form-actions">
        <button
          type="button"
          onClick={onBack}
          className="btn-secondary"
          disabled={saving}
        >
          ← Back
        </button>
        <button
          type="button"
          onClick={onCancel}
          className="btn-secondary"
          disabled={saving}
        >
          Cancel
        </button>
        <button
          type="button"
          onClick={handleSave}
          className="btn-primary btn-large"
          disabled={saving}
        >
          {saving ? (
            <>
              <span className="spinner"></span>
              Saving...
            </>
          ) : (
            'Save Results'
          )}
        </button>
      </div>
    </div>
  );
}
