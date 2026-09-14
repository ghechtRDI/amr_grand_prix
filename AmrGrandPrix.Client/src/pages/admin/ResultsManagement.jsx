/**
 * Admin Results Management
 * Lists upload batches, allows deletion and GP recalculation.
 */

import { useState, useEffect, useCallback } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import * as tokenService from '../../services/tokenService';
import '../pages.css';

const CURRENT_YEAR = new Date().getFullYear();
const YEAR_OPTIONS = Array.from({ length: 5 }, (_, i) => CURRENT_YEAR - i);

function fileTypeLabel(fileType) {
  if (typeof fileType === 'number') {
    return ['CSV', 'Excel', 'PDF', 'Text'][fileType] ?? fileType;
  }
  return fileType;
}

function statusLabel(status) {
  if (typeof status === 'number') {
    return ['Pending', 'Validated', 'Saved', 'Cancelled'][status] ?? status;
  }
  return status;
}

export default function ResultsManagement() {
  const navigate = useNavigate();
  const [batches, setBatches] = useState([]);
  const [year, setYear] = useState(CURRENT_YEAR);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [deleting, setDeleting] = useState(null);
  const [recalculating, setRecalculating] = useState(false);
  const [message, setMessage] = useState(null);

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
    if (!confirm(`Delete all ${batch.recordsUploaded} results from "${batch.raceName}" (${batch.fileName})?`)) return;

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
    }
  };

  const handleRecalculate = async () => {
    if (!confirm(`Recalculate all Grand Prix standings for ${year}?`)) return;

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
    <div className="page-container">
      <div className="page-header">
        <div className="header-row">
          <h1>Results Management</h1>
          <div className="header-actions">
            <select
              value={year}
              onChange={(e) => setYear(parseInt(e.target.value))}
              className="select-input"
            >
              {YEAR_OPTIONS.map((y) => (
                <option key={y} value={y}>{y}</option>
              ))}
            </select>
            <button
              onClick={handleRecalculate}
              className="btn-secondary"
              disabled={recalculating}
            >
              {recalculating ? 'Recalculating...' : `Recalculate GP Standings`}
            </button>
            <button
              onClick={() => navigate('/admin/results/upload')}
              className="btn-primary"
            >
              Upload Results
            </button>
          </div>
        </div>
      </div>

      <div className="page-content">
        {message && (
          <div className={`alert ${message.type === 'success' ? 'alert-success' : 'alert-error'}`}>
            {message.text}
            <button className="alert-close" onClick={() => setMessage(null)}>✕</button>
          </div>
        )}

        {loading && <div className="loading-state">Loading...</div>}
        {error && <div className="error-banner">{error}</div>}

        {!loading && !error && races.length === 0 && (
          <div className="empty-state">
            No uploaded results for {year}.{' '}
            <Link to="/admin/results/upload">Upload some results.</Link>
          </div>
        )}

        {!loading && races.map((race) => (
          <div key={race.raceId} className="race-card">
            <div className="race-card-header">
              <div className="race-card-title">
                <h3>
                  <Link to={`/races/${race.raceId}/results`}>{race.raceName}</Link>
                </h3>
                <span className="race-card-date">
                  {new Date(race.raceDate).toLocaleDateString('en-US', {
                    year: 'numeric', month: 'short', day: 'numeric',
                  })}
                </span>
                {race.isGrandPrixRace && <span className="gp-badge">Grand Prix</span>}
              </div>
              <Link to={`/races/${race.raceId}/results`} className="btn-secondary btn-sm">
                View Results
              </Link>
            </div>

            <table className="data-table batch-table">
              <thead>
                <tr>
                  <th>File</th>
                  <th>Type</th>
                  <th>Records</th>
                  <th>Uploaded</th>
                  <th>Status</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                {race.batches.map((b) => (
                  <tr key={b.uploadBatchId}>
                    <td className="filename-cell" title={b.fileName}>{b.fileName}</td>
                    <td>{fileTypeLabel(b.fileType)}</td>
                    <td>{b.recordsUploaded}</td>
                    <td>{new Date(b.uploadedAt).toLocaleString()}</td>
                    <td>
                      <span className={`status-badge status-${statusLabel(b.status).toLowerCase()}`}>
                        {statusLabel(b.status)}
                      </span>
                    </td>
                    <td>
                      <button
                        className="btn-danger btn-sm"
                        onClick={() => handleDelete(b)}
                        disabled={deleting === b.uploadBatchId}
                      >
                        {deleting === b.uploadBatchId ? 'Deleting...' : 'Delete'}
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ))}

        <div className="page-footer-links">
          <Link to="/">← Home</Link>
          <Link to={`/standings/${year}`}>View Standings →</Link>
        </div>
      </div>
    </div>
  );
}
