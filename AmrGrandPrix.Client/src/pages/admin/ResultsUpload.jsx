/**
 * Results Upload Wizard
 * Multi-step wizard for uploading and processing race results
 */

import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import RaceSelectionStep from '../../components/upload/RaceSelectionStep';
import FileUploadStep from '../../components/upload/FileUploadStep';
import ColumnMappingStep from '../../components/upload/ColumnMappingStep';
import DataReviewStep from '../../components/upload/DataReviewStep';
import ConfirmationStep from '../../components/upload/ConfirmationStep';
import '../../components/upload/upload.css';

const STEPS = [
  { id: 1, name: 'Race Selection', component: RaceSelectionStep },
  { id: 2, name: 'File Upload', component: FileUploadStep },
  { id: 3, name: 'Column Mapping', component: ColumnMappingStep },
  { id: 4, name: 'Data Review', component: DataReviewStep },
  { id: 5, name: 'Confirmation', component: ConfirmationStep },
];

export default function ResultsUpload() {
  const [currentStep, setCurrentStep] = useState(1);
  const [wizardData, setWizardData] = useState({});
  const navigate = useNavigate();

  const handleNext = (stepData) => {
    setWizardData(prev => ({ ...prev, ...stepData }));
    setCurrentStep(prev => Math.min(prev + 1, STEPS.length));
  };

  const handleBack = () => {
    setCurrentStep(prev => Math.max(prev - 1, 1));
  };

  const handleCancel = () => {
    if (window.confirm('Are you sure you want to cancel? All progress will be lost.')) {
      navigate('/');
    }
  };

  const CurrentStepComponent = STEPS[currentStep - 1].component;

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
                  step.id === currentStep
                    ? 'active'
                    : step.id < currentStep
                      ? 'completed'
                      : ''
                }`}
              >
                <div className="step-number">
                  {step.id < currentStep ? '✓' : step.id}
                </div>
                <div className="step-name">{step.name}</div>
                {idx < STEPS.length - 1 && <div className="step-connector" />}
              </div>
            ))}
          </div>
        </div>

        <div className="upload-content">
          <CurrentStepComponent
            wizardData={wizardData}
            onNext={handleNext}
            onBack={handleBack}
            onCancel={handleCancel}
          />
        </div>
      </div>
    </div>
  );
}
