/**
 * Step 2: Input Method
 * Choose between uploading a CSV/Excel file or pasting text results.
 * Pasted text supports a single mixed-gender block or separate male/female blocks.
 */

import { useState } from 'react';
import { useDropzone } from 'react-dropzone';
import { AlertCircle, ClipboardList, FileText, Loader2, UploadCloud, X } from 'lucide-react';
import * as tokenService from '../../services/tokenService';
import { Button } from '@/components/ui/button';
import { Label } from '@/components/ui/label';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Progress } from '@/components/ui/progress';
import { cn } from '@/lib/utils';

const METHOD_FILE = 'file';
const METHOD_PASTE = 'paste';
const PASTE_MIXED = 'mixed';
const PASTE_BY_GENDER = 'by-gender';

const TEXTAREA_CLASS =
  'w-full rounded-lg border border-input bg-transparent px-3 py-2 font-mono text-sm text-foreground outline-none transition-colors placeholder:text-muted-foreground focus-visible:border-ring focus-visible:ring-3 focus-visible:ring-ring/50';

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
    <div>
      <div className="mb-6 flex items-center justify-between border-b border-border pb-4">
        <h2 className="text-xl font-semibold">Step 2: Input Method</h2>
        <span className="text-sm text-muted-foreground">Step 2 of {totalSteps}</span>
      </div>

      {error && (
        <Alert variant="destructive" className="mb-6">
          <AlertCircle />
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      )}

      {/* Method selector cards */}
      <div className="mb-6 flex gap-4">
        <button
          type="button"
          onClick={() => { setMethod(METHOD_FILE); setError(null); }}
          className={cn(
            'flex flex-1 flex-col items-center gap-2 rounded-xl border-2 p-6 text-center transition-colors',
            method === METHOD_FILE
              ? 'border-primary bg-primary/10'
              : 'border-border bg-muted/30 hover:border-primary/50 hover:bg-primary/5'
          )}
        >
          <UploadCloud className="size-8 text-muted-foreground" />
          <span className="font-semibold">Upload File</span>
          <span className="text-sm text-muted-foreground">CSV or Excel (.xlsx, .xls)</span>
        </button>
        <button
          type="button"
          onClick={() => { setMethod(METHOD_PASTE); setError(null); }}
          className={cn(
            'flex flex-1 flex-col items-center gap-2 rounded-xl border-2 p-6 text-center transition-colors',
            method === METHOD_PASTE
              ? 'border-primary bg-primary/10'
              : 'border-border bg-muted/30 hover:border-primary/50 hover:bg-primary/5'
          )}
        >
          <ClipboardList className="size-8 text-muted-foreground" />
          <span className="font-semibold">Paste Text</span>
          <span className="text-sm text-muted-foreground">Copy &amp; paste from a results page or spreadsheet</span>
        </button>
      </div>

      {/* File upload panel */}
      {method === METHOD_FILE && (
        <div className="mb-4">
          {!file ? (
            <div
              {...getRootProps()}
              className={cn(
                'flex min-h-[260px] cursor-pointer flex-col items-center justify-center rounded-xl border-2 border-dashed border-border bg-muted/30 p-10 text-center transition-colors',
                'hover:border-primary/60 hover:bg-primary/5',
                isDragActive && !isDragReject && 'border-primary bg-primary/10',
                isDragReject && 'border-destructive bg-destructive/5'
              )}
            >
              <input {...getInputProps()} />
              {isDragActive ? (
                isDragReject ? (
                  <p className="text-destructive">Invalid file type. Please upload CSV or Excel files only.</p>
                ) : (
                  <p className="text-foreground">Drop the file here...</p>
                )
              ) : (
                <div className="flex flex-col items-center gap-2 text-muted-foreground">
                  <UploadCloud className="mb-2 size-10 text-muted-foreground/70" />
                  <p className="text-foreground">Drag and drop race results file here, or click to browse</p>
                  <p className="text-sm">Accepted formats: CSV, Excel (.xlsx, .xls)</p>
                  <p className="text-sm">Maximum file size: 10MB</p>
                </div>
              )}
            </div>
          ) : (
            <div className="rounded-xl border border-border bg-muted/30 p-6">
              <div className="flex items-center gap-3">
                <FileText className="size-8 shrink-0 text-muted-foreground" />
                <div className="min-w-0 flex-1">
                  <div className="truncate font-medium">{file.name}</div>
                  <div className="text-sm text-muted-foreground">{(file.size / 1024).toFixed(2)} KB</div>
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
                <div className="mt-3 flex flex-col gap-2">
                  <Progress value={uploadProgress} />
                  <div className="text-center text-sm text-muted-foreground">
                    Uploading and parsing... {uploadProgress}%
                  </div>
                </div>
              )}
            </div>
          )}
        </div>
      )}

      {/* Paste text panel */}
      {method === METHOD_PASTE && (
        <div className="mb-4">
          <div className="mb-6 flex gap-2">
            <Button
              type="button"
              variant={pasteMode === PASTE_MIXED ? 'secondary' : 'outline'}
              onClick={() => setPasteMode(PASTE_MIXED)}
            >
              All genders in one block
            </Button>
            <Button
              type="button"
              variant={pasteMode === PASTE_BY_GENDER ? 'secondary' : 'outline'}
              onClick={() => setPasteMode(PASTE_BY_GENDER)}
            >
              Separate by gender
            </Button>
          </div>

          {pasteMode === PASTE_MIXED && (
            <div className="flex flex-col gap-2">
              <Label>Paste results (include a header row)</Label>
              <textarea
                className={TEXTAREA_CLASS}
                value={mixedText}
                onChange={(e) => setMixedText(e.target.value)}
                placeholder={'Place\tName\tAge\tGender\tTime\n1\tJane Smith\t32\tF\t1:23:45\n2\tJohn Doe\t28\tM\t1:24:10'}
                rows={12}
              />
              <p className="text-xs text-muted-foreground">
                Tab- or comma-separated data with a header row. Include a Gender column, or switch to &ldquo;Separate by gender&rdquo; mode if your results don&rsquo;t have one.
              </p>
            </div>
          )}

          {pasteMode === PASTE_BY_GENDER && (
            <div className="flex flex-col gap-4 md:flex-row">
              <div className="flex flex-1 flex-col gap-2">
                <Label>Male Results (include a header row)</Label>
                <textarea
                  className={TEXTAREA_CLASS}
                  value={maleText}
                  onChange={(e) => setMaleText(e.target.value)}
                  placeholder={'Place\tName\tAge\tTime\n1\tJohn Doe\t28\t1:24:10'}
                  rows={8}
                />
              </div>
              <div className="flex flex-1 flex-col gap-2">
                <Label>Female Results (include a header row)</Label>
                <textarea
                  className={TEXTAREA_CLASS}
                  value={femaleText}
                  onChange={(e) => setFemaleText(e.target.value)}
                  placeholder={'Place\tName\tAge\tTime\n1\tJane Smith\t32\t1:23:45'}
                  rows={8}
                />
              </div>
              <p className="w-full text-xs text-muted-foreground">
                Each block must have its own header row. Leave a block empty to skip that gender.
              </p>
            </div>
          )}
        </div>
      )}

      <div className="mt-8 flex flex-col-reverse gap-3 border-t border-border pt-6 sm:flex-row sm:justify-end">
        <Button type="button" variant="outline" onClick={onBack}>
          &larr; Back
        </Button>
        <Button type="button" variant="outline" onClick={onCancel}>
          Cancel
        </Button>
        <Button type="button" onClick={handleSubmit} disabled={processing}>
          {processing ? (
            <>
              <Loader2 className="animate-spin" /> Processing...
            </>
          ) : (
            <>Next &rarr;</>
          )}
        </Button>
      </div>
    </div>
  );
}
