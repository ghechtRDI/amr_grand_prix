/**
 * Race Results Page
 * Shows all results for a single race, with GP points if applicable.
 */

import { useState, useEffect, useCallback } from 'react';
import { Link, useParams } from 'react-router-dom';
import { useAuth } from '../hooks/useAuth';
import * as tokenService from '../services/tokenService';
import './pages.css';

const STATUS_OPTIONS = ['Finished', 'DNF', 'DNS', 'DQ'];
const GENDER_OPTIONS = ['Male', 'Female', 'Nonbinary'];

function toStatusLabel(status) {
  return typeof status === 'number' ? STATUS_OPTIONS[status] : status;
}

function toGenderLabel(gender) {
  return typeof gender === 'number' ? GENDER_OPTIONS[gender] : gender;
}

// "1:23:45" or "23:45" formatted TimeSpan (as returned by the API) -> plain string for editing
function timeToEditableString(timeSpan) {
  if (!timeSpan) return '';
  return timeSpan.split('.')[0]; // drop fractional seconds if present
}

function formatTime(timeSpan) {
  if (!timeSpan) return '—';
  const parts = timeSpan.split(':');
  if (parts.length === 3) {
    const h = parseInt(parts[0]);
    const m = parts[1];
    const s = parseFloat(parts[2]).toFixed(0).padStart(2, '0');
    if (h === 0) return `${m}:${s}`;
    return `${h}:${h >= 0 ? m : parts[1]}:${s}`;
  }
  return timeSpan;
}

function genderLabel(g) {
  if (g === 0 || g === 'Male') return 'M';
  if (g === 1 || g === 'Female') return 'F';
  return g;
}

function statusBadge(status) {
  const s = typeof status === 'string' ? status : ['Finished', 'DNF', 'DNS', 'DQ'][status] || status;
  if (s === 'Finished' || s === 0) return null;
  return <span className={`status-badge status-${s.toLowerCase()}`}>{s}</span>;
}

