/**
 * Step 1: Race Selection
 * Series-first: pick a race series, then either add results to an existing race (one year's
 * running of one of the series' course variants, e.g. one that's missing finishers) or record a
 * new race by choosing the variant and date. Variants are reused year over year so results stay
 * comparable; "+ New variant" and "+ New Series" cover a course or event not seen before.
 * For a series with several variants the admin ticks which ones the results file contains. Ticking
 * more than one makes a multi-variant upload: the extractor is told exactly which variants to
 * expect, and each variant's race is created (or an existing one for the year reused) only when
 * results for it are saved in Confirmation.
 */

import { useState, useEffect } from 'react';
import { useForm, Controller } from 'react-hook-form';
import { AlertCircle, Loader2 } from 'lucide-react';
import * as raceService from '../../services/raceService';
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

const NEW_SERIES = '__new_series__';   // top-level: an event with no series yet
const NEW_RACE = '__new_race__';       // within an existing series: a new year/variant
const NEW_VARIANT = '__new_variant__'; // within NEW_RACE: a course not run before

const today = () => new Date().toISOString().split('T')[0];

const pluralResults = (n) => `${n} result${n === 1 ? '' : 's'}`;

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
      raceVariantId: '',
      newVariantName: '',
      newSeriesName: '',
      raceDate: today(),
      isGrandPrixRace: false,
      // Multi-variant series: the variants ticked as in the file, and which count toward the GP.
      selectedVariantIds: [],
      variantGrandPrix: {},
    },
  });

  const seriesSelection = watch('seriesSelection');
  const raceInstanceId = watch('raceInstanceId');
  const raceVariantId = watch('raceVariantId');
  const raceDate = watch('raceDate');
  const selectedVariantIds = watch('selectedVariantIds') || [];

  const isNewSeriesMode = seriesSelection === NEW_SERIES;
  const isExistingSeriesMode = !!seriesSelection && !isNewSeriesMode;
  const isNewRaceMode = isExistingSeriesMode && raceInstanceId === NEW_RACE;
  const isExistingRaceMode = isExistingSeriesMode && !!raceInstanceId && raceInstanceId !== NEW_RACE;
  const variants = seriesDetail?.variants || [];
  const hasMultipleVariants = variants.length > 1;

  const isNewVariantMode = isNewRaceMode && (raceVariantId === NEW_VARIANT || variants.length === 0);
  // Variant checklist (series with several variants): 2+ ticked = one file, several races.
  const isChecklistMode = isNewRaceMode && hasMultipleVariants && !isNewVariantMode;
  const isMultiVariantMode = isChecklistMode && selectedVariantIds.length > 1;
  // The single variant a new race is being created for, if not a new variant.
  const singleVariantId = isChecklistMode
    ? (selectedVariantIds.length === 1 ? selectedVariantIds[0] : null)
    : (isNewRaceMode && !isNewVariantMode ? raceVariantId : null);
  const variantById = (id) => variants.find((v) => v.raceVariantId === id);
  const selectedRace = seriesDetail?.races.find((r) => r.raceId === raceInstanceId);

  const existingRaceFor = (variantId) =>
    seriesDetail?.races.find(
      (r) => r.raceVariantId === variantId && r.year === raceService.yearOf(raceDate)
    );

  // A new race must not duplicate an existing variant + year. (A multi-variant upload reuses them.)
  const duplicateRace = singleVariantId ? existingRaceFor(singleVariantId) : null;

  useEffect(() => {
    raceService
      .getSeriesList()
      .then(setRaceSeriesList)
      .catch(() => setRaceSeriesList([]))
      .finally(() => setLoading(false));
  }, []);

  // When an existing series is picked, load its variants and race instances.
  useEffect(() => {
    if (!isExistingSeriesMode) {
      setSeriesDetail(null);
      return;
    }
    setSeriesDetailLoading(true);
    raceService
      .getSeriesDetail(seriesSelection)
      .then(setSeriesDetail)
      .catch(() => setSeriesDetail(null))
      .finally(() => setSeriesDetailLoading(false));
  }, [seriesSelection, isExistingSeriesMode]);

  const seriesOptionLabel = (value) => {
    if (value === NEW_SERIES) return '+ New Series';
    return raceSeriesList.find((s) => s.raceSeriesId === value)?.name || null;
  };

  const raceInstanceLabel = (race) =>
    `${race.date}${hasMultipleVariants ? ` — ${variantById(race.raceVariantId)?.name}` : ''} (${pluralResults(race.resultsCount)})`;

  const raceInstanceOptionLabel = (value) => {
    if (value === NEW_RACE) return '+ New race';
    const race = seriesDetail?.races.find((r) => r.raceId === value);
    return race ? raceInstanceLabel(race) : null;
  };

  const handleSeriesChange = (value) => {
    setValue('seriesSelection', value);
    setValue('raceInstanceId', '');
    setValue('raceVariantId', '');
    setValue('newVariantName', '');
    setValue('newSeriesName', '');
    setValue('raceDate', today());
    setValue('isGrandPrixRace', false);
    setValue('selectedVariantIds', []);
    setValue('variantGrandPrix', {});
  };

  const handleVariantChange = (value) => {
    setValue('raceVariantId', value);
    setValue('isGrandPrixRace', value === NEW_VARIANT ? false : !!variantById(value)?.isGrandPrixByDefault);
  };

  const toggleVariant = (variantId, included) => {
    setValue(
      'selectedVariantIds',
      included
        ? variants.map((v) => v.raceVariantId).filter((id) => id === variantId || selectedVariantIds.includes(id))
        : selectedVariantIds.filter((id) => id !== variantId),
      { shouldValidate: true }
    );
  };

  const handleRaceInstanceChange = (value) => {
    setValue('raceInstanceId', value);
    if (value !== NEW_RACE) return;

    setValue('raceDate', today());

    // Several variants: the admin ticks which ones the file contains.
    if (hasMultipleVariants) {
      setValue('raceVariantId', '');
      setValue('selectedVariantIds', []);
      setValue(
        'variantGrandPrix',
        Object.fromEntries(variants.map((v) => [v.raceVariantId, !!v.isGrandPrixByDefault]))
      );
      return;
    }

    // Default to the variant that counts toward the GP (or the first one).
    const defaultVariant = variants.find((v) => v.isGrandPrixByDefault) || variants[0];
    setValue('raceVariantId', defaultVariant?.raceVariantId || NEW_VARIANT);
    setValue('isGrandPrixRace', !!defaultVariant?.isGrandPrixByDefault);
  };

  const onSubmit = async (data) => {
    try {
      setSubmitting(true);
      setError(null);

      // Existing race selected - nothing to create.
      if (isExistingRaceMode) {
        const variant = variantById(selectedRace.raceVariantId);
        onNext({
          raceSelection: {
            ...data,
            includedVariantIds: null,
            raceId: selectedRace.raceId,
            raceName: seriesDetail.name,
            raceDate: selectedRace.date,
            isGrandPrixRace: selectedRace.isGrandPrixRace,
            courseVariant: hasMultipleVariants ? variant?.name : '',
            raceSeriesId: seriesDetail.raceSeriesId,
            raceVariantId: selectedRace.raceVariantId,
            knownVariants: hasMultipleVariants ? variants.map((v) => v.name) : [],
          },
        });
        return;
      }

      // Multi-variant upload: nothing to create yet - Data Review matches each section of the file
      // to one of the ticked variants, and races are created on save only for variants with results.
      if (isMultiVariantMode) {
        const included = variants.filter((v) => data.selectedVariantIds.includes(v.raceVariantId));
        onNext({
          raceSelection: {
            ...data,
            includedVariantIds: included.map((v) => v.raceVariantId),
            raceId: null,
            raceName: seriesDetail.name,
            raceDate: data.raceDate,
            isGrandPrixRace: false,
            courseVariant: '',
            raceSeriesId: seriesDetail.raceSeriesId,
            raceVariantId: null,
            knownVariants: included.map((v) => v.name),
          },
        });
        return;
      }

      let series = seriesDetail;
      let variant = null;

      if (isNewSeriesMode) {
        const created = await raceService.createSeries({
          name: data.newSeriesName.trim(),
          isGrandPrix: data.isGrandPrixRace,
        });
        series = await raceService.getSeriesDetail(created.raceSeriesId);
        variant = series.variants[0];
      } else if (isNewVariantMode) {
        variant = await raceService.createVariant(series.raceSeriesId, {
          name: data.newVariantName.trim(),
          isGrandPrixByDefault: data.isGrandPrixRace,
        });
        series = { ...series, variants: [...series.variants, variant] };
      } else {
        variant = variantById(singleVariantId);
      }

      const race = await raceService.createRace({
        raceVariantId: variant.raceVariantId,
        date: data.raceDate,
        // A ticked variant carries its own Grand Prix checkbox.
        isGrandPrixRace: isChecklistMode
          ? !!data.variantGrandPrix?.[variant.raceVariantId]
          : data.isGrandPrixRace,
      });

      const multi = series.variants.length > 1;
      onNext({
        raceSelection: {
          ...data,
          includedVariantIds: null,
          raceId: race.raceId,
          raceName: series.name,
          raceDate: race.date,
          isGrandPrixRace: race.isGrandPrixRace,
          courseVariant: multi ? variant.name : '',
          raceSeriesId: series.raceSeriesId,
          raceVariantId: variant.raceVariantId,
          knownVariants: multi ? series.variants.map((v) => v.name) : [],
        },
      });
    } catch (err) {
      setError(err.message);
      console.error('Error creating race:', err);
    } finally {
      setSubmitting(false);
    }
  };

  const header = (
    <div className="mb-6 flex items-center justify-between border-b border-border pb-4">
      <h2 className="text-xl font-semibold">Step 1: Race Selection</h2>
      <span className="text-sm text-muted-foreground">Step 1 of 4</span>
    </div>
  );

  if (loading) {
    return (
      <div>
        {header}
        <div className="flex flex-col gap-4">
          <Skeleton className="h-4 w-32" />
          <Skeleton className="h-8 w-full" />
          <Skeleton className="h-4 w-24" />
          <Skeleton className="h-8 w-full" />
        </div>
      </div>
    );
  }

  const grandPrixCheckbox = (label) => (
    <label className="flex items-center gap-2 text-sm font-medium">
      <input
        type="checkbox"
        className="size-4 rounded border-input accent-primary"
        {...register('isGrandPrixRace')}
      />
      {label}
    </label>
  );

  const dateField = (
    <div className="flex flex-col gap-2">
      <Label htmlFor="raceDate">Race Date</Label>
      <Input
        type="date"
        id="raceDate"
        aria-invalid={!!errors.raceDate}
        {...register('raceDate', { required: 'Race date is required' })}
      />
      {errors.raceDate && <p className="text-sm text-destructive">{errors.raceDate.message}</p>}
    </div>
  );

  return (
    <div>
      {header}

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
            rules={{ required: 'Please select a race series or create a new one' }}
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
                  <SelectItem value={NEW_SERIES}>+ New Series</SelectItem>
                </SelectContent>
              </Select>
            )}
          />
          {errors.seriesSelection && (
            <p className="text-sm text-destructive">{errors.seriesSelection.message}</p>
          )}
        </div>

        {isNewSeriesMode && (
          <>
            <div className="flex flex-col gap-2">
              <Label htmlFor="newSeriesName">Series Name</Label>
              <Input
                type="text"
                id="newSeriesName"
                aria-invalid={!!errors.newSeriesName}
                {...register('newSeriesName', {
                  validate: (v) => !isNewSeriesMode || !!v?.trim() || 'Series name is required',
                })}
                placeholder="e.g., Mount Marathon Race"
              />
              {errors.newSeriesName && (
                <p className="text-sm text-destructive">{errors.newSeriesName.message}</p>
              )}
              <p className="text-xs text-muted-foreground">
                Created with a single course. Add more course variants (e.g. a junior race) from the
                series later, or with &ldquo;+ New variant&rdquo; next time.
              </p>
            </div>
            {dateField}
            {grandPrixCheckbox('This is a Grand Prix race')}
          </>
        )}

        {isExistingSeriesMode && (
          seriesDetailLoading ? (
            <Skeleton className="h-8 w-full" />
          ) : (
            <div className="flex flex-col gap-2">
              <Label htmlFor="raceInstanceId">Race</Label>
              <Controller
                name="raceInstanceId"
                control={control}
                rules={{ required: 'Please select a race or "+ New race"' }}
                render={({ field }) => (
                  <Select
                    value={field.value || undefined}
                    onValueChange={(value) => {
                      field.onChange(value);
                      handleRaceInstanceChange(value);
                    }}
                  >
                    <SelectTrigger id="raceInstanceId" className="w-full" aria-invalid={!!errors.raceInstanceId}>
                      <SelectValue placeholder="-- Select a race --">
                        {(value) => raceInstanceOptionLabel(value) || '-- Select a race --'}
                      </SelectValue>
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value={NEW_RACE}>+ New race</SelectItem>
                      {seriesDetail?.races.map((r) => (
                        <SelectItem key={r.raceId} value={r.raceId}>{raceInstanceLabel(r)}</SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                )}
              />
              {errors.raceInstanceId && (
                <p className="text-sm text-destructive">{errors.raceInstanceId.message}</p>
              )}
              <p className="text-xs text-muted-foreground">
                Pick an existing race to add more results to it (e.g. one that&rsquo;s missing
                finishers), or &ldquo;+ New race&rdquo; to record a new year.
                {hasMultipleVariants &&
                  ' If the file covers several variants, choose “+ New race” and tick each one.'}
              </p>
            </div>
          )
        )}

        {isExistingRaceMode && selectedRace && (
          <div className="rounded-lg border border-border bg-muted/30 p-4 text-sm">
            <dl className="grid grid-cols-2 gap-2">
              <dt className="text-muted-foreground">Race</dt>
              <dd className="font-medium">{seriesDetail.name}</dd>
              {hasMultipleVariants && (
                <>
                  <dt className="text-muted-foreground">Variant</dt>
                  <dd className="font-medium">{variantById(selectedRace.raceVariantId)?.name}</dd>
                </>
              )}
              <dt className="text-muted-foreground">Grand Prix Race</dt>
              <dd className="font-medium">{selectedRace.isGrandPrixRace ? 'Yes' : 'No'}</dd>
            </dl>
            <p className="mt-2 text-xs text-muted-foreground">
              To change these, edit the race from Results Management.
            </p>
          </div>
        )}

        {isNewRaceMode && (
          <>
            {isChecklistMode ? (
              <div className="flex flex-col gap-2">
                <Label>Variants in this results file</Label>
                <Controller
                  name="selectedVariantIds"
                  control={control}
                  rules={{
                    validate: (v) => !isChecklistMode || v?.length > 0 || 'Tick at least one variant',
                  }}
                  render={() => (
                    <div className="flex flex-col divide-y divide-border rounded-lg border border-border">
                      {variants.map((v) => {
                        const checked = selectedVariantIds.includes(v.raceVariantId);
                        const existing = existingRaceFor(v.raceVariantId);
                        return (
                          <div
                            key={v.raceVariantId}
                            className="flex flex-wrap items-center justify-between gap-2 px-3 py-2"
                          >
                            <label className="flex items-center gap-2 text-sm font-medium">
                              <input
                                type="checkbox"
                                className="size-4 rounded border-input accent-primary"
                                checked={checked}
                                onChange={(e) => toggleVariant(v.raceVariantId, e.target.checked)}
                              />
                              {v.name}
                            </label>
                            {checked && isMultiVariantMode && existing && (
                              <span className="text-xs text-muted-foreground">
                                {existing.year} race exists ({pluralResults(existing.resultsCount)})
                                &mdash; results are added to it
                              </span>
                            )}
                            {checked && !existing && (
                              <label className="flex items-center gap-1.5 text-xs text-muted-foreground">
                                <input
                                  type="checkbox"
                                  className="size-4 rounded border-input accent-primary"
                                  {...register(`variantGrandPrix.${v.raceVariantId}`)}
                                />
                                Counts toward the Grand Prix this year
                              </label>
                            )}
                          </div>
                        );
                      })}
                    </div>
                  )}
                />
                {errors.selectedVariantIds && (
                  <p className="text-sm text-destructive">{errors.selectedVariantIds.message}</p>
                )}
                <p className="text-xs text-muted-foreground">
                  Tick every course this results file contains. With more than one, the AI extractor
                  only looks for those, each section is matched to its variant in Data Review, and a
                  race is recorded on save only for variants that had results.
                </p>
                <button
                  type="button"
                  className="self-start text-xs text-muted-foreground underline underline-offset-2 hover:text-foreground"
                  onClick={() => handleVariantChange(NEW_VARIANT)}
                >
                  A course that isn&rsquo;t listed? Add a variant
                </button>
              </div>
            ) : isNewVariantMode ? (
              <div className="flex flex-col gap-2">
                <Label htmlFor="newVariantName">New Course Variant</Label>
                <Input
                  id="newVariantName"
                  type="text"
                  placeholder="Variant name, e.g. Junior Race"
                  aria-invalid={!!errors.newVariantName}
                  {...register('newVariantName', {
                    validate: (v) => !isNewVariantMode || !!v?.trim() || 'Variant name is required',
                  })}
                />
                {errors.newVariantName && (
                  <p className="text-sm text-destructive">{errors.newVariantName.message}</p>
                )}
                {variants.length > 0 && (
                  <button
                    type="button"
                    className="self-start text-xs text-muted-foreground underline underline-offset-2 hover:text-foreground"
                    onClick={() => handleVariantChange(hasMultipleVariants ? '' : variants[0].raceVariantId)}
                  >
                    &larr; Choose an existing variant instead
                  </button>
                )}
              </div>
            ) : (
              <button
                type="button"
                className="self-start text-xs text-muted-foreground underline underline-offset-2 hover:text-foreground"
                onClick={() => handleVariantChange(NEW_VARIANT)}
              >
                This is a different course (add a variant)
              </button>
            )}

            {dateField}

            {!isChecklistMode &&
              grandPrixCheckbox(
                isNewVariantMode ? 'Counts toward the Grand Prix' : 'Counts toward the Grand Prix this year'
              )}

            {duplicateRace && (
              <Alert variant="destructive">
                <AlertCircle />
                <AlertDescription>
                  There&rsquo;s already a {duplicateRace.year}
                  {hasMultipleVariants ? ` ${variantById(duplicateRace.raceVariantId)?.name}` : ''} race
                  ({duplicateRace.date}). Select it above to add results to it.
                </AlertDescription>
              </Alert>
            )}
          </>
        )}

        <div className="mt-4 flex flex-col-reverse gap-3 border-t border-border pt-6 sm:flex-row sm:justify-end">
          <Button type="button" variant="outline" onClick={onCancel}>
            Cancel
          </Button>
          <Button type="submit" disabled={submitting || !!duplicateRace}>
            {submitting ? <Loader2 className="animate-spin" /> : null}
            Next &rarr;
          </Button>
        </div>
      </form>
    </div>
  );
}
