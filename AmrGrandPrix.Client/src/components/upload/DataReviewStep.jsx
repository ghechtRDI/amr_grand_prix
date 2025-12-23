/**
 * Step 4: Data Review & Validation
 * Editable table to review and fix data before saving
 */

import { useState, useMemo } from 'react';
import {
  useReactTable,
  getCoreRowModel,
  flexRender,
  createColumnHelper,
} from '@tanstack/react-table';

const columnHelper = createColumnHelper();

export default function DataReviewStep({ wizardData, onNext, onBack, onCancel }) {
  const [data, setData] = useState(() => {
    // Transform parsed results based on column mapping
    const parsedResults = wizardData.parsedResults || [];
    const mapping = wizardData.columnMapping || {};

    return parsedResults.map((row, idx) => ({
      id: idx,
      name: row.columns?.[mapping.name] || row.name || '',
      age: row.columns?.[mapping.age] || row.age || '',
      place: row.columns?.[mapping.place] || row.place || '',
      time: row.columns?.[mapping.time] || row.time || '',
      gender: mapping.detectGenderFromSections
        ? row.sectionHeader?.includes('MALE') || row.sectionHeader?.includes('MEN')
          ? 'Male'
          : row.sectionHeader?.includes('FEMALE') || row.sectionHeader?.includes('WOMEN')
            ? 'Female'
            : ''
        : row.columns?.[mapping.gender] || row.gender || '',
      bib: row.columns?.[mapping.bib] || row.bib || '',
      status: row.status || 'Finished',
      validationIssues: row.validationIssues || [],
      isNewRunner: row.isNewRunner || false,
    }));
  });

  const [editingCell, setEditingCell] = useState(null);

  // Update cell value
  const updateData = (rowIndex, columnId, value) => {
    setData(old =>
      old.map((row, index) => {
        if (index === rowIndex) {
          return {
            ...row,
            [columnId]: value,
          };
        }
        return row;
      })
    );
  };

  // Editable cell component
  const EditableCell = ({ getValue, row, column, table }) => {
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
  };

  // Define columns
  const columns = useMemo(
    () => [
      columnHelper.accessor('place', {
        header: 'Place',
        cell: EditableCell,
        size: 80,
      }),
      columnHelper.accessor('bib', {
        header: 'Bib',
        cell: EditableCell,
        size: 80,
      }),
      columnHelper.accessor('name', {
        header: 'Name',
        cell: EditableCell,
        size: 200,
      }),
      columnHelper.accessor('age', {
        header: 'Age',
        cell: EditableCell,
        size: 80,
      }),
      columnHelper.accessor('gender', {
        header: 'Gender',
        cell: EditableCell,
        size: 100,
      }),
      columnHelper.accessor('time', {
        header: 'Time',
        cell: EditableCell,
        size: 120,
      }),
      columnHelper.accessor('status', {
        header: 'Status',
        cell: EditableCell,
        size: 100,
      }),
      columnHelper.display({
        id: 'issues',
        header: 'Issues',
        cell: ({ row }) => {
          const issues = row.original.validationIssues || [];
          const isNewRunner = row.original.isNewRunner;

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
              {isNewRunner && (
                <span className="issue-badge info" title="New runner">
                  ℹ
                </span>
              )}
            </div>
          );
        },
        size: 80,
      }),
    ],
    [editingCell]
  );

  const table = useReactTable({
    data,
    columns,
    getCoreRowModel: getCoreRowModel(),
  });

  // Calculate summary stats
  const stats = useMemo(() => {
    const totalResults = data.length;
    const warnings = data.filter(row => row.validationIssues?.length > 0).length;
    const newRunners = data.filter(row => row.isNewRunner).length;

    return { totalResults, warnings, newRunners };
  }, [data]);

  const handleSubmit = () => {
    // Validate all required fields are present
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
        <h2>Step 4: Data Review</h2>
        <span className="step-indicator">Step 4 of 5</span>
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
        </div>
        <p className="review-hint">
          Click any cell to edit. Press Enter to save, Escape to cancel.
        </p>
      </div>

      <div className="table-container">
        <table className="data-review-table">
          <thead>
            {table.getHeaderGroups().map(headerGroup => (
              <tr key={headerGroup.id}>
                {headerGroup.headers.map(header => (
                  <th
                    key={header.id}
                    style={{ width: header.getSize() }}
                  >
                    {flexRender(
                      header.column.columnDef.header,
                      header.getContext()
                    )}
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
        <button type="button" onClick={onBack} className="btn-secondary">
          ← Back
        </button>
        <button type="button" onClick={onCancel} className="btn-secondary">
          Cancel
        </button>
        <button type="button" onClick={handleSubmit} className="btn-primary">
          Next →
        </button>
      </div>
    </div>
  );
}
