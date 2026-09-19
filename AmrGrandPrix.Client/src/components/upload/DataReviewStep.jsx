/**
 * Step 4: Data Review & Validation
 * Editable table to review and fix data before saving
 */

import { useState, useMemo, useCallback, useEffect } from 'react';
import {
  useReactTable,
  getCoreRowModel,
  flexRender,
  createColumnHelper,
} from '@tanstack/react-table';
import * as tokenService from '../../services/tokenService';

const columnHelper = createColumnHelper();

// Must match AmrGrandPrix.API.Models.GrandPrixConstants.AgeCategories
const AGE_CATEGORIES = [
  '17 and Under', '18-29', '30-39', '40-49', '50-59', '60-69', '70-79', '80-89',
];

// Defined at module scope so it never changes reference between renders.
// Reads editingCell / setEditingCell / updateData from table.options.meta.
function EditableCell({ getValue, row, column, table }) {
  const { editingCell, setEditingCell, updateData } = table.options.meta;
  const initialValue = getValue();
  const [value, setValue] = useState(initialValue);

  const onBlur = () => {
    updateData(row.index, column.id, value);
    setEditingCell(null);
  };

  const onKeyDown = (e) => {
    if (e.key === 'Enter') {
      onBlur();
    } else if (e.key === 'Escape') {
      setValue(initialValue);
      setEditingCell(null);
    }
  };

  if (editingCell === `${row.index}-${column.id}`) {
    if (column.id === 'gender') {
      return (
        <select
          value={value}
          onChange={(e) => setValue(e.target.value)}
          onBlur={onBlur}
          autoFocus
          className="cell-input"
        >
          <option value="">-- Select --</option>
          <option value="Male">Male</option>
          <option value="Female">Female</option>
          <option value="Nonbinary">Nonbinary</option>
        </select>
      );
    }

    if (column.id === 'status') {
      return (
        <select
          value={value}
          onChange={(e) => setValue(e.target.value)}
          onBlur={onBlur}
          autoFocus
          className="cell-input"
        >
          <option value="Finished">Finished</option>
          <option value="DNF">DNF</option>
          <option value="DNS">DNS</option>
          <option value="DQ">DQ</option>
        </select>
      );
    }

    if (column.id === 'ageCategory') {
      return (
        <select
          value={value}
          onChange={(e) => setValue(e.target.value)}
          onBlur={onBlur}
          autoFocus
          className="cell-input"
        >
          <option value="">-- Unknown --</option>
          {AGE_CATEGORIES.map(cat => (
            <option key={cat} value={cat}>{cat}</option>
          ))}
        </select>
      );
    }

    return (
      <input
        type="text"
        value={value}
        onChange={(e) => setValue(e.target.value)}
        onBlur={onBlur}
        onKeyDown={onKeyDown}
        autoFocus
        className="cell-input"
      />
    );
  }

  return (
    <div
      onClick={() => setEditingCell(`${row.index}-${column.id}`)}
      className="cell-value"
    >
      {value || '-'}
    </div>
  );
}

// Dropdown for confirming/selecting which existing runner a row matches.
// Always shows "+ New Runner" plus any suggested matches, sorted by confidence.
function RunnerMatchCell({ row, table }) {
  const { updateRunnerMatch, updateData } = table.options.meta;
  const matches = row.original.runnerMatches || [];
  const selected = row.original.matchedRunnerId || '';

  if (matches.length === 0) {
    return <span className="cell-value muted-text">New runner</span>;
  }

  const selectedMatch = matches.find(m => m.runnerId === selected);
  const uploadedAge = row.original.age === '' ? null : Number(row.original.age);
  const hasAgeDiscrepancy =
    !!selectedMatch && uploadedAge != null && selectedMatch.age != null && uploadedAge !== selectedMatch.age;

  return (
    <div className="runner-match-cell">
      <select
        value={selected}
        onChange={(e) => updateRunnerMatch(row.index, e.target.value || null)}
        className="cell-input runner-match-select"
      >
        <option value="">+ New Runner</option>
        {matches.map(m => (
          <option key={m.runnerId} value={m.runnerId}>
            {m.firstName} {m.lastName} · age {m.age ?? '?'}{m.age != null && !m.hasVerifiedDateOfBirth ? ' (est.)' : ''} · {Math.round(m.confidence * 100)}%
          </option>
        ))}
      </select>
      {hasAgeDiscrepancy && (
        selectedMatch.hasVerifiedDateOfBirth ? (
          <span
            className="age-discrepancy-toggle age-discrepancy-locked"
            title="This runner has a verified date of birth and can't be overwritten here"
          >
            Verified age on file ({selectedMatch.age}) — uploaded age {uploadedAge} won't change it
          </span>
        ) : (
          <label
            className="age-discrepancy-toggle"
            title="The uploaded age differs from this runner's estimated age"
          >
            <input
              type="checkbox"
              checked={!!row.original.updateRunnerAge}
              onChange={(e) => updateData(row.index, 'updateRunnerAge', e.target.checked)}
            />
            Update estimated age ({selectedMatch.age} → {uploadedAge})
          </label>
        )
      )}
    </div>
  );
}

