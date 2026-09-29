/**
 * Race Service
 * Race series, their course variants, and yearly race instances.
 */

import { authHeaders, request } from './api';

export const getSeriesList = () => request('/api/race-series');

/** { raceSeriesId, name, variants: [...], races: [{ raceId, year, date, raceVariantId, ... }] } */
export const getSeriesDetail = (seriesId) => request(`/api/race-series/${seriesId}`);

/** Creates a series; with no `variants`, a single "Standard" variant (GP if `isGrandPrix`). */
export const createSeries = ({ name, isGrandPrix = false, variants = [] }) =>
  request('/api/race-series', {
    method: 'POST',
    headers: authHeaders(),
    body: JSON.stringify({ name, isGrandPrix, variants }),
  });

export const createVariant = (seriesId, { name, isGrandPrixByDefault = false }) =>
  request(`/api/race-series/${seriesId}/variants`, {
    method: 'POST',
    headers: authHeaders(),
    body: JSON.stringify({ name, isGrandPrixByDefault }),
  });

/** `isGrandPrixRace` defaults to the variant's default when omitted. */
export const createRace = ({ raceVariantId, date, isGrandPrixRace }) =>
  request('/api/races', {
    method: 'POST',
    headers: authHeaders(),
    body: JSON.stringify({ raceVariantId, date, isGrandPrixRace: isGrandPrixRace ?? null }),
  });

export const updateRace = (raceId, { raceVariantId, date, isGrandPrixRace, location }) =>
  request(`/api/races/${raceId}`, {
    method: 'PUT',
    headers: authHeaders(),
    body: JSON.stringify({ raceVariantId, date, isGrandPrixRace, location }),
  });

export const getRace = (raceId) => request(`/api/races/detail/${raceId}`, { headers: authHeaders() });

/** The variant whose name or alias matches `label` (case-insensitive), if any. */
export const findVariant = (variants, label) => {
  const needle = (label || '').trim().toLowerCase();
  if (!needle) return null;
  return (
    variants.find(
      (v) => v.name.toLowerCase() === needle || (v.aliases || []).some((a) => a.toLowerCase() === needle)
    ) || null
  );
};

export const yearOf = (isoDate) => Number((isoDate || '').slice(0, 4)) || new Date().getFullYear();

/** "Mount Marathon Race – Junior", or just the series name for single-course series. */
export const raceLabel = (series, variant) =>
  series.variants.length > 1 && variant ? `${series.name} – ${variant.name}` : series.name;