export default function RaceResults() {
  const { raceId } = useParams();
  const { hasRole, hasAnyRole, isAuthenticated } = useAuth();
  const [race, setRace] = useState(null);
  const [results, setResults] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [genderFilter, setGenderFilter] = useState('all');
  const [sortField, setSortField] = useState('place');
  const [sortDir, setSortDir] = useState('asc');
  const [editingResultId, setEditingResultId] = useState(null);
  const [editValues, setEditValues] = useState({});
  const [saving, setSaving] = useState(false);
  const [deletingId, setDeletingId] = useState(null);
  const [actionError, setActionError] = useState(null);

  const canEdit = isAuthenticated() && hasAnyRole(['Admin', 'Manager']);
  const canDelete = isAuthenticated() && hasRole('Admin');

  const fetchData = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [raceRes, resultsRes] = await Promise.all([
        fetch(`/api/races/detail/${raceId}`),
        fetch(`/api/results/race/${raceId}`),
      ]);

      if (!raceRes.ok) throw new Error('Race not found');
      if (!resultsRes.ok) throw new Error('Failed to load results');

      const [raceData, resultsData] = await Promise.all([
        raceRes.json(),
        resultsRes.json(),
      ]);

      setRace(raceData);
      setResults(resultsData);
    } catch (e) {
      setError(e.message);
    } finally {
      setLoading(false);
    }
  }, [raceId]);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  const startEdit = (r) => {
    setActionError(null);
    setEditingResultId(r.resultId);
    setEditValues({
      bib: r.bib ?? '',
      place: r.place ?? '',
      time: timeToEditableString(r.time),
      age: r.age ?? '',
      ageCategory: r.ageCategory ?? '',
      gender: toGenderLabel(r.gender) ?? '',
      status: toStatusLabel(r.status) ?? 'Finished',
      notes: r.notes ?? '',
    });
  };

  const cancelEdit = () => {
    setEditingResultId(null);
    setEditValues({});
  };

  const saveEdit = async (resultId) => {
    setSaving(true);
    setActionError(null);
    try {
      const res = await fetch(`/api/results/${resultId}`, {
        method: 'PUT',
        headers: {
          'Content-Type': 'application/json',
          Authorization: `Bearer ${tokenService.getAccessToken()}`,
        },
        body: JSON.stringify({
          bib: editValues.bib === '' ? null : parseInt(editValues.bib),
          place: editValues.place === '' ? null : parseInt(editValues.place),
          timeString: editValues.time || null,
          age: editValues.age === '' ? null : parseInt(editValues.age),
          ageCategory: editValues.age === '' ? (editValues.ageCategory || null) : null,
          gender: editValues.gender,
          status: editValues.status,
          notes: editValues.notes || null,
        }),
      });

      if (!res.ok) {
        const txt = await res.text();
        throw new Error(txt || `HTTP ${res.status}`);
      }

      setEditingResultId(null);
      setEditValues({});
      await fetchData();
    } catch (e) {
      setActionError(`Failed to save changes: ${e.message}`);
    } finally {
      setSaving(false);
    }
  };

  const handleDelete = async (r) => {
    if (!window.confirm(`Delete ${r.runnerName}'s result from this race? This cannot be undone.`)) return;

    setDeletingId(r.resultId);
    setActionError(null);
    try {
      const res = await fetch(`/api/results/${r.resultId}`, {
        method: 'DELETE',
        headers: { Authorization: `Bearer ${tokenService.getAccessToken()}` },
      });

      if (!res.ok) {
        const txt = await res.text();
        throw new Error(txt || `HTTP ${res.status}`);
      }

      await fetchData();
    } catch (e) {
      setActionError(`Failed to delete result: ${e.message}`);
    } finally {
      setDeletingId(null);
    }
  };

  const handleSort = (field) => {
    if (sortField === field) {
      setSortDir((d) => (d === 'asc' ? 'desc' : 'asc'));
    } else {
      setSortField(field);
      setSortDir('asc');
    }
  };

  const sortIcon = (field) => {
    if (sortField !== field) return ' ↕';
    return sortDir === 'asc' ? ' ↑' : ' ↓';
  };

  const filteredResults = results
    .filter((r) => {
      if (genderFilter === 'all') return true;
      const g = typeof r.gender === 'number'
        ? ['Male', 'Female', 'Nonbinary'][r.gender]
        : r.gender;
      return g === genderFilter;
    })
    .sort((a, b) => {
      let valA = a[sortField];
      let valB = b[sortField];
      // nulls last
      if (valA == null) return 1;
      if (valB == null) return -1;
      if (typeof valA === 'string') valA = valA.toLowerCase();
      if (typeof valB === 'string') valB = valB.toLowerCase();
      const cmp = valA < valB ? -1 : valA > valB ? 1 : 0;
      return sortDir === 'asc' ? cmp : -cmp;
    });

  if (loading) return <div className="page-container"><div className="loading-state">Loading results...</div></div>;
  if (error) return <div className="page-container"><div className="error-banner">{error}</div></div>;

  return (
    <div className="page-container">
      <div className="page-header">
        <div className="breadcrumb">
          <Link to="/">Home</Link> / <span>Race Results</span>
        </div>
        <h1>{race?.name}</h1>
        <div className="race-meta">
          {race?.date && (
            <span>{new Date(race.date).toLocaleDateString('en-US', { year: 'numeric', month: 'long', day: 'numeric' })}</span>
          )}
          {race?.location && <span>{race.location}</span>}
          {race?.courseVariant && <span>Course: {race.courseVariant}</span>}
          {race?.isGrandPrixRace && <span className="gp-badge">Grand Prix</span>}
        </div>
        <div className="results-summary">
          <span>{results.length} results</span>
          {results.filter((r) => {
            const s = typeof r.status === 'number' ? r.status : ['Finished', 'DNF', 'DNS', 'DQ'].indexOf(r.status);
            return s === 0;
          }).length !== results.length && (
            <span>({results.filter((r) => {
              const s = typeof r.status === 'number' ? r.status : ['Finished', 'DNF', 'DNS', 'DQ'].indexOf(r.status);
              return s === 0;
            }).length} finishers)</span>
          )}
        </div>
      </div>

      <div className="page-content">
        {/* Gender filter */}
        <div className="filter-bar">
          <div className="toggle-group">
            {['all', 'Male', 'Female'].map((g) => (
              <button
                key={g}
                className={`toggle-btn ${genderFilter === g ? 'active' : ''}`}
                onClick={() => setGenderFilter(g)}
              >
                {g === 'all' ? 'All' : g}
              </button>
            ))}
          </div>
          <span className="results-count">{filteredResults.length} shown</span>
        </div>

        {actionError && <div className="error-banner">{actionError}</div>}

        <div className="table-wrapper">
          <table className="data-table">
            <thead>
              <tr>
                <th onClick={() => handleSort('place')} className="sortable">
                  Place{sortIcon('place')}
                </th>
                <th onClick={() => handleSort('placeGender')} className="sortable">
                  {genderFilter === 'all' ? 'Gender Place' : 'Place'}{sortIcon('placeGender')}
                </th>
                <th onClick={() => handleSort('runnerName')} className="sortable">
                  Name{sortIcon('runnerName')}
                </th>
                <th onClick={() => handleSort('age')} className="sortable">
                  Age{sortIcon('age')}
                </th>
                <th>Gender</th>
                <th onClick={() => handleSort('time')} className="sortable">
                  Time{sortIcon('time')}
                </th>
                <th>Status</th>
                {race?.isGrandPrixRace && <th>Record</th>}
                {(canEdit || canDelete) && <th>Actions</th>}
              </tr>
            </thead>
            <tbody>
              {filteredResults.map((r) => {
                const status = typeof r.status === 'number'
                  ? ['Finished', 'DNF', 'DNS', 'DQ'][r.status]
                  : r.status;
                const isEditing = editingResultId === r.resultId;

                if (isEditing) {
                  return (
                    <tr key={r.resultId} className="row-editing">
                      <td>
                        <input type="text" value={editValues.place} className="cell-input"
                          onChange={(e) => setEditValues(v => ({ ...v, place: e.target.value }))} />
                      </td>
                      <td>{r.placeGender ?? '—'}</td>
                      <td className="name-cell">
                        <Link to={`/runners/${r.runnerId}`}>{r.runnerName}</Link>
                      </td>
                      <td>
                        <input type="text" value={editValues.age} className="cell-input" placeholder="Age"
                          onChange={(e) => setEditValues(v => ({ ...v, age: e.target.value }))} />
                      </td>
                      <td>
                        <select value={editValues.gender} className="cell-input"
                          onChange={(e) => setEditValues(v => ({ ...v, gender: e.target.value }))}>
                          {GENDER_OPTIONS.map(g => <option key={g} value={g}>{g}</option>)}
                        </select>
                      </td>
                      <td>
                        <input type="text" value={editValues.time} className="cell-input" placeholder="h:mm:ss"
                          onChange={(e) => setEditValues(v => ({ ...v, time: e.target.value }))} />
                      </td>
                      <td>
                        <select value={editValues.status} className="cell-input"
                          onChange={(e) => setEditValues(v => ({ ...v, status: e.target.value }))}>
                          {STATUS_OPTIONS.map(s => <option key={s} value={s}>{s}</option>)}
                        </select>
                      </td>
                      {race?.isGrandPrixRace && <td></td>}
                      <td className="row-actions">
                        <button className="btn-primary btn-sm" disabled={saving} onClick={() => saveEdit(r.resultId)}>
                          {saving ? 'Saving...' : 'Save'}
                        </button>
                        <button className="btn-secondary btn-sm" disabled={saving} onClick={cancelEdit}>
                          Cancel
                        </button>
                      </td>
                    </tr>
                  );
                }

                return (
                  <tr key={r.resultId} className={status !== 'Finished' ? 'dnf-row' : ''}>
                    <td className="place-cell">{r.place ?? '—'}</td>
                    <td>{r.placeGender ?? '—'}</td>
                    <td className="name-cell">
                      <Link to={`/runners/${r.runnerId}`}>{r.runnerName}</Link>
                    </td>
                    <td>{r.age ?? r.ageCategory ?? '—'}</td>
                    <td>{genderLabel(r.gender)}</td>
                    <td className="time-cell">{formatTime(r.time)}</td>
                    <td>{statusBadge(r.status)}</td>
                    {race?.isGrandPrixRace && (
                      <td>{r.isNewRecord ? <span className="record-badge">CR</span> : ''}</td>
                    )}
                    {(canEdit || canDelete) && (
                      <td className="row-actions">
                        {canEdit && (
                          <button className="btn-secondary btn-sm" onClick={() => startEdit(r)}>
                            Edit
                          </button>
                        )}
                        {canDelete && (
                          <button
                            className="btn-danger btn-sm"
                            disabled={deletingId === r.resultId}
                            onClick={() => handleDelete(r)}
                          >
                            {deletingId === r.resultId ? 'Deleting...' : 'Delete'}
                          </button>
                        )}
                      </td>
                    )}
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>

        <div className="page-footer-links">
          <Link to="/">← Home</Link>
          {race?.isGrandPrixRace && (
            <Link to={`/standings/${race.year}`}>View GP Standings →</Link>
          )}
        </div>
      </div>
    </div>
  );
}
