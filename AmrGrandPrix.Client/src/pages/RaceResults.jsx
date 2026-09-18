/**
 * Race Results Page
 * Shows all results for a single race, with GP points if applicable.
 */

import { useState, useEffect } from 'react';
import { Link, useParams } from 'react-router-dom';
import './pages.css';

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
  const [race, setRace] = useState(null);
  const [results, setResults] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [genderFilter, setGenderFilter] = useState('all');
  const [sortField, setSortField] = useState('place');
  const [sortDir, setSortDir] = useState('asc');

  useEffect(() => {
    const fetchData = async () => {
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
    };

    fetchData();
  }, [raceId]);

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
              </tr>
            </thead>
            <tbody>
              {filteredResults.map((r) => {
                const status = typeof r.status === 'number'
                  ? ['Finished', 'DNF', 'DNS', 'DQ'][r.status]
                  : r.status;
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
