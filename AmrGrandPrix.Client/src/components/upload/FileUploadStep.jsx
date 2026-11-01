/**
 * Step 2: File Upload
 * Drag-and-drop file upload that sends the file to the LLM extraction API.
 * Accepts PDF, CSV, and Excel files.
 *
 * The upload returns 202 straight away and the server extracts in the background (it can take
 * minutes for a big file), so this polls GET /api/results/upload/{id}. The progress bar tracks
 * real work: upload bytes, then the number of result rows the LLM has written so far against
 * the server's rough estimate of how many the file contains.
 */

import { useEffect, useRef, useState } from 'react';
import { useDropzone } from 'react-dropzone';
import { AlertCircle, FileText, Loader2, UploadCloud, X } from 'lucide-react';
import * as tokenService from '../../services/tokenService';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Badge } from '@/components/ui/badge';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Progress } from '@/components/ui/progress';
import { readError } from '@/services/api';
import { cn } from '@/lib/utils';

const POLL_INTERVAL_MS = 1500;
const MAX_POLL_FAILURES = 5;

// Share of the bar given to each phase: uploading the file, waiting for the LLM's first rows,
// then extracting rows. The last 5% is left for the results arriving.
const UPLOAD_END = 8;
const START_END = 15;
const EXTRACT_END = 95;

const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

/** POST with upload-progress events (fetch can't report request-body progress). */
function postWithProgress(url, body, onProgress) {
  return new Promise((resolve, reject) => {
    const xhr = new XMLHttpRequest();
    xhr.open('POST', url);
    xhr.setRequestHeader('Authorization', `Bearer ${tokenService.getAccessToken()}`);
    xhr.upload.onprogress = (e) => { if (e.lengthComputable) onProgress(e.loaded / e.total); };
    xhr.onload = () => resolve(new Response(xhr.responseText, { status: xhr.status }));
    xhr.onerror = () => reject(new Error('Network error while uploading the file'));
    xhr.send(body);
  });
}

/** Bar percentage for the current phase (see the *_END constants). */
function progressPercent({ phase, uploadFraction, rows, expectedRows, extractingSeconds }) {
  switch (phase) {
    case 'uploading':
      return UPLOAD_END * uploadFraction;
    case 'queued':
      return UPLOAD_END;
    case 'extracting': {
      if (rows === 0) {
        // The model reads the whole file before writing its first row; ease toward START_END.
        return UPLOAD_END + (START_END - UPLOAD_END) * (1 - Math.exp(-extractingSeconds / 15));
      }
      // Without an estimate, approach the end asymptotically instead of claiming a fraction.
      const fraction = expectedRows ? Math.min(rows / expectedRows, 1) : 1 - Math.exp(-rows / 250);
      return START_END + (EXTRACT_END - START_END) * fraction;
    }
    case 'done':
      return 100;
    default:
      return 0;
  }
}

function formatElapsed(seconds) {
  const m = Math.floor(seconds / 60);
  const s = String(Math.floor(seconds % 60)).padStart(2, '0');
  return `${m}:${s}`;
}

function progressLabel({ phase, uploadFraction, rows, expectedRows }) {
  switch (phase) {
    case 'uploading':
      return `Uploading file… ${Math.round(uploadFraction * 100)}%`;
    case 'queued':
      return 'Waiting for another upload to finish extracting…';
    case 'extracting':
      if (rows === 0) return 'AI is reading the file…';
      if (expectedRows && rows >= expectedRows) return `Extracted ${rows} results — finishing up…`;
      return expectedRows
        ? `Extracted ${rows} of ~${expectedRows} results…`
        : `Extracted ${rows} results…`;
    case 'done':
      return `Done — extracted ${rows} results`;
    default:
      return '';
  }
}

