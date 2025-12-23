/**
 * Step 1: Race Selection
 * Allows user to select an existing race or create a new one
 */

import { useState, useEffect } from 'react';
import { useForm } from 'react-hook-form';

export default function RaceSelectionStep({ wizardData, onNext, onCancel }) {
  const [isCreatingNew, setIsCreatingNew] = useState(false);
  const [races, setRaces] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  const {
    register,
    handleSubmit,
    watch,
    setValue,
    formState: { errors },
  } = useForm({
    defaultValues: wizardData.raceSelection || {
      raceId: '',
      raceName: '',
      raceDate: new Date().toISOString().split('T')[0],
      isGrandPrixRace: false,
      courseVariant: '',
    },
  });

  const selectedRaceId = watch('raceId');
  const isGrandPrixRace = watch('isGrandPrixRace');

  // Fetch existing races
  useEffect(() => {
    fetchRaces();
  }, []);

  const fetchRaces = async () => {
    try {
      setLoading(true);
      const token = localStorage.getItem('token');
      const currentYear = new Date().getFullYear();

      const response = await fetch(`/api/races/${currentYear}`, {
        headers: {
          'Authorization': `Bearer ${token}`,
        },
      });

      if (!response.ok) {
        throw new Error('Failed to fetch races');
      }

      const data = await response.json();
      setRaces(data);
    } catch (err) {
      setError(err.message);
      console.error('Error fetching races:', err);
    } finally {
      setLoading(false);
    }
  };

  // When selecting an existing race, populate the form
  const handleRaceSelection = (e) => {
    const raceId = e.target.value;
    setValue('raceId', raceId);

    if (raceId && raceId !== 'new') {
      const selectedRace = races.find(r => r.id === raceId);
      if (selectedRace) {
        setValue('raceName', selectedRace.name);
        setValue('raceDate', selectedRace.date.split('T')[0]);
        setValue('isGrandPrixRace', selectedRace.isGrandPrixRace);
        setValue('courseVariant', selectedRace.courseVariant || '');
        setIsCreatingNew(false);
      }
    } else if (raceId === 'new') {
      setIsCreatingNew(true);
      setValue('raceName', '');
      setValue('raceDate', new Date().toISOString().split('T')[0]);
      setValue('isGrandPrixRace', false);
      setValue('courseVariant', '');
    }
  };

  const onSubmit = (data) => {
    onNext({ raceSelection: data });
  };

  if (loading) {
    return (
      <div className="wizard-step">
        <div className="step-header">
          <h2>Step 1: Race Selection</h2>
          <span className="step-indicator">Step 1 of 5</span>
        </div>
        <div className="loading">Loading races...</div>
      </div>
    );
  }

  return (
    <div className="wizard-step">
      <div className="step-header">
        <h2>Step 1: Race Selection</h2>
        <span className="step-indicator">Step 1 of 5</span>
      </div>

      {error && (
        <div className="error-message">
          {error}
        </div>
      )}

      <form onSubmit={handleSubmit(onSubmit)} className="race-selection-form">
        <div className="form-group">
          <label htmlFor="raceId">Select Race:</label>
          <select
            id="raceId"
            {...register('raceId', { required: 'Please select a race or create new' })}
            onChange={handleRaceSelection}
            className={errors.raceId ? 'error' : ''}
          >
            <option value="">-- Select a race --</option>
            {races.map(race => (
              <option key={race.id} value={race.id}>
                {race.name} - {new Date(race.date).toLocaleDateString()}
                {race.courseVariant ? ` (${race.courseVariant})` : ''}
              </option>
            ))}
            <option value="new">+ Create New Race</option>
          </select>
          {errors.raceId && (
            <span className="field-error">{errors.raceId.message}</span>
          )}
        </div>

        {(isCreatingNew || selectedRaceId === 'new') && (
          <>
            <div className="form-group">
              <label htmlFor="raceName">Race Name:</label>
              <input
                type="text"
                id="raceName"
                {...register('raceName', {
                  required: isCreatingNew ? 'Race name is required' : false
                })}
                placeholder="e.g., Mount Marathon Race"
                className={errors.raceName ? 'error' : ''}
              />
              {errors.raceName && (
                <span className="field-error">{errors.raceName.message}</span>
              )}
            </div>

            <div className="form-group">
              <label htmlFor="raceDate">Race Date:</label>
              <input
                type="date"
                id="raceDate"
                {...register('raceDate', {
                  required: isCreatingNew ? 'Race date is required' : false
                })}
                className={errors.raceDate ? 'error' : ''}
              />
              {errors.raceDate && (
                <span className="field-error">{errors.raceDate.message}</span>
              )}
            </div>

            <div className="form-group checkbox-group">
              <label>
                <input
                  type="checkbox"
                  {...register('isGrandPrixRace')}
                />
                This is a Grand Prix race
              </label>
            </div>

            {isGrandPrixRace && (
              <div className="form-group">
                <label htmlFor="courseVariant">Course Variant (optional):</label>
                <input
                  type="text"
                  id="courseVariant"
                  {...register('courseVariant')}
                  placeholder="e.g., Full Monty, Uphill Only"
                />
                <small className="field-hint">
                  Specify the variant if this race has multiple course options
                </small>
              </div>
            )}
          </>
        )}

        <div className="form-actions">
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
