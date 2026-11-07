/**
 * Results Upload Wizard
 * Multi-step wizard: Race Selection → File Upload → Data Review → Confirmation
 */

import { useState, useEffect } from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import { Check, Loader2 } from 'lucide-react';
import { fetchResumeWizardData } from '../../components/upload/resumeBatch';
import { Button } from '@/components/ui/button';
import RaceSelectionStep from '../../components/upload/RaceSelectionStep';
import FileUploadStep    from '../../components/upload/FileUploadStep';
import DataReviewStep    from '../../components/upload/DataReviewStep';
import ConfirmationStep  from '../../components/upload/ConfirmationStep';
import { Card, CardContent } from '@/components/ui/card';
import { Progress } from '@/components/ui/progress';
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog';
import { cn } from '@/lib/utils';

const STEPS = [
  { id: 'race-selection', name: 'Race Selection', component: RaceSelectionStep },
  { id: 'file-upload',    name: 'File Upload',    component: FileUploadStep    },
  { id: 'data-review',    name: 'Data Review',    component: DataReviewStep    },
  { id: 'confirmation',   name: 'Confirmation',   component: ConfirmationStep  },
];

// The Data Review step's wide multi-column table needs more room than the other,
// narrower form steps - widen only for that step rather than stretching everything.
const WIDE_STEP_IDS = new Set(['data-review']);

export default function ResultsUpload() {
  const location = useLocation();
  const navigate = useNavigate();
  // Resume from Results Management hands over the fetched batch as `resume`. That snapshot is
  // swapped for just its id below, because history state survives a page reload: keeping the
  // snapshot would restore the review draft as it was when Resume was clicked, not the latest
  // Save Draft. A reload (state holds only `resumeBatchId`) re-fetches the batch instead.
  const resumeData = location.state?.resume || null;
  const resumeBatchId = location.state?.resumeBatchId || resumeData?.uploadBatchId || null;
  const isResume = !!resumeBatchId;

  const [currentStepId, setCurrentStepId] = useState(isResume ? 'data-review' : 'race-selection');
  const [wizardData, setWizardData]       = useState(resumeData || {});
  const [resumeLoading, setResumeLoading] = useState(isResume && !resumeData);
  const [resumeError, setResumeError]     = useState(null);
  const [cancelDialogOpen, setCancelDialogOpen] = useState(false);

  useEffect(() => {
    if (resumeData) {
      navigate(location.pathname, { replace: true, state: { resumeBatchId } });
    }
    // Run once on mount: the replace above changes location.state, which must not re-trigger this.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => {
    if (!resumeLoading) return;
    let cancelled = false;
    fetchResumeWizardData(resumeBatchId)
      .then(data => { if (!cancelled) setWizardData(data); })
      .catch(err => { if (!cancelled) setResumeError(err.message); })
      .finally(() => { if (!cancelled) setResumeLoading(false); });
    return () => { cancelled = true; };
  }, [resumeLoading, resumeBatchId]);

  const currentIndex        = STEPS.findIndex(s => s.id === currentStepId);
  const CurrentStepComponent = STEPS[currentIndex]?.component;

  const handleNext = (stepData) => {
    setWizardData(prev => ({ ...prev, ...stepData }));
    const nextIndex = currentIndex + 1;
    if (nextIndex < STEPS.length) setCurrentStepId(STEPS[nextIndex].id);
  };

  const handleBack = () => {
    // Resumed batches skip race selection / file upload (already done in a prior session),
    // so there's no earlier step to go back to.
    if (isResume && currentStepId === 'data-review') {
      navigate('/admin/results');
      return;
    }
    if (currentIndex > 0) setCurrentStepId(STEPS[currentIndex - 1].id);
  };

  const handleCancelRequest = () => setCancelDialogOpen(true);

  const handleConfirmCancel = () => {
    setCancelDialogOpen(false);
    navigate(isResume ? '/admin/results' : '/');
  };

  if (!CurrentStepComponent) return null;

  const stepProps = {
    wizardData: { ...wizardData, totalSteps: STEPS.length },
    onNext:   handleNext,
    onBack:   handleBack,
    onCancel: handleCancelRequest,
  };

  return (
    <div className="min-h-svh bg-background px-4 py-8 md:py-12">
      <div className={cn('mx-auto', WIDE_STEP_IDS.has(currentStepId) ? 'max-w-[100rem]' : 'max-w-5xl')}>
        <h1 className="mb-8 text-center text-2xl font-semibold tracking-tight md:text-3xl">
          Upload Race Results
        </h1>

        <div className="mb-8">
          <ol className="mb-4 flex items-start justify-between gap-2">
            {STEPS.map((step, idx) => {
              const isActive = step.id === currentStepId;
              const isCompleted = idx < currentIndex;
              return (
                <li key={step.id} className="flex flex-1 flex-col items-center gap-2 text-center">
                  <div
                    className={cn(
                      'flex size-8 shrink-0 items-center justify-center rounded-full border text-sm font-semibold transition-colors',
                      isCompleted && 'border-success bg-success/15 text-success',
                      isActive && 'border-primary bg-primary text-primary-foreground',
                      !isActive && !isCompleted && 'border-border bg-muted text-muted-foreground'
                    )}
                  >
                    {isCompleted ? <Check className="size-4" /> : idx + 1}
                  </div>
                  <span
                    className={cn(
                      'text-xs font-medium sm:text-sm',
                      isActive ? 'text-foreground' : 'text-muted-foreground'
                    )}
                  >
                    {step.name}
                  </span>
                </li>
              );
            })}
          </ol>
          <Progress value={STEPS.length > 1 ? (currentIndex / (STEPS.length - 1)) * 100 : 0} />
        </div>

        <Card>
          <CardContent className="p-6 md:p-8">
            {resumeLoading ? (
              <div className="flex items-center justify-center gap-2 py-12 text-muted-foreground">
                <Loader2 className="size-4 animate-spin" /> Loading upload…
              </div>
            ) : resumeError ? (
              <div className="flex flex-col items-center gap-4 py-12 text-center">
                <p className="text-destructive">Couldn&rsquo;t load this upload: {resumeError}</p>
                <Button variant="outline" onClick={() => navigate('/admin/results')}>
                  Back to Results Management
                </Button>
              </div>
            ) : (
              <CurrentStepComponent {...stepProps} />
            )}
          </CardContent>
        </Card>
      </div>

      <AlertDialog open={cancelDialogOpen} onOpenChange={setCancelDialogOpen}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Cancel this upload?</AlertDialogTitle>
            <AlertDialogDescription>
              Any changes since your last Save Draft will be lost. The uploaded file stays
              pending in Results Management, so you can resume it later.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Keep editing</AlertDialogCancel>
            <AlertDialogAction onClick={handleConfirmCancel}>Discard and cancel</AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
