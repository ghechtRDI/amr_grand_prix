/**
 * Step 4: Data Review & Validation
 * Editable table to review and fix data before saving
 */

import { useState, useMemo, useCallback } from 'react';
import {
  useReactTable,
  getCoreRowModel,
  flexRender,
  createColumnHelper,
} from '@tanstack/react-table';

const columnHelper = createColumnHelper();

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
  const { updateRunnerMatch } = table.options.meta;
  const matches = row.original.runnerMatches || [];
  const selected = row.original.matchedRunnerId || '';

  if (matches.length === 0) {
    return <span className="cell-value muted-text">New runner</span>;
  }

  return (
    <select
      value={selected}
      onChange={(e) => updateRunnerMatch(row.index, e.target.value || null)}
      className="cell-input runner-match-select"
    >
      <option value="">+ New Runner</option>
      {matches.map(m => (
        <option key={m.runnerId} value={m.runnerId}>
          {m.firstName} {m.lastName} · age {m.age ?? '?'} · {Math.round(m.confidence * 100)}%
        </option>
      ))}
    </select>
  );
}

const COLUMNS = [
  columnHelper.accessor('place',  { header: 'Place',  cell: EditableCell, size: 80 }),
  columnHelper.accessor('bib',    { header: 'Bib',    cell: EditableCell, size: 80 }),
  columnHelper.accessor('name',   { header: 'Name',   cell: EditableCell, size: 200 }),
  columnHelper.accessor('age',    { header: 'Age',    cell: EditableCell, size: 80 }),
  columnHelper.accessor('gender', { header: 'Gender', cell: EditableCell, size: 100 }),
  columnHelper.accessor('time',   { header: 'Time',   cell: EditableCell, size: 120 }),
  columnHelper.accessor('status', { header: 'Status', cell: EditableCell, size: 100 }),
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
      place:  row.place  ?? '',
      // Use timeString (original LLM-extracted string) for display/editing
      time:   row.timeString || '',
      gender: row.gender || '',
      bib:    row.bib    ?? '',
      status: row.status || 'Finished',
      validationIssues: row.validationIssues || [],
      runnerMatches: row.runnerMatches || [],
      // No matched runner → will be created as new
      isNewRunner: (row.runnerMatches || []).length === 0 && !row.matchedRunnerId,
      matchedRunnerId: row.matchedRunnerId || null,
    }));
  });

  const [editingCell, setEditingCell] = useState(null);

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
          ? { ...row, matchedRunnerId: runnerId, isNewRunner: !runnerId }
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

  const handleSubmit = () => {
    const invalidRows = data.filter(
      row => !row.name || !row.age || !row.place || !row.time
    );

    if (invalidRows.length > 0) {
      alert(
        `${invalidRows.length} row(s) are missing required fields (Name, Age, Place, Time). Please fix these issues before continuing.`
      );
      return;
    }

    onNext({ reviewedData: data });
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

      <div className="table-container">
        <table className="data-review-table">
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
          <tbody>
            {table.getRowModel().rows.map(row => (
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
        </table>
      </div>

      <div className="form-actions">
        <button type="button" onClick={onBack} className="btn-secondary">← Back</button>
        <button type="button" onClick={onCancel} className="btn-secondary">Cancel</button>
        <button type="button" onClick={handleSubmit} className="btn-primary">Next →</button>
      </div>
    </div>
  );
}
