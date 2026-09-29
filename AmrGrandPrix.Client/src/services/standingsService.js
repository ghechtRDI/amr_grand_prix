/**
 * Standings Service
 * Grand Prix season finalization: once a year's Grand Prix is over it's finalized, locking its
 * standings; it can be un-finalized to correct results and then finalized again.
 */

import { authHeaders, request } from './api';

/** { year, isFinalized, finalizedAt, grandPrixRaceCount, grandPrixRacesWithoutResults } */
export const getSeason = (year) => request(`/api/standings/${year}/season`);

export const finalizeSeason = (year) =>
  request(`/api/standings/${year}/finalize`, { method: 'POST', headers: authHeaders() });

export const unfinalizeSeason = (year) =>
  request(`/api/standings/${year}/unfinalize`, { method: 'POST', headers: authHeaders() });

export const recalculateStandings = (year) =>
  request(`/api/standings/${year}/recalculate`, { method: 'POST', headers: authHeaders() });
