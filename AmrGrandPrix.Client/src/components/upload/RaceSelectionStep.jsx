/**
 * Step 1: Race Selection
 * Allows user to select an existing race or create a new one
 */

import { useState, useEffect } from 'react';
import { useForm, Controller } from 'react-hook-form';
import { AlertCircle, Loader2 } from 'lucide-react';
import * as tokenService from '../../services/tokenService';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Skeleton } from '@/components/ui/skeleton';
import { Alert, AlertDescription } from '@/components/ui/alert';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';

export default function RaceSelectionStep({ wizardData, onNext, onCancel }) {
  const [isCreatingNew, setIsCreatingNew] = useState(false);
  const [races, setRaces] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  const {
    control,
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
      const token = tokenService.getAccessToken();
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

  // base-ui's Select doesn't resolve a selected item's label from its children the
  // way Radix does - it needs an explicit lookup to render anything but the raw value.
  const raceOptionLabel = (raceId) => {
    if (!raceId) return null;
    if (raceId === 'new') return '+ Create New Race';
    const race = races.find(r => r.raceId === raceId);
    if (!race) return raceId;
    return `${race.name} - ${new Date(race.date).toLocaleDateString()}${race.courseVariant ? ` (${race.courseVariant})` : ''}`;
  };

  // When selecting an existing race, populate the form
  const handleRaceSelection = (raceId) => {
    if (raceId && raceId !== 'new') {
      const selectedRace = races.find(r => r.raceId === raceId);
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

  const onSubmit = async (data) => {
    // If creating a new race, create it in the database first
    if (data.raceId === 'new') {
      try {
        setLoading(true);
        setError(null);

        const token = tokenService.getAccessToken();
        const response = await fetch('/api/races', {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
            'Authorization': `Bearer ${token}`,
          },
          body: JSON.stringify({
            name: data.raceName,
            date: data.raceDate,
            isGrandPrixRace: data.isGrandPrixRace,
            courseVariant: data.courseVariant || null,
            location: null,
          }),
        });

        if (!response.ok) {
          let errorMessage = 'Failed to create race';
          try {
            // Read as text first, then try to parse as JSON
            const responseText = await response.text();
            if (responseText) {
              try {
                const errorData = JSON.parse(responseText);
                errorMessage = errorData.message || errorData.title || responseText;
              } catch {
                errorMessage = responseText;
              }
            } else {
              errorMessage = `HTTP ${response.status}: ${response.statusText}`;
            }
          } catch {
            errorMessage = `HTTP ${response.status}: ${response.statusText}`;
          }
          throw new Error(errorMessage);
        }

        const createdRace = await response.json();

        // Update the data with the newly created race ID
        data.raceId = createdRace.raceId;

        onNext({ raceSelection: data });
      } catch (err) {
        setError(err.message);
        console.error('Error creating race:', err);
      } finally {
        setLoading(false);
      }
    } else {
      // Existing race selected, proceed normally
      onNext({ raceSelection: data });
    }
  };

  if (loading) {
    return (
      <div>
        <div className="mb-6 flex items-center justify-between border-b border-border pb-4">
          <h2 className="text-xl font-semibold">Step 1: Race Selection</h2>
          <span className="text-sm text-muted-foreground">Step 1 of 4</span>
        </div>
        <div className="flex flex-col gap-4">
          <Skeleton className="h-4 w-32" />
          <Skeleton className="h-8 w-full" />
          <Skeleton className="h-4 w-24" />
          <Skeleton className="h-8 w-full" />
        </div>
      </div>
    );
  }

  return (
    <div>
      <div className="mb-6 flex items-center justify-between border-b border-border pb-4">
        <h2 className="text-xl font-semibold">Step 1: Race Selection</h2>
        <span className="text-sm text-muted-foreground">Step 1 of 4</span>
      </div>

      {error && (
        <Alert variant="destructive" className="mb-6">
          <AlertCircle />
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      )}

      <form onSubmit={handleSubmit(onSubmit)} className="flex flex-col gap-5">
        <div className="flex flex-col gap-2">
          <Label htmlFor="raceId">Select Race</Label>
          <Controller
            name="raceId"
            control={control}
            rules={{ required: 'Please select a race or create new' }}
            render={({ field }) => (
              <Select
                value={field.value || undefined}
                onValueChange={(value) => {
                  field.onChange(value);
                  handleRaceSelection(value);
                }}
              >
                <SelectTrigger
                  id="raceId"
                  className="w-full"
                  aria-invalid={!!errors.raceId}
                >
                  <SelectValue placeholder="-- Select a race --">
                    {(value) => raceOptionLabel(value) || '-- Select a race --'}
                  </SelectValue>
                </SelectTrigger>
                <SelectContent>
                  {races.map(race => (
                    <SelectItem key={race.raceId} value={race.raceId}>
                      {race.name} - {new Date(race.date).toLocaleDateString()}
                      {race.courseVariant ? ` (${race.courseVariant})` : ''}
                    </SelectItem>
                  ))}
                  <SelectItem value="new">+ Create New Race</SelectItem>
                </SelectContent>
              </Select>
            )}
          />
          {errors.raceId && (
            <p className="text-sm text-destructive">{errors.raceId.message}</p>
          )}
        </div>

        {(isCreatingNew || selectedRaceId === 'new') && (
          <>
            <div className="flex flex-col gap-2">
              <Label htmlFor="raceName">Race Name</Label>
              <Input
                type="text"
                id="raceName"
                aria-invalid={!!errors.raceName}
                {...register('raceName', {
                  required: isCreatingNew ? 'Race name is required' : false
                })}
                placeholder="e.g., Mount Marathon Race"
              />
              {errors.raceName && (
                <p className="text-sm text-destructive">{errors.raceName.message}</p>
              )}
            </div>

            <div className="flex flex-col gap-2">
              <Label htmlFor="raceDate">Race Date</Label>
              <Input
                type="date"
                id="raceDate"
                aria-invalid={!!errors.raceDate}
                {...register('raceDate', {
                  required: isCreatingNew ? 'Race date is required' : false
                })}
              />
              {errors.raceDate && (
                <p className="text-sm text-destructive">{errors.raceDate.message}</p>
              )}
            </div>

            <label className="flex items-center gap-2 text-sm font-medium">
              <input
                type="checkbox"
                className="size-4 rounded border-input accent-primary"
                {...register('isGrandPrixRace')}
              />
              This is a Grand Prix race
            </label>

            {isGrandPrixRace && (
              <div className="flex flex-col gap-2">
                <Label htmlFor="courseVariant">Course Variant (optional)</Label>
                <Input
                  type="text"
                  id="courseVariant"
                  {...register('courseVariant')}
                  placeholder="e.g., Full Monty, Uphill Only"
                />
                <p className="text-xs text-muted-foreground">
                  Specify the variant if this race has multiple course options
                </p>
              </div>
            )}
          </>
        )}

        <div className="mt-4 flex flex-col-reverse gap-3 border-t border-border pt-6 sm:flex-row sm:justify-end">
          <Button type="button" variant="outline" onClick={onCancel}>
            Cancel
          </Button>
          <Button type="submit" disabled={loading}>
            {loading ? <Loader2 className="animate-spin" /> : null}
            Next &rarr;
          </Button>
        </div>
      </form>
    </div>
  );
}