// Race resolver shown for each course-variant group that isn't the primary
// (pre-selected in Step 1) race — pick an existing race or create a new one.
function GroupRaceSelector({ groupKey, primaryRaceSelection, resolved, onResolve }) {
  const [races, setRaces] = useState([]);
  const [loading, setLoading] = useState(true);
  const [mode, setMode] = useState('select');
  const [newRace, setNewRace] = useState({
    name: primaryRaceSelection.raceName ? `${primaryRaceSelection.raceName} - ${groupKey}` : groupKey,
    date: primaryRaceSelection.raceDate || new Date().toISOString().split('T')[0],
    isGrandPrixRace: false,
    courseVariant: groupKey,
  });
  const [creating, setCreating] = useState(false);
  const [error, setError] = useState(null);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const token = tokenService.getAccessToken();
        const year = primaryRaceSelection.raceDate
          ? new Date(primaryRaceSelection.raceDate).getFullYear()
          : new Date().getFullYear();
        const res = await fetch(`/api/races/${year}`, {
          headers: { Authorization: `Bearer ${token}` },
        });
        if (res.ok) {
          const list = await res.json();
          if (!cancelled) setRaces(list);
        }
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => { cancelled = true; };
  }, [primaryRaceSelection.raceDate]);

  const handleSelect = (e) => {
    const value = e.target.value;
    if (value === 'new') {
      setMode('new');
      onResolve(null);
    } else if (value) {
      setMode('select');
      const race = races.find(r => r.raceId === value);
      onResolve({ raceId: value, raceName: race?.name || value, isGrandPrixRace: !!race?.isGrandPrixRace });
    } else {
      setMode('select');
      onResolve(null);
    }
  };

  const handleCreate = async () => {
    try {
      setCreating(true);
      setError(null);
      const token = tokenService.getAccessToken();
      const res = await fetch('/api/races', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${token}` },
        body: JSON.stringify({
          name: newRace.name,
          date: newRace.date,
          isGrandPrixRace: newRace.isGrandPrixRace,
          courseVariant: newRace.courseVariant || null,
          location: null,
        }),
      });
      if (!res.ok) throw new Error('Failed to create race');
      const created = await res.json();
      onResolve({ raceId: created.raceId, raceName: created.name, isGrandPrixRace: !!created.isGrandPrixRace });
    } catch (err) {
      setError(err.message);
    } finally {
      setCreating(false);
    }
  };

  return (
    <div className="group-race-resolver">
      <label>Race for &ldquo;{groupKey}&rdquo;:</label>
      {loading ? (
        <span className="muted-text">Loading races…</span>
      ) : (
        <select
          value={mode === 'new' ? 'new' : (resolved?.raceId || '')}
          onChange={handleSelect}
          className="cell-input"
        >
          <option value="">-- Select a race --</option>
          {races.map(r => (
            <option key={r.raceId} value={r.raceId}>
              {r.name} - {new Date(r.date).toLocaleDateString()}
              {r.courseVariant ? ` (${r.courseVariant})` : ''}
            </option>
          ))}
          <option value="new">+ Create New Race</option>
        </select>
      )}
      {mode === 'new' && (
        <div className="group-race-new-fields">
          <input
            type="text"
            value={newRace.name}
            onChange={(e) => setNewRace({ ...newRace, name: e.target.value })}
            placeholder="Race name"
            disabled={!!resolved}
          />
          <input
            type="date"
            value={newRace.date}
            onChange={(e) => setNewRace({ ...newRace, date: e.target.value })}
            disabled={!!resolved}
          />
          <label>
            <input
              type="checkbox"
              checked={newRace.isGrandPrixRace}
              onChange={(e) => setNewRace({ ...newRace, isGrandPrixRace: e.target.checked })}
              disabled={!!resolved}
            />
            Grand Prix race
          </label>
          <input
            type="text"
            value={newRace.courseVariant}
            onChange={(e) => setNewRace({ ...newRace, courseVariant: e.target.value })}
            placeholder="Course variant"
            disabled={!!resolved}
          />
          <button
            type="button"
            onClick={handleCreate}
            disabled={creating || !!resolved}
            className="btn-secondary"
          >
            {resolved ? 'Race Created ✓' : (creating ? 'Creating…' : 'Create Race')}
          </button>
          {error && <span className="field-error">{error}</span>}
        </div>
      )}
    </div>
  );
}

const COLUMNS = [
  columnHelper.accessor('place',  { header: 'Place',  cell: EditableCell, size: 80 }),
  columnHelper.accessor('bib',    { header: 'Bib',    cell: EditableCell, size: 80 }),
  columnHelper.accessor('name',   { header: 'Name',   cell: EditableCell, size: 200 }),
  columnHelper.accessor('age',    { header: 'Age',    cell: EditableCell, size: 80 }),
  columnHelper.accessor('ageCategory', { header: 'Age Category', cell: EditableCell, size: 120 }),
  columnHelper.accessor('gender', { header: 'Gender', cell: EditableCell, size: 100 }),
  columnHelper.accessor('time',   { header: 'Time',   cell: EditableCell, size: 120 }),
  columnHelper.accessor('status', { header: 'Status', cell: EditableCell, size: 100 }),
  columnHelper.accessor('courseVariant', { header: 'Course Variant', cell: EditableCell, size: 140 }),
  columnHelper.display({
    id: 'runnerMatch',
    header: 'Runner Match',
    cell: RunnerMatchCell,
    size: 220,
  }),
  columnHelper.display({
    id: 'issues',
    header: 'Issues',
    cell: ({ row }) => {
      const issues = row.original.validationIssues || [];
      const isNewRunner = row.original.isNewRunner;
      const needsReview = isNewRunner && (row.original.runnerMatches || []).length > 0;

      if (issues.length === 0 && !isNewRunner) {
        return <span className="status-ok">✓</span>;
      }

      return (
        <div className="issue-indicators">
          {issues.map((issue, idx) => (
            <span
              key={idx}
              className={`issue-badge ${issue.severity}`}
              title={issue.message}
            >
              ⚠
            </span>
          ))}
          {needsReview && (
            <span className="issue-badge review" title="Possible match found — confirm in Runner Match column">
              ❓
            </span>
          )}
          {isNewRunner && !needsReview && (
            <span className="issue-badge info" title="New runner">
              ℹ
            </span>
          )}
        </div>
      );
    },
    size: 80,
  }),
];

export default function DataReviewStep({ wizardData, onNext, onBack, onCancel }) {
  const [data, setData] = useState(() => {
    const parsedResults = wizardData.parsedResults || [];

    return parsedResults.map((row, idx) => ({
      id: idx,
      name:   row.name   || '',
      age:    row.age    ?? '',
      ageCategory: row.ageCategory ?? '',
      place:  row.place  ?? '',
      // Use timeString (original LLM-extracted string) for display/editing
      time:   row.timeString || '',
      gender: row.gender || '',
      bib:    row.bib    ?? '',
      status: row.status || 'Finished',
      courseVariant: row.courseVariant || '',
      validationIssues: row.validationIssues || [],
      runnerMatches: row.runnerMatches || [],
      // No matched runner → will be created as new
      isNewRunner: (row.runnerMatches || []).length === 0 && !row.matchedRunnerId,
      matchedRunnerId: row.matchedRunnerId || null,
      updateRunnerAge: false,
    }));
  });

  const [editingCell, setEditingCell] = useState(null);
  // Course-variant group key -> { raceId, raceName } chosen via GroupRaceSelector.
  // The "primary" group (pre-selected race from Step 1) never needs an entry here.
  const [groupRaces, setGroupRaces] = useState(wizardData.groupRaces || {});

  const updateData = useCallback((rowIndex, columnId, value) => {
    setData(old =>
      old.map((row, index) =>
        index === rowIndex ? { ...row, [columnId]: value } : row
      )
    );
  }, []);

  // Called when the user picks a suggested runner (or "+ New Runner") from the dropdown.
  const updateRunnerMatch = useCallback((rowIndex, runnerId) => {
    setData(old =>
      old.map((row, index) =>
        index === rowIndex
          ? { ...row, matchedRunnerId: runnerId, isNewRunner: !runnerId, updateRunnerAge: false }
          : row
      )
    );
  }, []);

  const table = useReactTable({
    data,
    columns: COLUMNS,
    getCoreRowModel: getCoreRowModel(),
    meta: { editingCell, setEditingCell, updateData, updateRunnerMatch },
  });

  const stats = useMemo(() => ({
    totalResults: data.length,
    warnings: data.filter(row => row.validationIssues?.length > 0).length,
    newRunners: data.filter(row => row.isNewRunner).length,
    needsReview: data.filter(row => row.isNewRunner && (row.runnerMatches || []).length > 0).length,
  }), [data]);

  const primaryRaceSelection = wizardData.raceSelection || {};

  // Group rows by detected course variant. A blank/untagged variant belongs to the
  // primary race pre-selected in Step 1; any other distinct variant is a separate
  // group the admin must route to its own race (existing or new) below.
  const rowGroupsMap = new Map();
  for (const row of table.getRowModel().rows) {
    const key = (row.original.courseVariant || '').trim();
    if (!rowGroupsMap.has(key)) rowGroupsMap.set(key, []);
    rowGroupsMap.get(key).push(row);
  }
  const rowGroups = Array.from(rowGroupsMap.entries());
  const primaryKey = rowGroups.some(([key]) => key === '') ? '' : (rowGroups[0]?.[0] ?? '');
  const hasMultipleGroups = rowGroups.length > 1;

  const renderTableBody = (rows) => (
    <tbody>
      {rows.map(row => (
        <tr
          key={row.id}
          className={
            row.original.validationIssues?.length > 0
              ? 'row-warning'
              : row.original.isNewRunner
                ? 'row-info'
                : ''
          }
        >
          {row.getVisibleCells().map(cell => (
            <td key={cell.id}>
              {flexRender(cell.column.columnDef.cell, cell.getContext())}
            </td>
          ))}
        </tr>
      ))}
    </tbody>
  );

  const tableHead = (
    <thead>
      {table.getHeaderGroups().map(headerGroup => (
        <tr key={headerGroup.id}>
          {headerGroup.headers.map(header => (
            <th key={header.id} style={{ width: header.getSize() }}>
              {flexRender(header.column.columnDef.header, header.getContext())}
            </th>
          ))}
        </tr>
      ))}
    </thead>
  );

  const handleSubmit = () => {
    const invalidRows = data.filter(
      row => !row.name || (!row.age && !row.ageCategory) || !row.place || !row.time
    );

    if (invalidRows.length > 0) {
      alert(
        `${invalidRows.length} row(s) are missing required fields (Name, Age or Age Category, Place, Time). Please fix these issues before continuing.`
      );
      return;
    }

    const unresolvedGroups = rowGroups
      .map(([key]) => key)
      .filter(key => key !== primaryKey && !groupRaces[key]?.raceId);

    if (unresolvedGroups.length > 0) {
      alert(
        `Please select or create a race for the following detected course variant(s): ${unresolvedGroups.join(', ')}`
      );
      return;
    }

    onNext({ reviewedData: data, groupRaces, primaryGroupKey: primaryKey });
  };

  return (
    <div className="wizard-step">
      <div className="step-header">
        <h2>Step 3: Data Review</h2>
        <span className="step-indicator">Step 3 of {wizardData.totalSteps || 4}</span>
      </div>

      <div className="review-summary">
        <div className="summary-stats">
          <div className="stat">
            <span className="stat-label">Total Results:</span>
            <span className="stat-value">{stats.totalResults}</span>
          </div>
          {stats.warnings > 0 && (
            <div className="stat">
              <span className="stat-label">Warnings:</span>
              <span className="stat-value warning">{stats.warnings}</span>
            </div>
          )}
          {stats.newRunners > 0 && (
            <div className="stat">
              <span className="stat-label">New Runners:</span>
              <span className="stat-value info">{stats.newRunners}</span>
            </div>
          )}
          {stats.needsReview > 0 && (
            <div className="stat">
              <span className="stat-label">Possible Matches to Confirm:</span>
              <span className="stat-value review">{stats.needsReview}</span>
            </div>
          )}
        </div>
        <p className="review-hint">
          Click any cell to edit. Press Enter to save, Escape to cancel.
          {stats.needsReview > 0 && ' Use the Runner Match column to confirm or reject suggested matches.'}
        </p>
      </div>

      {!hasMultipleGroups && (
        <div className="table-container">
          <table className="data-review-table">
            {tableHead}
            {renderTableBody(rowGroups[0]?.[1] || [])}
          </table>
        </div>
      )}

      {hasMultipleGroups && rowGroups.map(([key, rows]) => (
        <div className="course-group" key={key || '(primary)'}>
          <div className="course-group-header">
            <h3>{key || primaryRaceSelection.raceName || 'Primary race'}</h3>
            {key === primaryKey ? (
              <span className="group-resolved-badge">
                → {primaryRaceSelection.raceName} (Step 1 selection)
              </span>
            ) : (
              <GroupRaceSelector
                groupKey={key}
                primaryRaceSelection={primaryRaceSelection}
                resolved={groupRaces[key] || null}
                onResolve={(resolved) =>
                  setGroupRaces(prev => {
                    const next = { ...prev };
                    if (resolved) next[key] = resolved;
                    else delete next[key];
                    return next;
                  })
                }
              />
            )}
          </div>
          <div className="table-container">
            <table className="data-review-table">
              {tableHead}
              {renderTableBody(rows)}
            </table>
          </div>
        </div>
      ))}

      <div className="form-actions">
        <button type="button" onClick={onBack} className="btn-secondary">← Back</button>
        <button type="button" onClick={onCancel} className="btn-secondary">Cancel</button>
        <button type="button" onClick={handleSubmit} className="btn-primary">Next →</button>
      </div>
    </div>
  );
}
