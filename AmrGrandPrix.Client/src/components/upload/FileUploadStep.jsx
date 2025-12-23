/**
 * Step 2: File Upload
 * Drag-and-drop file upload with validation and parsing
 */

import { useState } from 'react';
import { useDropzone } from 'react-dropzone';

export default function FileUploadStep({ wizardData, onNext, onBack, onCancel }) {
  const [file, setFile] = useState(wizardData.file || null);
  const [uploading, setUploading] = useState(false);
  const [uploadProgress, setUploadProgress] = useState(0);
  const [error, setError] = useState(null);
  const [parseResult, setParseResult] = useState(null);

  const { getRootProps, getInputProps, isDragActive, isDragReject } = useDropzone({
    accept: {
      'text/csv': ['.csv'],
      'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet': ['.xlsx'],
      'application/vnd.ms-excel': ['.xls'],
      'application/pdf': ['.pdf'],
    },
    maxSize: 10485760, // 10MB
    multiple: false,
    onDrop: (acceptedFiles, rejectedFiles) => {
      if (rejectedFiles.length > 0) {
        const rejection = rejectedFiles[0];
        if (rejection.errors[0]?.code === 'file-too-large') {
          setError('File is too large. Maximum size is 10MB.');
        } else if (rejection.errors[0]?.code === 'file-invalid-type') {
          setError('Invalid file type. Please upload CSV, Excel (.xlsx, .xls), or PDF files only.');
        } else {
          setError('File upload failed. Please try again.');
        }
        return;
      }

      if (acceptedFiles.length > 0) {
        setFile(acceptedFiles[0]);
        setError(null);
        setParseResult(null);
      }
    },
  });

  const handleUploadAndParse = async () => {
    if (!file) {
      setError('Please select a file to upload');
      return;
    }

    const raceId = wizardData.raceSelection?.raceId;
    if (!raceId || raceId === 'new') {
      setError('Please select or create a race first');
      return;
    }

    try {
      setUploading(true);
      setUploadProgress(0);
      setError(null);

      const formData = new FormData();
      formData.append('file', file);
      formData.append('raceId', raceId);

      const token = localStorage.getItem('token');

      // Simulate progress (real progress would need backend support)
      const progressInterval = setInterval(() => {
        setUploadProgress(prev => {
          if (prev >= 90) {
            clearInterval(progressInterval);
            return 90;
          }
          return prev + 10;
        });
      }, 200);

      const response = await fetch('/api/results/upload', {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${token}`,
        },
        body: formData,
      });

      clearInterval(progressInterval);
      setUploadProgress(100);

      if (!response.ok) {
        const errorData = await response.json();
        throw new Error(errorData.message || 'Failed to upload and parse file');
      }

      const result = await response.json();
      setParseResult(result);

      // Proceed to next step automatically if successful
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
      }, 500);

    } catch (err) {
      setError(err.message);
      console.error('Upload error:', err);
      setUploadProgress(0);
    } finally {
      setUploading(false);
    }
  };

  const handleRemoveFile = () => {
    setFile(null);
    setParseResult(null);
    setError(null);
    setUploadProgress(0);
  };

  return (
    <div className="wizard-step">
      <div className="step-header">
        <h2>Step 2: File Upload</h2>
        <span className="step-indicator">Step 2 of 5</span>
      </div>

      {error && (
        <div className="error-message">
          {error}
        </div>
      )}

      {!file ? (
        <div
          {...getRootProps()}
          className={`dropzone ${isDragActive ? 'active' : ''} ${isDragReject ? 'reject' : ''}`}
        >
          <input {...getInputProps()} />
          {isDragActive ? (
            isDragReject ? (
              <p>Invalid file type. Please upload CSV, Excel, or PDF files only.</p>
            ) : (
              <p>Drop the file here...</p>
            )
          ) : (
            <div className="dropzone-content">
              <div className="dropzone-icon">📁</div>
              <p>Drag and drop race results file here, or click to browse</p>
              <p className="dropzone-hint">
                Accepted formats: CSV, Excel (.xlsx, .xls), PDF
              </p>
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
            {!uploading && !parseResult && (
              <button
                type="button"
                onClick={handleRemoveFile}
                className="btn-icon"
                title="Remove file"
              >
                ✕
              </button>
            )}
          </div>

          {uploading && (
            <div className="upload-progress">
              <div className="progress-bar">
                <div
                  className="progress-fill"
                  style={{ width: `${uploadProgress}%` }}
                />
              </div>
              <div className="progress-text">
                Uploading and parsing... {uploadProgress}%
              </div>
            </div>
          )}

          {parseResult && (
            <div className="parse-results">
              <h3>Parse Results</h3>
              <div className="parse-stats">
                <div className="stat">
                  <span className="stat-label">Total Rows:</span>
                  <span className="stat-value">{parseResult.totalRows}</span>
                </div>
                <div className="stat">
                  <span className="stat-label">Valid Rows:</span>
                  <span className="stat-value success">{parseResult.validRows}</span>
                </div>
                {parseResult.rowsWithIssues > 0 && (
                  <div className="stat">
                    <span className="stat-label">Rows with Issues:</span>
                    <span className="stat-value warning">{parseResult.rowsWithIssues}</span>
                  </div>
                )}
              </div>
              {parseResult.detectedColumns && parseResult.detectedColumns.length > 0 && (
                <div className="detected-columns">
                  <strong>Detected Columns:</strong>
                  <div className="column-tags">
                    {parseResult.detectedColumns.map((col, idx) => (
                      <span key={idx} className="column-tag">{col}</span>
                    ))}
                  </div>
                </div>
              )}
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
        {file && !parseResult && (
          <button
            type="button"
            onClick={handleUploadAndParse}
            className="btn-primary"
            disabled={uploading}
          >
            {uploading ? 'Processing...' : 'Upload & Parse'}
          </button>
        )}
      </div>
    </div>
  );
}
