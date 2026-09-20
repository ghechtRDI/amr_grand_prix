/**
 * Step 2: File Upload
 * Drag-and-drop file upload that sends the file to the LLM extraction API.
 * Accepts PDF, CSV, and Excel files.
 */

import { useState } from 'react';
import { useDropzone } from 'react-dropzone';
import { AlertCircle, FileText, Loader2, UploadCloud, X } from 'lucide-react';
import * as tokenService from '../../services/tokenService';
import { Button } from '@/components/ui/button';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Progress } from '@/components/ui/progress';
import { cn } from '@/lib/utils';

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
    <div>
      <div className="mb-6 flex items-center justify-between border-b border-border pb-4">
        <h2 className="text-xl font-semibold">Step 2: Upload Results File</h2>
        <span className="text-sm text-muted-foreground">Step 2 of {totalSteps}</span>
      </div>

      {error && (
        <Alert variant="destructive" className="mb-6">
          <AlertCircle />
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      )}

      <div className="mb-4">
        {!file ? (
          <div
            {...getRootProps()}
            className={cn(
              'flex min-h-[280px] cursor-pointer flex-col items-center justify-center rounded-xl border-2 border-dashed border-border bg-muted/30 p-10 text-center transition-colors',
              'hover:border-primary/60 hover:bg-primary/5',
              isDragActive && !isDragReject && 'border-primary bg-primary/10',
              isDragReject && 'border-destructive bg-destructive/5'
            )}
          >
            <input {...getInputProps()} />
            {isDragActive ? (
              isDragReject ? (
                <p className="text-destructive">Invalid file type. Please upload a PDF, CSV, or Excel file.</p>
              ) : (
                <p className="text-foreground">Drop the file here&hellip;</p>
              )
            ) : (
              <div className="flex flex-col items-center gap-2 text-muted-foreground">
                <UploadCloud className="mb-2 size-10 text-muted-foreground/70" />
                <p className="text-foreground">Drag and drop a race results file here, or click to browse</p>
                <p className="text-sm">Accepted formats: PDF, CSV, Excel (.xlsx, .xls)</p>
                <p className="text-sm">Maximum file size: 10MB</p>
              </div>
            )}
          </div>
        ) : (
          <div className="rounded-xl border border-border bg-muted/30 p-6">
            <div className="mb-3 flex items-center gap-3">
              <FileText className="size-8 shrink-0 text-muted-foreground" />
              <div className="min-w-0 flex-1">
                <div className="truncate font-medium">{file.name}</div>
                <div className="text-sm text-muted-foreground">{(file.size / 1024).toFixed(1)} KB</div>
              </div>
              {!processing && (
                <Button
                  type="button"
                  variant="ghost"
                  size="icon"
                  onClick={() => { setFile(null); setUploadProgress(0); }}
                  title="Remove file"
                  aria-label="Remove file"
                >
                  <X className="size-4" />
                </Button>
              )}
            </div>
            {processing && (
              <div className="mt-2 flex flex-col gap-2">
                <Progress value={uploadProgress} />
                <div className="text-center text-sm text-muted-foreground">
                  {uploadProgress < 100
                    ? `Extracting results with AI… ${uploadProgress}%`
                    : 'Processing complete!'}
                </div>
              </div>
            )}
          </div>
        )}
      </div>

      <p className="mt-4 text-xs text-muted-foreground">
        Results are extracted automatically — no column mapping needed.
        PDF, CSV, and Excel files are all supported.
      </p>

      <div className="mt-8 flex flex-col-reverse gap-3 border-t border-border pt-6 sm:flex-row sm:justify-end">
        <Button type="button" variant="outline" onClick={onBack}>
          &larr; Back
        </Button>
        <Button type="button" variant="outline" onClick={onCancel}>
          Cancel
        </Button>
        <Button
          type="button"
          onClick={handleSubmit}
          disabled={processing || !file}
        >
          {processing ? (
            <>
              <Loader2 className="animate-spin" /> Extracting&hellip;
            </>
          ) : (
            <>Next &rarr;</>
          )}
        </Button>
      </div>
    </div>
  );
}
