/**
 * Step 1: Race Selection
 * Series-first: pick a race series, then either add results to an existing race instance in that
 * series (e.g. one that's missing finishers) or create a new instance on a new date (race name is
 * inherited from the series). "+ Create New Race" covers an event that isn't in any series yet.
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

const NEW_RACE = '__new_race__';   // top-level: an event with no series yet
const NEW_DATE = '__new_date__';   // within an existing series: a new instance
const NEW_SERIES = '__new_series__'; // nested, within NEW_RACE: create a matching series too

const authHeaders = () => ({
  'Content-Type': 'application/json',
  Authorization: `Bearer ${tokenService.getAccessToken()}`,
});

export default function RaceSelectionStep({ wizardData, onNext, onCancel }) {
  const [raceSeriesList, setRaceSeriesList] = useState([]);
  const [seriesDetail, setSeriesDetail] = useState(null);
  const [seriesDetailLoading, setSeriesDetailLoading] = useState(false);
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
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
      seriesSelection: '',
      raceInstanceId: '',
      raceId: '',
      raceName: '',
      raceDate: new Date().toISOString().split('T')[0],
      isGrandPrixRace: false,
      courseVariant: '',
      newRaceSeriesId: NEW_SERIES,
      newRaceSeriesName: '',
    },
  });

  const seriesSelection = watch('seriesSelection');
  const raceInstanceId = watch('raceInstanceId');
  const newRaceSeriesId = watch('newRaceSeriesId');

  const isNewRaceMode = seriesSelection === NEW_RACE;
  const isExistingSeriesMode = !!seriesSelection && !isNewRaceMode;
  const isNewDateMode = isExistingSeriesMode && raceInstanceId === NEW_DATE;
  const isExistingRaceMode = isExistingSeriesMode && !!raceInstanceId && raceInstanceId !== NEW_DATE;
  const showRaceDateAndDetails = isNewRaceMode || isNewDateMode;

  useEffect(() => {
    fetch('/api/race-series')
      .then((r) => (r.ok ? r.json() : []))
      .then(setRaceSeriesList)
      .catch(() => setRaceSeriesList([]))
      .finally(() => setLoading(false));
  }, []);

  // When an existing series is picked, load its race instances.
  useEffect(() => {
    if (!isExistingSeriesMode) {
      setSeriesDetail(null);
      return;
    }
    setSeriesDetailLoading(true);
    fetch(`/api/race-series/${seriesSelection}`)
      .then((r) => (r.ok ? r.json() : null))
      .then(setSeriesDetail)
      .catch(() => setSeriesDetail(null))
      .finally(() => setSeriesDetailLoading(false));
  }, [seriesSelection, isExistingSeriesMode]);

  const seriesOptionLabel = (value) => {
    if (value === NEW_RACE) return '+ Create New Race';
    return raceSeriesList.find((s) => s.raceSeriesId === value)?.name || null;
  };

  const raceInstanceOptionLabel = (value) => {
    if (value === NEW_DATE) return '+ New Date';
    const race = seriesDetail?.races.find((r) => r.raceId === value);
    if (!race) return null;
    return `${race.date} — ${race.courseVariant || 'Standard'} (${race.resultsCount} result${race.resultsCount === 1 ? '' : 's'})`;
  };

  const handleSeriesChange = (value) => {
    setValue('seriesSelection', value);
    setValue('raceInstanceId', '');
    setValue('raceId', '');
    setValue('raceDate', new Date().toISOString().split('T')[0]);
    setValue('isGrandPrixRace', false);
    setValue('courseVariant', '');

    if (value === NEW_RACE) {
      setValue('raceName', '');
      setValue('newRaceSeriesId', NEW_SERIES);
      setValue('newRaceSeriesName', '');
    }
  };

  const handleRaceInstanceChange = (value) => {
    setValue('raceInstanceId', value);

    if (value === NEW_DATE) {
      setValue('raceId', '');
      setValue('raceName', seriesDetail?.name || '');
      setValue('raceDate', new Date().toISOString().split('T')[0]);
      setValue('isGrandPrixRace', false);
      setValue('courseVariant', '');
      return;
    }

    const race = seriesDetail?.races.find((r) => r.raceId === value);
    if (race) {
      setValue('raceId', race.raceId);
      setValue('raceName', seriesDetail.name);
      setValue('raceDate', race.date);
      setValue('isGrandPrixRace', race.isGrandPrixRace);
      setValue('courseVariant', race.courseVariant || '');
    }
  };

  // Course/variant names already on record for this series' other race instances - passed
  // through as an LLM hint in Step 2 so a multi-variant file gets labeled consistently.
  const knownVariants = isExistingSeriesMode
    ? [...new Set((seriesDetail?.races || []).map((r) => r.courseVariant).filter(Boolean))]
    : [];

  const onSubmit = async (data) => {
    // Existing race instance selected - nothing to create, just proceed.
    if (isExistingRaceMode) {
      onNext({ raceSelection: { ...data, knownVariants, raceSeriesId: seriesSelection } });
      return;
    }

    try {
      setSubmitting(true);
      setError(null);

      let raceSeriesId = null;
      if (isNewDateMode) {
        raceSeriesId = seriesSelection;
      } else if (isNewRaceMode) {
        if (data.newRaceSeriesId === NEW_SERIES && data.newRaceSeriesName?.trim()) {
          const seriesResponse = await fetch('/api/race-series', {
            method: 'POST',
            headers: authHeaders(),
            body: JSON.stringify({ name: data.newRaceSeriesName.trim() }),
          });
          if (seriesResponse.ok) {
            const createdSeries = await seriesResponse.json();
            raceSeriesId = createdSeries.raceSeriesId;
          }
        } else if (data.newRaceSeriesId && data.newRaceSeriesId !== NEW_SERIES) {
          raceSeriesId = data.newRaceSeriesId;
        }
      }

      const response = await fetch('/api/races', {
        method: 'POST',
        headers: authHeaders(),
        body: JSON.stringify({
          name: data.raceName,
          date: data.raceDate,
          isGrandPrixRace: data.isGrandPrixRace,
          courseVariant: data.courseVariant || null,
          location: null,
          raceSeriesId,
        }),
      });

      if (!response.ok) {
        const responseText = await response.text().catch(() => '');
        let errorMessage = `HTTP ${response.status}: ${response.statusText}`;
        if (responseText) {
          try {
            const errorData = JSON.parse(responseText);
            errorMessage = errorData.message || errorData.title || responseText;
          } catch {
            errorMessage = responseText;
          }
        }
        throw new Error(errorMessage);
      }

      const createdRace = await response.json();
      onNext({ raceSelection: { ...data, raceId: createdRace.raceId, knownVariants, raceSeriesId } });
    } catch (err) {
      setError(err.message);
      console.error('Error creating race:', err);
    } finally {
      setSubmitting(false);
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
          <Label htmlFor="seriesSelection">Race Series</Label>
          <Controller
            name="seriesSelection"
            control={control}
            rules={{ required: 'Please select a race series or create a new race' }}
            render={({ field }) => (
              <Select
                value={field.value || undefined}
                onValueChange={(value) => {
                  field.onChange(value);
                  handleSeriesChange(value);
                }}
              >
                <SelectTrigger id="seriesSelection" className="w-full" aria-invalid={!!errors.seriesSelection}>
                  <SelectValue placeholder="-- Select a race series --">
                    {(value) => seriesOptionLabel(value) || '-- Select a race series --'}
                  </SelectValue>
                </SelectTrigger>
                <SelectContent>
                  {raceSeriesList.map((s) => (
                    <SelectItem key={s.raceSeriesId} value={s.raceSeriesId}>{s.name}</SelectItem>
                  ))}
                  <SelectItem value={NEW_RACE}>+ Create New Race</SelectItem>
                </SelectContent>
              </Select>
            )}
          />
          {errors.seriesSelection && (
            <p className="text-sm text-destructive">{errors.seriesSelection.message}</p>
          )}
        </div>

        {isExistingSeriesMode && (
          seriesDetailLoading ? (
            <Skeleton className="h-8 w-full" />
          ) : (
            <div className="flex flex-col gap-2">
              <Label htmlFor="raceInstanceId">Race Instance</Label>
              <Controller
                name="raceInstanceId"
                control={control}
                rules={{ required: 'Please select a race instance or a new date' }}
                render={({ field }) => (
                  <Select
                    value={field.value || undefined}
                    onValueChange={(value) => {
                      field.onChange(value);
                      handleRaceInstanceChange(value);
                    }}
                  >
                    <SelectTrigger id="raceInstanceId" className="w-full" aria-invalid={!!errors.raceInstanceId}>
                      <SelectValue placeholder="-- Select a race instance --">
                        {(value) => raceInstanceOptionLabel(value) || '-- Select a race instance --'}
                      </SelectValue>
                    </SelectTrigger>
                    <SelectContent>
                      {seriesDetail?.races.map((r) => (
                        <SelectItem key={r.raceId} value={r.raceId}>
                          {r.date} — {r.courseVariant || 'Standard'} ({r.resultsCount} result{r.resultsCount === 1 ? '' : 's'})
                        </SelectItem>
                      ))}
                      <SelectItem value={NEW_DATE}>+ New Date</SelectItem>
                    </SelectContent>
                  </Select>
                )}
              />
              {errors.raceInstanceId && (
                <p className="text-sm text-destructive">{errors.raceInstanceId.message}</p>
              )}
              <p className="text-xs text-muted-foreground">
                Pick an existing date to add more results to it (e.g. one that's missing
                finishers), or "+ New Date" to record a new running of this race.
              </p>
            </div>
          )
        )}

        {isExistingRaceMode && (
          <div className="rounded-lg border border-border bg-muted/30 p-4 text-sm">
            <dl className="grid grid-cols-2 gap-2">
              <dt className="text-muted-foreground">Race Name</dt>
              <dd className="font-medium">{seriesDetail?.name}</dd>
              <dt className="text-muted-foreground">Grand Prix Race</dt>
              <dd className="font-medium">{watch('isGrandPrixRace') ? 'Yes' : 'No'}</dd>
              {watch('courseVariant') && (
                <>
                  <dt className="text-muted-foreground">Course Variant</dt>
                  <dd className="font-medium">{watch('courseVariant')}</dd>
                </>
              )}
            </dl>
            <p className="mt-2 text-xs text-muted-foreground">
              These are fixed for an existing race and can't be changed here.
            </p>
          </div>
        )}

        {isNewRaceMode && (
          <div className="flex flex-col gap-2">
            <Label htmlFor="raceName">Race Name</Label>
            <Input
              type="text"
              id="raceName"
              aria-invalid={!!errors.raceName}
              {...register('raceName', { required: 'Race name is required' })}
              placeholder="e.g., Mount Marathon Race"
            />
            {errors.raceName && (
              <p className="text-sm text-destructive">{errors.raceName.message}</p>
            )}
          </div>
        )}

        {isNewDateMode && (
          <p className="text-sm text-muted-foreground">
            Race name: <span className="font-medium text-foreground">{seriesDetail?.name}</span>
          </p>
        )}

        {showRaceDateAndDetails && (
          <>
            <div className="flex flex-col gap-2">
              <Label htmlFor="raceDate">Race Date</Label>
              <Input
                type="date"
                id="raceDate"
                aria-invalid={!!errors.raceDate}
                {...register('raceDate', { required: 'Race date is required' })}
              />
              {errors.raceDate && (
                <p className="text-sm text-destructive">{errors.raceDate.message}</p>
              )}
            </div>

            {isNewRaceMode && (
              <div className="flex flex-col gap-2">
                <Label htmlFor="newRaceSeriesId">Race Series (optional)</Label>
                <Controller
                  name="newRaceSeriesId"
                  control={control}
                  render={({ field }) => (
                    <Select value={field.value || undefined} onValueChange={field.onChange}>
                      <SelectTrigger id="newRaceSeriesId" className="w-full">
                        <SelectValue placeholder="-- No series --">
                          {(value) =>
                            value === NEW_SERIES
                              ? '+ Create New Series'
                              : raceSeriesList.find((s) => s.raceSeriesId === value)?.name || '-- No series --'
                          }
                        </SelectValue>
                      </SelectTrigger>
                      <SelectContent>
                        {raceSeriesList.map((s) => (
                          <SelectItem key={s.raceSeriesId} value={s.raceSeriesId}>{s.name}</SelectItem>
                        ))}
                        <SelectItem value={NEW_SERIES}>+ Create New Series</SelectItem>
                      </SelectContent>
                    </Select>
                  )}
                />
                {newRaceSeriesId === NEW_SERIES && (
                  <Input
                    type="text"
                    placeholder="New series name, e.g. Mount Marathon Race"
                    {...register('newRaceSeriesName')}
                  />
                )}
              </div>
            )}

            <label className="flex items-center gap-2 text-sm font-medium">
              <input
                type="checkbox"
                className="size-4 rounded border-input accent-primary"
                {...register('isGrandPrixRace')}
              />
              This is a Grand Prix race
            </label>
          </>
        )}

        <div className="mt-4 flex flex-col-reverse gap-3 border-t border-border pt-6 sm:flex-row sm:justify-end">
          <Button type="button" variant="outline" onClick={onCancel}>
            Cancel
          </Button>
          <Button type="submit" disabled={submitting}>
            {submitting ? <Loader2 className="animate-spin" /> : null}
            Next &rarr;
          </Button>
        </div>
      </form>
    </div>
  );
}
