/**
 * Step 5: Confirmation & Save
 * Final review and save results to database
 */

import { useState } from 'react';
import { useNavigate } from 'react-router-dom';

export default function ConfirmationStep({ wizardData, onBack, onCancel }) {
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState(null);
  const [success, setSuccess] = useState(false);
  const [savedRaceId, setSavedRaceId] = useState(null);
  const navigate = useNavigate();

  const raceSelection = wizardData.raceSelection || {};
  const reviewedData = wizardData.reviewedData || [];
  const uploadBatchId = wizardData.uploadBatchId;

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
    try {
      setSaving(true);
      setError(null);

      const token = localStorage.getItem('token');

      // Prepare payload
      const payload = {
        raceId: raceSelection.raceId,
        uploadBatchId: uploadBatchId,
        results: reviewedData.map(row => ({
          name: row.name,
          age: parseInt(row.age),
          place: row.place ? parseInt(row.place) : null,
          time: row.time,
          gender: row.gender,
          bib: row.bib ? parseInt(row.bib) : null,
          status: row.status,
        })),
        // If creating new race
        newRace: raceSelection.raceId === 'new' ? {
          name: raceSelection.raceName,
          date: raceSelection.raceDate,
          isGrandPrixRace: raceSelection.isGrandPrixRace,
          courseVariant: raceSelection.courseVariant,
        } : null,
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
      setSavedRaceId(result.raceId);
      setSuccess(true);

    } catch (err) {
      setError(err.message);
      console.error('Save error:', err);
    } finally {
      setSaving(false);
    }
  };

  const handleViewResults = () => {
    if (savedRaceId) {
      navigate(`/races/${savedRaceId}/results`);
    }
  };

  const handleViewStandings = () => {
    const year = new Date(raceSelection.raceDate).getFullYear();
    navigate(`/standings/${year}`);
  };

  if (success) {
    return (
      <div className="wizard-step">
        <div className="step-header">
          <h2>Success!</h2>
          <span className="step-indicator">Step 5 of 5</span>
        </div>

        <div className="success-message">
          <div className="success-icon">✓</div>
          <h3>Results saved successfully!</h3>
          <p>
            {stats.totalResults} results have been saved to the database.
          </p>
          {raceSelection.isGrandPrixRace && (
            <p className="gp-notice">
              Grand Prix standings have been updated automatically.
            </p>
          )}
        </div>

        <div className="success-actions">
          <button
            type="button"
            onClick={handleViewResults}
            className="btn-primary"
          >
            View Race Results
          </button>
          {raceSelection.isGrandPrixRace && (
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
        <span className="step-indicator">Step 5 of 5</span>
      </div>

      <div className="confirmation-summary">
        <h3>Review Summary</h3>

        <div className="summary-section">
          <h4>Race Information</h4>
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
