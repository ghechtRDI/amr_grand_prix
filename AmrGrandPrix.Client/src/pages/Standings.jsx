/**
 * Grand Prix Standings Dashboard
 * Shows standings for Open and Age divisions, filterable by year.
 */

import { useState, useEffect, useCallback } from 'react';
import { Link, useParams, useNavigate } from 'react-router-dom';
import './pages.css';

const AGE_CATEGORIES = [
  '17 and Under',
  '19-29',
  '30-39',
  '40-49',
  '50-59',
  '60-69',
  '70-79',
  '80-89',
];

const DIVISION_OPEN = 'open';
const DIVISION_AGE = 'age';

function StandingsTable({ standings, showAgeCategory }) {
  if (!standings || standings.length === 0) {
    return <p className="empty-state">No standings data for this division.</p>;
  }

  return (
    <div className="table-wrapper">
      <table className="data-table">
        <thead>
          <tr>
            <th>Rank</th>
            <th>Runner</th>
            {showAgeCategory && <th>Age Group</th>}
            <th>Total Points</th>
            <th>Races</th>
            <th>Best</th>
            <th>2nd</th>
            <th>Gamut</th>
          </tr>
        </thead>
        <tbody>
          {standings.map((s) => (
            <tr key={s.standingId} className={s.runTheGamutQualified ? 'gamut-row' : ''}>
              <td className="rank-cell">{s.rank}</td>
              <td className="name-cell">
                <Link to={`/runners/${s.runnerId}`}>{s.runnerName}</Link>
              </td>
              {showAgeCategory && <td>{s.ageCategory || '—'}</td>}
              <td className="points-cell">{s.totalPoints}</td>
              <td>{s.racesCounted}/{s.racesCompleted}</td>
              <td>{s.bestRacePoints || '—'}</td>
              <td>{s.secondBestRacePoints || '—'}</td>
              <td className="gamut-cell">{s.runTheGamutQualified ? '✓' : ''}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

const CURRENT_YEAR = new Date().getFullYear();

export default function Standings() {
  const { year: yearParam } = useParams();
  const navigate = useNavigate();

  const [availableYears, setAvailableYears] = useState([]);
  const [selectedYear, setSelectedYear] = useState(parseInt(yearParam) || CURRENT_YEAR);
  const [mainTab, setMainTab] = useState(DIVISION_OPEN);
  const [genderTab, setGenderTab] = useState('male');
  const [ageCategory, setAgeCategory] = useState(AGE_CATEGORIES[2]); // default 30-39
  const [ageGender, setAgeGender] = useState('male');

  const [standings, setStandings] = useState([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);

  useEffect(() => {
    fetch('/api/standings/years')
      .then((r) => r.json())
      .then((years) => {
        if (years.length === 0) {
          setAvailableYears([CURRENT_YEAR]);
        } else {
          setAvailableYears(years);
          if (!yearParam) setSelectedYear(years[0]);
        }
      })
      .catch(() => setAvailableYears([CURRENT_YEAR]));
  }, [yearParam]);

  const loadStandings = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      let url;
      if (mainTab === DIVISION_OPEN) {
        url = `/api/standings/${selectedYear}/open/${genderTab}`;
      } else {
        const encoded = encodeURIComponent(ageCategory);
        url = `/api/standings/${selectedYear}/age/${encoded}/${ageGender}`;
      }

      const res = await fetch(url);
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      const data = await res.json();
      setStandings(data.standings || []);
    } catch (e) {
      setError(e.message);
      setStandings([]);
    } finally {
      setLoading(false);
    }
  }, [selectedYear, mainTab, genderTab, ageCategory, ageGender]);

  useEffect(() => {
    loadStandings();
  }, [loadStandings]);

  const handleYearChange = (y) => {
    setSelectedYear(parseInt(y));
    navigate(`/standings/${y}`, { replace: true });
  };

  const divisionLabel = () => {
    if (mainTab === DIVISION_OPEN) {
      return `Open ${genderTab === 'male' ? 'Male' : 'Female'} Division`;
    }
    return `${ageCategory} ${ageGender === 'male' ? 'Male' : 'Female'}`;
  };

  return (
    <div className="page-container">
      <div className="page-header">
        <div className="header-row">
          <h1>Grand Prix Standings</h1>
          <div className="year-selector">
            <label htmlFor="year-select">Year:</label>
            <select
              id="year-select"
              value={selectedYear}
              onChange={(e) => handleYearChange(e.target.value)}
              className="select-input"
            >
              {availableYears.map((y) => (
                <option key={y} value={y}>{y}</option>
              ))}
            </select>
          </div>
        </div>
      </div>

      <div className="page-content">
        {/* Main division tabs */}
        <div className="tab-bar">
          <button
            className={`tab-btn ${mainTab === DIVISION_OPEN ? 'active' : ''}`}
            onClick={() => setMainTab(DIVISION_OPEN)}
          >
            Open Division
          </button>
          <button
            className={`tab-btn ${mainTab === DIVISION_AGE ? 'active' : ''}`}
            onClick={() => setMainTab(DIVISION_AGE)}
          >
            Age Divisions
          </button>
        </div>

        {/* Open division controls */}
        {mainTab === DIVISION_OPEN && (
          <div className="sub-controls">
            <div className="toggle-group">
              <button
                className={`toggle-btn ${genderTab === 'male' ? 'active' : ''}`}
                onClick={() => setGenderTab('male')}
              >
                Male
              </button>
              <button
                className={`toggle-btn ${genderTab === 'female' ? 'active' : ''}`}
                onClick={() => setGenderTab('female')}
              >
                Female
              </button>
            </div>
          </div>
        )}

        {/* Age division controls */}
        {mainTab === DIVISION_AGE && (
          <div className="sub-controls age-controls">
            <div className="form-row">
              <label htmlFor="age-cat">Age Group:</label>
              <select
                id="age-cat"
                value={ageCategory}
                onChange={(e) => setAgeCategory(e.target.value)}
                className="select-input"
              >
                {AGE_CATEGORIES.map((cat) => (
                  <option key={cat} value={cat}>{cat}</option>
                ))}
              </select>
            </div>
            <div className="toggle-group">
              <button
                className={`toggle-btn ${ageGender === 'male' ? 'active' : ''}`}
                onClick={() => setAgeGender('male')}
              >
                Male
              </button>
              <button
                className={`toggle-btn ${ageGender === 'female' ? 'active' : ''}`}
                onClick={() => setAgeGender('female')}
              >
                Female
              </button>
            </div>
          </div>
        )}

        {/* Table header */}
        <div className="section-header">
          <h2>{selectedYear} — {divisionLabel()}</h2>
          <p className="section-hint">
            Best 4 races count toward total. ✓ = Run the Gamut (7+ races).
          </p>
        </div>

        {loading && <div className="loading-state">Loading standings...</div>}
        {error && <div className="error-banner">Error loading standings: {error}</div>}
        {!loading && !error && (
          <StandingsTable
            standings={standings}
            showAgeCategory={mainTab === DIVISION_AGE}
          />
        )}

        <div className="page-footer-links">
          <Link to="/">← Home</Link>
        </div>
      </div>
    </div>
  );
}
