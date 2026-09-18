/**
 * Results Upload Wizard
 * Multi-step wizard: Race Selection → File Upload → Data Review → Confirmation
 */

import { useState } from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import RaceSelectionStep from '../../components/upload/RaceSelectionStep';
import FileUploadStep    from '../../components/upload/FileUploadStep';
import DataReviewStep    from '../../components/upload/DataReviewStep';
import ConfirmationStep  from '../../components/upload/ConfirmationStep';
import '../../components/upload/upload.css';

const STEPS = [
  { id: 'race-selection', name: 'Race Selection', component: RaceSelectionStep },
  { id: 'file-upload',    name: 'File Upload',    component: FileUploadStep    },
  { id: 'data-review',    name: 'Data Review',    component: DataReviewStep    },
  { id: 'confirmation',   name: 'Confirmation',   component: ConfirmationStep  },
];

export default function ResultsUpload() {
  const location = useLocation();
  const resumeData = location.state?.resume || null;

  const [currentStepId, setCurrentStepId] = useState(resumeData ? 'data-review' : 'race-selection');
  const [wizardData, setWizardData]       = useState(resumeData || {});
  const navigate = useNavigate();

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
    if (resumeData && currentStepId === 'data-review') {
      navigate('/admin/results');
      return;
    }
    if (currentIndex > 0) setCurrentStepId(STEPS[currentIndex - 1].id);
  };

  const handleCancel = () => {
    if (window.confirm('Are you sure you want to cancel? All progress will be lost.'))
      navigate(resumeData ? '/admin/results' : '/');
  };

  if (!CurrentStepComponent) return null;

  const stepProps = {
    wizardData: { ...wizardData, totalSteps: STEPS.length },
    onNext:   handleNext,
    onBack:   handleBack,
    onCancel: handleCancel,
  };

  return (
    <div className="results-upload-page">
      <div className="upload-container">
        <div className="upload-header">
          <h1>Upload Race Results</h1>
          <div className="progress-indicator">
            {STEPS.map((step, idx) => (
              <div
                key={step.id}
                className={`progress-step ${
                  step.id === currentStepId
                    ? 'active'
                    : idx < currentIndex
                      ? 'completed'
                      : ''
                }`}
              >
                <div className="step-number">
                  {idx < currentIndex ? '✓' : idx + 1}
                </div>
                <div className="step-name">{step.name}</div>
                {idx < STEPS.length - 1 && <div className="step-connector" />}
              </div>
            ))}
          </div>
        </div>

        <div className="upload-content">
          <CurrentStepComponent {...stepProps} />
        </div>
      </div>
    </div>
  );
}
