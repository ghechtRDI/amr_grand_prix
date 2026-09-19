/**
 * Step 2: File Upload
 * Drag-and-drop file upload that sends the file to the LLM extraction API.
 * Accepts PDF, CSV, and Excel files.
 */

import { useState } from 'react';
import { useDropzone } from 'react-dropzone';
import * as tokenService from '../../services/tokenService';

export default function FileUploadStep({ wizardData, onNext, onBack, onCancel }) {
  const totalSteps = wizardData.totalSteps || 4;
  const [file, setFile] = useState(null);
  const [uploadProgress, setUploadProgress] = useState(0);
  const [processing, setProcessing] = useState(false);
  const [error, setError] = useState(null);

  const { getRootProps, getInputProps, isDragActive, isDragReject } = useDropzone({
    accept: {
      'application/pdf': ['.pdf'],
      'text/csv': ['.csv'],
      'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet': ['.xlsx'],
      'application/vnd.ms-excel': ['.xls'],
    },
    maxSize: 10485760,
    multiple: false,
    onDrop: (acceptedFiles, rejectedFiles) => {
      if (rejectedFiles.length > 0) {
        const err = rejectedFiles[0].errors[0];
        setError(
          err?.code === 'file-too-large'
            ? 'File is too large. Maximum size is 10MB.'
            : 'Invalid file type. Please upload a PDF, CSV, or Excel file.'
        );
        return;
      }
      if (acceptedFiles.length > 0) {
        setFile(acceptedFiles[0]);
        setError(null);
      }
    },
  });

  const handleSubmit = async () => {
    if (!file) { setError('Please select a file'); return; }
    const raceId = wizardData.raceSelection?.raceId;
    if (!raceId) { setError('Please select a race first'); return; }

    try {
      setProcessing(true);
      setError(null);

      const formData = new FormData();
      formData.append('file', file);
      formData.append('raceId', raceId);

      // Fake progress while LLM processes (can take 10–30 s)
      let fakeProgress = 0;
      const progressInterval = setInterval(() => {
        fakeProgress = Math.min(fakeProgress + 5, 85);
        setUploadProgress(fakeProgress);
      }, 600);

      const res = await fetch('/api/results/upload', {
        method: 'POST',
        headers: { Authorization: `Bearer ${tokenService.getAccessToken()}` },
        body: formData,
      });

      clearInterval(progressInterval);
      setUploadProgress(100);

      if (!res.ok) {
        const text = await res.text();
        let message = `HTTP ${res.status}`;
        try { message = JSON.parse(text)?.message || text; } catch { message = text; }
        throw new Error(message);
      }

      const result = await res.json();

      setTimeout(() => {
        onNext({
          file,
          uploadBatchId: result.uploadBatchId,
          parsedResults: result.parsedResults,
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

  return (
    <div className="wizard-step">
      <div className="step-header">
        <h2>Step 2: Upload Results File</h2>
        <span className="step-indicator">Step 2 of {totalSteps}</span>
      </div>

      {error && <div className="error-message">{error}</div>}

      <div className="method-panel">
        {!file ? (
          <div
            {...getRootProps()}
            className={`dropzone ${isDragActive ? 'active' : ''} ${isDragReject ? 'reject' : ''}`}
          >
            <input {...getInputProps()} />
            {isDragActive ? (
              isDragReject ? (
                <p>Invalid file type. Please upload a PDF, CSV, or Excel file.</p>
              ) : (
                <p>Drop the file here&hellip;</p>
              )
            ) : (
              <div className="dropzone-content">
                <div className="dropzone-icon">📁</div>
                <p>Drag and drop a race results file here, or click to browse</p>
                <p className="dropzone-hint">Accepted formats: PDF, CSV, Excel (.xlsx, .xls)</p>
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
                <div className="file-size">{(file.size / 1024).toFixed(1)} KB</div>
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
                <div className="progress-text">
                  {uploadProgress < 100
                    ? `Extracting results with AI… ${uploadProgress}%`
                    : 'Processing complete!'}
                </div>
              </div>
            )}
          </div>
        )}
      </div>

      <p className="field-hint" style={{ marginTop: '1rem' }}>
        Results are extracted automatically — no column mapping needed.
        PDF, CSV, and Excel files are all supported.
      </p>

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
          disabled={processing || !file}
        >
          {processing ? (
            <><span className="spinner" /> Extracting&hellip;</>
          ) : (
            'Next →'
          )}
        </button>
      </div>
    </div>
  );
}
