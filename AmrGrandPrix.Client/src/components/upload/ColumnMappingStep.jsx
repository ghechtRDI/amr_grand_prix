/**
 * Step 3: Column Mapping
 * Map detected columns to required fields
 */

import { useState } from 'react';
import { useForm } from 'react-hook-form';

const REQUIRED_FIELDS = [
  { key: 'name', label: 'Name', required: true },
  { key: 'age', label: 'Age', required: true },
  { key: 'place', label: 'Place', required: true },
  { key: 'time', label: 'Time', required: true },
  { key: 'gender', label: 'Gender', required: false },
];

const OPTIONAL_FIELDS = [
  { key: 'bib', label: 'Bib Number', required: false },
  { key: 'category', label: 'Category', required: false },
  { key: 'notes', label: 'Notes', required: false },
];

export default function ColumnMappingStep({ wizardData, onNext, onBack, onCancel }) {
  const detectedColumns = wizardData.detectedColumns || [];
  const parsedResults = wizardData.parsedResults || [];
  const [detectGenderFromSections, setDetectGenderFromSections] = useState(false);

  const {
    register,
    handleSubmit,
    watch,
    formState: { errors },
  } = useForm({
    defaultValues: wizardData.columnMapping || {
      name: '',
      age: '',
      place: '',
      time: '',
      gender: '',
      bib: '',
      category: '',
      notes: '',
    },
  });

  const watchedMappings = watch();

  const onSubmit = (data) => {
    // Validate that all required fields are mapped
    const missingFields = REQUIRED_FIELDS
      .filter(field => field.required && field.key !== 'gender')
      .filter(field => !data[field.key] || data[field.key] === '');

    if (missingFields.length > 0 && !detectGenderFromSections) {
      alert(`Please map all required fields: ${missingFields.map(f => f.label).join(', ')}`);
      return;
    }

    onNext({
      columnMapping: {
        ...data,
        detectGenderFromSections,
      },
    });
  };

  // Get preview data (first 5 rows)
  const getPreviewData = () => {
    return parsedResults.slice(0, 5).map((row, idx) => {
      const mappedRow = {};
      [...REQUIRED_FIELDS, ...OPTIONAL_FIELDS].forEach(field => {
        const columnName = watchedMappings[field.key];
        if (columnName && row.columns) {
          mappedRow[field.key] = row.columns[columnName] || '';
        } else {
          mappedRow[field.key] = '';
        }
      });
      return { ...mappedRow, rowNumber: idx + 1 };
    });
  };

  const previewData = getPreviewData();

  return (
    <div className="wizard-step">
      <div className="step-header">
        <h2>Step 3: Column Mapping</h2>
        <span className="step-indicator">Step 3 of 5</span>
      </div>

      <div className="step-description">
        <p>Map the detected columns to the required fields for race results.</p>
      </div>

      <form onSubmit={handleSubmit(onSubmit)} className="column-mapping-form">
        <div className="mapping-section">
          <h3>Required Fields</h3>
          {REQUIRED_FIELDS.map(field => (
            <div key={field.key} className="form-group mapping-row">
              <label htmlFor={field.key}>
                {field.label}
                {field.required && <span className="required">*</span>}
              </label>
              <select
                id={field.key}
                {...register(field.key, {
                  required: field.required && field.key !== 'gender'
                    ? `${field.label} mapping is required`
                    : false,
                })}
                className={errors[field.key] ? 'error' : ''}
                disabled={field.key === 'gender' && detectGenderFromSections}
              >
                <option value="">-- Select column --</option>
                {detectedColumns.map((col, idx) => (
                  <option key={idx} value={col}>
                    {col}
                  </option>
                ))}
              </select>
              {errors[field.key] && (
                <span className="field-error">{errors[field.key].message}</span>
              )}
            </div>
          ))}

          <div className="form-group checkbox-group">
            <label>
              <input
                type="checkbox"
                checked={detectGenderFromSections}
                onChange={(e) => setDetectGenderFromSections(e.target.checked)}
              />
              Detect gender from section headers (e.g., "MALE RESULTS", "FEMALE")
            </label>
          </div>
        </div>

        <div className="mapping-section">
          <h3>Optional Fields</h3>
          {OPTIONAL_FIELDS.map(field => (
            <div key={field.key} className="form-group mapping-row">
              <label htmlFor={field.key}>{field.label}</label>
              <select id={field.key} {...register(field.key)}>
                <option value="">-- Not mapped --</option>
                {detectedColumns.map((col, idx) => (
                  <option key={idx} value={col}>
                    {col}
                  </option>
                ))}
              </select>
            </div>
          ))}
        </div>

        {previewData.length > 0 && (
          <div className="preview-section">
            <h3>Preview (First 5 Rows)</h3>
            <div className="preview-table-container">
              <table className="preview-table">
                <thead>
                  <tr>
                    <th>#</th>
                    <th>Name</th>
                    <th>Age</th>
                    <th>Place</th>
                    <th>Time</th>
                    <th>Gender</th>
                    <th>Bib</th>
                  </tr>
                </thead>
                <tbody>
                  {previewData.map((row, idx) => (
                    <tr key={idx}>
                      <td>{row.rowNumber}</td>
                      <td>{row.name || '-'}</td>
                      <td>{row.age || '-'}</td>
                      <td>{row.place || '-'}</td>
                      <td>{row.time || '-'}</td>
                      <td>
                        {detectGenderFromSections ? (
                          <em>Auto-detect</em>
                        ) : (
                          row.gender || '-'
                        )}
                      </td>
                      <td>{row.bib || '-'}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <p className="preview-hint">
              Review the preview to ensure columns are mapped correctly
            </p>
          </div>
        )}

        <div className="form-actions">
          <button type="button" onClick={onBack} className="btn-secondary">
            ← Back
          </button>
          <button type="button" onClick={onCancel} className="btn-secondary">
            Cancel
          </button>
          <button type="submit" className="btn-primary">
            Next →
          </button>
        </div>
      </form>
    </div>
  );
}