export default function FileUploadStep({ wizardData, onNext, onBack, onCancel }) {
  const totalSteps = wizardData.totalSteps || 4;
  const [file, setFile] = useState(null);
  const [progress, setProgress] = useState({ phase: 'idle', uploadFraction: 0, rows: 0, expectedRows: null });
  const [extractingSince, setExtractingSince] = useState(null);
  const [now, setNow] = useState(() => Date.now());
  const [processing, setProcessing] = useState(false);
  // Stops polling if the user leaves the step mid-extraction (it carries on server-side).
  const unmountedRef = useRef(false);
  useEffect(() => () => { unmountedRef.current = true; }, []);

  // Tick once a second while working, for the elapsed clock and the "reading the file" ease-in.
  useEffect(() => {
    if (!processing) return undefined;
    const id = setInterval(() => setNow(Date.now()), 1000);
    return () => clearInterval(id);
  }, [processing]);
  const [error, setError] = useState(null);
  // Multi-variant upload: the variants ticked in Step 1 (no race exists yet).
  const includedVariantIds = wizardData.raceSelection?.includedVariantIds || [];
  const isMultiVariant = includedVariantIds.length > 0;
  const [knownVariants, setKnownVariants] = useState(
    (wizardData.raceSelection?.knownVariants || []).join(', ')
  );

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
    const { raceId, raceSeriesId, raceDate } = wizardData.raceSelection || {};
    if (!raceId && !isMultiVariant) { setError('Please select a race first'); return; }

    try {
      setProcessing(true);
      setError(null);

      const formData = new FormData();
      formData.append('file', file);
      // A multi-variant upload has no race yet - just the series, date and ticked variants.
      if (raceId) {
        formData.append('raceId', raceId);
        if (knownVariants.trim()) formData.append('knownVariants', knownVariants.trim());
      } else {
        formData.append('raceSeriesId', raceSeriesId);
        formData.append('raceDate', raceDate);
        includedVariantIds.forEach((id) => formData.append('includedVariantIds', id));
      }

      setProgress({ phase: 'uploading', uploadFraction: 0, rows: 0, expectedRows: null });
      setExtractingSince(null);
      const uploadRes = await postWithProgress('/api/results/upload', formData, (fraction) =>
        setProgress((p) => ({ ...p, uploadFraction: fraction })));
      if (!uploadRes.ok) throw new Error(await readError(uploadRes));
      const { uploadBatchId, expectedRows } = await uploadRes.json();
      setProgress({ phase: 'queued', uploadFraction: 1, rows: 0, expectedRows });

      const result = await pollUntilExtracted(uploadBatchId);
      if (!result) return; // left the page; the batch will be waiting under Results Management

      setProgress((p) => ({ ...p, phase: 'done', rows: result.totalRows }));
      setTimeout(() => {
        onNext({
          file,
          uploadBatchId: result.uploadBatchId,
          parsedResults: result.parsedResults,
          totalRows: result.totalRows,
          validRows: result.validRows,
          rowsWithIssues: result.rowsWithIssues,
        });
      }, 600);
    } catch (err) {
      setError(err.message);
      setProgress({ phase: 'idle', uploadFraction: 0, rows: 0, expectedRows: null });
      setProcessing(false);
    }
  };

  /** Poll the batch until extraction finishes; resolves with the parsed results (null if unmounted). */
  const pollUntilExtracted = async (batchId) => {
    let failures = 0;
    while (!unmountedRef.current) {
      await sleep(POLL_INTERVAL_MS);
      if (unmountedRef.current) break;

      let status;
      try {
        const res = await fetch(`/api/results/upload/${batchId}`, {
          headers: { Authorization: `Bearer ${tokenService.getAccessToken()}` },
        });
        if (!res.ok) {
          // A 4xx is a real answer (e.g. the batch was deleted); retry only server/network blips.
          if (res.status < 500) throw Object.assign(new Error(await readError(res)), { fatal: true });
          throw new Error(await readError(res));
        }
        status = await res.json();
        failures = 0;
      } catch (err) {
        if (err.fatal || ++failures >= MAX_POLL_FAILURES) throw err;
        continue;
      }

      switch (status.state) {
        case 'Ready':
          return status.result;
        case 'Failed':
          throw new Error(`Extraction failed: ${status.error || 'unknown error'}`);
        case 'Extracting':
          setExtractingSince((since) => since ?? Date.now());
          setProgress((p) => ({ ...p, phase: 'extracting', rows: status.rowsExtracted, expectedRows: status.expectedRows }));
          break;
        default: // Queued
          setProgress((p) => ({ ...p, phase: 'queued' }));
      }
    }
    return null;
  };

  const extractingSeconds = extractingSince ? Math.max(0, (now - extractingSince) / 1000) : 0;
  const percent = Math.round(progressPercent({ ...progress, extractingSeconds }));

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
                  onClick={() => setFile(null)}
                  title="Remove file"
                  aria-label="Remove file"
                >
                  <X className="size-4" />
                </Button>
              )}
            </div>
            {processing && (
              <div className="mt-2 flex flex-col gap-2">
                <Progress
                  value={percent}
                  aria-label="Extraction progress"
                  className="[&_[data-slot=progress-indicator]]:duration-1000 [&_[data-slot=progress-indicator]]:ease-out [&_[data-slot=progress-track]]:h-2"
                />
                <div className="flex items-center justify-between gap-3 text-sm text-muted-foreground">
                  <span aria-live="polite">{progressLabel(progress)}</span>
                  {extractingSince && progress.phase !== 'done' && (
                    <span className="shrink-0 tabular-nums">{formatElapsed(extractingSeconds)}</span>
                  )}
                </div>
                {progress.phase === 'extracting' && extractingSeconds > 20 && (
                  <p className="text-xs text-muted-foreground">
                    Large files can take a few minutes. You can leave this page — extraction keeps
                    going, and the upload will be waiting under Results Management (Resume) when it&rsquo;s done.
                  </p>
                )}
              </div>
            )}
          </div>
        )}
      </div>

      <p className="mt-4 text-xs text-muted-foreground">
        Results are extracted automatically — no column mapping needed.
        PDF, CSV, and Excel files are all supported.
      </p>

      {isMultiVariant ? (
        <div className="mt-6 flex flex-col gap-2 border-t border-border pt-6">
          <Label>Variants in this file</Label>
          <div className="flex flex-wrap gap-2">
            {wizardData.raceSelection.knownVariants.map((name) => (
              <Badge key={name} variant="secondary">{name}</Badge>
            ))}
          </div>
          <p className="text-xs text-muted-foreground">
            The AI extractor is told the file contains only these variants and labels each section
            with one of them. To change the list, go back to Race Selection.
          </p>
        </div>
      ) : (
        <div className="mt-6 flex flex-col gap-2 border-t border-border pt-6">
          <Label htmlFor="knownVariants">Known course/variant names (optional)</Label>
          <Input
            id="knownVariants"
            type="text"
            value={knownVariants}
            onChange={(e) => setKnownVariants(e.target.value)}
            placeholder="e.g. Junior - Blueberry Knoll, Adult - Young at Heart Blueberry Knoll"
            disabled={processing}
          />
          <p className="text-xs text-muted-foreground">
            If this file's results cover more than one course or division at once, list the known
            names here, comma-separated. The AI extractor will reuse these exact labels instead of
            inventing its own when it splits the file into sections.
            {wizardData.raceSelection?.knownVariants?.length > 0 &&
              ' Pre-filled from this series’ other race instances — edit as needed.'}
          </p>
        </div>
      )}

      <div className="mt-8 flex flex-col-reverse gap-3 border-t border-border pt-6 sm:flex-row sm:justify-end">
        <Button type="button" variant="outline" onClick={onBack} disabled={processing}>
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
