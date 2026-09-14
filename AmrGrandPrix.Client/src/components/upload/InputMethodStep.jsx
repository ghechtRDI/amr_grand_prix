/**
 * Step 2: Input Method
 * Choose between uploading a CSV/Excel file or pasting text results.
 * Pasted text supports a single mixed-gender block or separate male/female blocks.
 */

import { useState } from 'react';
import { useDropzone } from 'react-dropzone';
import * as tokenService from '../../services/tokenService';

const METHOD_FILE = 'file';
const METHOD_PASTE = 'paste';
const PASTE_MIXED = 'mixed';
const PASTE_BY_GENDER = 'by-gender';

export default function InputMethodStep({ wizardData, onNext, onBack, onCancel }) {
  const totalSteps = wizardData.totalSteps || 5;
  const [method, setMethod] = useState(METHOD_FILE);
  const [pasteMode, setPasteMode] = useState(PASTE_MIXED);

  // File upload state
  const [file, setFile] = useState(null);
  const [uploadProgress, setUploadProgress] = useState(0);

  // Paste state
  const [mixedText, setMixedText] = useState('');
  const [maleText, setMaleText] = useState('');
  const [femaleText, setFemaleText] = useState('');

  const [processing, setProcessing] = useState(false);
  const [error, setError] = useState(null);

  const { getRootProps, getInputProps, isDragActive, isDragReject } = useDropzone({
    accept: {
      'text/csv': ['.csv'],
      'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet': ['.xlsx'],
      'application/vnd.ms-excel': ['.xls'],
    },
    maxSize: 10485760,
    multiple: false,
    onDrop: (acceptedFiles, rejectedFiles) => {
      if (rejectedFiles.length > 0) {
        const err = rejectedFiles[0].errors[0];
        if (err?.code === 'file-too-large') {
          setError('File is too large. Maximum size is 10MB.');
        } else {
          setError('Invalid file type. Please upload CSV or Excel (.xlsx, .xls) files only.');
        }
        return;
      }
      if (acceptedFiles.length > 0) {
        setFile(acceptedFiles[0]);
        setError(null);
      }
    },
  });

  const authHeaders = () => ({
    Authorization: `Bearer ${tokenService.getAccessToken()}`,
  });

  const extractError = async (res) => {
    try {
      const text = await res.text();
      if (!text) return `HTTP ${res.status}: ${res.statusText}`;
      try {
        const json = JSON.parse(text);
        return json.message || json.title || text;
      } catch {
        return text;
      }
    } catch {
      return `HTTP ${res.status}: ${res.statusText}`;
    }
  };

  const parseTextBlock = async (textContent, gender, raceId) => {
    const res = await fetch('/api/results/parse-text', {
      method: 'POST',
      headers: { ...authHeaders(), 'Content-Type': 'application/json' },
      body: JSON.stringify({ raceId, textContent, gender }),
    });
    if (!res.ok) throw new Error(await extractError(res));
    return res.json();
  };

  const handleFileSubmit = async () => {
    if (!file) { setError('Please select a file'); return; }
    const raceId = wizardData.raceSelection?.raceId;
    if (!raceId) { setError('Please select a race first'); return; }

    try {
      setProcessing(true);
      setError(null);

      const formData = new FormData();
      formData.append('file', file);
      formData.append('raceId', raceId);

      let fakeProgress = 0;
      const progressInterval = setInterval(() => {
        fakeProgress = Math.min(fakeProgress + 10, 90);
        setUploadProgress(fakeProgress);
      }, 200);

      const res = await fetch('/api/results/upload', {
        method: 'POST',
        headers: authHeaders(),
        body: formData,
      });

      clearInterval(progressInterval);
      setUploadProgress(100);

      if (!res.ok) throw new Error(await extractError(res));
      const result = await res.json();

      setTimeout(() => {
        onNext({
          file,
          uploadBatchId: result.uploadBatchId,
          parsedResults: result.parsedResults,
          detectedColumns: result.detectedColumns,
          totalRows: result.totalRows,
          validRows: result.validRows,
          rowsWithIssues: result.rowsWithIssues,
        });
      }, 400);
    } catch (err) {
      setError(err.message);
      setUploadProgress(0);
    } finally {
      setProcessing(false);
    }
  };

  const handlePasteSubmit = async () => {
    const raceId = wizardData.raceSelection?.raceId;
    if (!raceId) { setError('Please select a race first'); return; }

    if (pasteMode === PASTE_MIXED && !mixedText.trim()) {
      setError('Please paste some results text');
      return;
    }
    if (pasteMode === PASTE_BY_GENDER && !maleText.trim() && !femaleText.trim()) {
      setError('Please paste results for at least one gender');
      return;
    }

    try {
      setProcessing(true);
      setError(null);

      if (pasteMode === PASTE_MIXED) {
        const result = await parseTextBlock(mixedText, null, raceId);
        onNext({
          uploadBatchId: result.uploadBatchId,
          parsedResults: result.parsedResults,
          detectedColumns: result.detectedColumns,
          totalRows: result.totalRows,
          validRows: result.validRows,
          rowsWithIssues: result.rowsWithIssues,
        });
        return;
      }

      // Separate by gender: parse each block, combine results
      let allResults = [];
      let detectedColumns = [];
      let firstBatchId = null;
      let primaryBatchId = null;

      if (maleText.trim()) {
        const maleResult = await parseTextBlock(maleText, 'Male', raceId);
        firstBatchId = maleResult.uploadBatchId;
        allResults = [...allResults, ...maleResult.parsedResults];
        if (maleResult.detectedColumns.length > 0) detectedColumns = maleResult.detectedColumns;
      }

      if (femaleText.trim()) {
        const femaleResult = await parseTextBlock(femaleText, 'Female', raceId);
        primaryBatchId = femaleResult.uploadBatchId;
        allResults = [...allResults, ...femaleResult.parsedResults];
        if (femaleResult.detectedColumns.length > 0) detectedColumns = femaleResult.detectedColumns;
      }

      // If only one gender was pasted, use that batch as primary
      if (!primaryBatchId) primaryBatchId = firstBatchId;

      // Clean up the first batch if a second was created
      if (firstBatchId && primaryBatchId !== firstBatchId) {
        await fetch(`/api/results/batch/${firstBatchId}`, {
          method: 'DELETE',
          headers: authHeaders(),
        });
      }

      onNext({
        uploadBatchId: primaryBatchId,
        parsedResults: allResults,
        detectedColumns,
        totalRows: allResults.length,
        validRows: allResults.filter(r => !r.validationIssues?.length).length,
        rowsWithIssues: allResults.filter(r => (r.validationIssues?.length ?? 0) > 0).length,
      });
    } catch (err) {
      setError(err.message);
    } finally {
      setProcessing(false);
    }
  };

  const handleSubmit = method === METHOD_FILE ? handleFileSubmit : handlePasteSubmit;

  return (
    <div className="wizard-step">
      <div className="step-header">
        <h2>Step 2: Input Method</h2>
        <span className="step-indicator">Step 2 of {totalSteps}</span>
      </div>

      {error && <div className="error-message">{error}</div>}

      {/* Method selector cards */}
      <div className="input-method-cards">
        <div
          className={`input-method-card ${method === METHOD_FILE ? 'selected' : ''}`}
          onClick={() => { setMethod(METHOD_FILE); setError(null); }}
        >
          <div className="method-card-icon">📁</div>
          <div className="method-card-title">Upload File</div>
          <div className="method-card-desc">CSV or Excel (.xlsx, .xls)</div>
        </div>
        <div
          className={`input-method-card ${method === METHOD_PASTE ? 'selected' : ''}`}
          onClick={() => { setMethod(METHOD_PASTE); setError(null); }}
        >
          <div className="method-card-icon">📋</div>
          <div className="method-card-title">Paste Text</div>
          <div className="method-card-desc">Copy &amp; paste from a results page or spreadsheet</div>
        </div>
      </div>

      {/* File upload panel */}
      {method === METHOD_FILE && (
        <div className="method-panel">
          {!file ? (
            <div
              {...getRootProps()}
              className={`dropzone ${isDragActive ? 'active' : ''} ${isDragReject ? 'reject' : ''}`}
            >
              <input {...getInputProps()} />
              {isDragActive ? (
                isDragReject ? (
                  <p>Invalid file type. Please upload CSV or Excel files only.</p>
                ) : (
                  <p>Drop the file here...</p>
                )
              ) : (
                <div className="dropzone-content">
                  <div className="dropzone-icon">📁</div>
                  <p>Drag and drop race results file here, or click to browse</p>
                  <p className="dropzone-hint">Accepted formats: CSV, Excel (.xlsx, .xls)</p>
                  <p className="dropzone-hint">Maximum file size: 10MB</p>
                </div>
              )}
            </div>
          ) : (
            <div className="file-preview">
              <div className="file-info">
                <div className="file-icon">📄</div>
                <div className="file-details">
                  <div className="file-name">{file.name}</div>
                  <div className="file-size">{(file.size / 1024).toFixed(2)} KB</div>
                </div>
                {!processing && (
                  <button
                    type="button"
                    onClick={() => { setFile(null); setUploadProgress(0); }}
                    className="btn-icon"
                    title="Remove file"
                  >
                    ✕
                  </button>
                )}
              </div>
              {processing && (
                <div className="upload-progress">
                  <div className="progress-bar">
                    <div className="progress-fill" style={{ width: `${uploadProgress}%` }} />
                  </div>
                  <div className="progress-text">Uploading and parsing... {uploadProgress}%</div>
                </div>
              )}
            </div>
          )}
        </div>
      )}

      {/* Paste text panel */}
      {method === METHOD_PASTE && (
        <div className="method-panel">
          <div className="paste-mode-toggle">
            <button
              type="button"
              className={`toggle-btn ${pasteMode === PASTE_MIXED ? 'active' : ''}`}
              onClick={() => setPasteMode(PASTE_MIXED)}
            >
              All genders in one block
            </button>
            <button
              type="button"
              className={`toggle-btn ${pasteMode === PASTE_BY_GENDER ? 'active' : ''}`}
              onClick={() => setPasteMode(PASTE_BY_GENDER)}
            >
              Separate by gender
            </button>
          </div>

          {pasteMode === PASTE_MIXED && (
            <div className="form-group">
              <label>Paste results (include a header row)</label>
              <textarea
                className="paste-textarea"
                value={mixedText}
                onChange={(e) => setMixedText(e.target.value)}
                placeholder={'Place\tName\tAge\tGender\tTime\n1\tJane Smith\t32\tF\t1:23:45\n2\tJohn Doe\t28\tM\t1:24:10'}
                rows={12}
              />
              <span className="field-hint">
                Tab- or comma-separated data with a header row. Include a Gender column, or switch to &ldquo;Separate by gender&rdquo; mode if your results don&rsquo;t have one.
              </span>
            </div>
          )}

          {pasteMode === PASTE_BY_GENDER && (
            <div className="paste-by-gender">
              <div className="form-group">
                <label>Male Results (include a header row)</label>
                <textarea
                  className="paste-textarea"
                  value={maleText}
                  onChange={(e) => setMaleText(e.target.value)}
                  placeholder={'Place\tName\tAge\tTime\n1\tJohn Doe\t28\t1:24:10'}
                  rows={8}
                />
              </div>
              <div className="form-group">
                <label>Female Results (include a header row)</label>
                <textarea
                  className="paste-textarea"
                  value={femaleText}
                  onChange={(e) => setFemaleText(e.target.value)}
                  placeholder={'Place\tName\tAge\tTime\n1\tJane Smith\t32\t1:23:45'}
                  rows={8}
                />
              </div>
              <span className="field-hint">
                Each block must have its own header row. Leave a block empty to skip that gender.
              </span>
            </div>
          )}
        </div>
      )}

      <div className="form-actions">
        <button type="button" onClick={onBack} className="btn-secondary">
          ← Back
        </button>
        <button type="button" onClick={onCancel} className="btn-secondary">
          Cancel
        </button>
        <button
          type="button"
          onClick={handleSubmit}
          className="btn-primary"
          disabled={processing}
        >
          {processing ? (
            <><span className="spinner" /> Processing...</>
          ) : (
            'Next →'
          )}
        </button>
      </div>
    </div>
  );
}
