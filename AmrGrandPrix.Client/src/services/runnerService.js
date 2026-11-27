/**
 * Runner Service
 * The public athlete directory, plus admin racer management: listing/editing runner records,
 * finding likely duplicates, and merging one runner record into another.
 */

import { authHeaders, request } from './api';

/**
 * Public. AthleteDirectoryEntryDto[]: { runnerId, fullName, gender, currentAge, isAgeEstimated,
 * resultCount, topGrandPrixScore, topGrandPrixYear } — no contact details or birth dates.
 */
export const getAthleteDirectory = () => request('/api/runners/directory');

/** One RunnerAdminDto (Admin/Manager only) — the record EditRunnerDialog edits. */
export const getRunnerForManagement = (runnerId) =>
  request(`/api/runners/${runnerId}/manage`, { headers: authHeaders() });

/** RunnerAdminDto[] — matches legal, preferred, and alternate names. */
export const getRunnersForManagement = (search = '', limit = 100) =>
  request(`/api/runners/manage?${new URLSearchParams({ search, limit })}`, { headers: authHeaders() });

/** PotentialDuplicateDto[]: { runnerA, runnerB, confidence, nameSimilarity, genderMatch, birthYearMatch } */
export const getPotentialDuplicates = (minConfidence = 0.75) =>
  request(`/api/runners/potential-duplicates?minConfidence=${minConfidence}`, { headers: authHeaders() });

/** { firstName, lastName, gender, dateOfBirth, email, preferredName, alternateNames } */
export const updateRunner = (runnerId, data) =>
  request(`/api/runners/${runnerId}`, {
    method: 'PUT',
    headers: authHeaders(),
    body: JSON.stringify(data),
  });

/** What merging `sourceId` into `targetId` would do; changes nothing. */
export const getMergePreview = (targetId, sourceId) =>
  request(`/api/runners/${targetId}/merge-preview/${sourceId}`, { headers: authHeaders() });

/** Folds `sourceId` into `targetId` (which is kept) and recalculates affected standings. */
export const mergeRunners = (targetId, sourceId) =>
  request(`/api/runners/${targetId}/merge`, {
    method: 'POST',
    headers: authHeaders(),
    body: JSON.stringify({ sourceRunnerId: sourceId }),
  });

/** DismissedDuplicateDto[]: { runnerA, runnerB, dismissedAt, dismissedBy } — pairs marked "not a match". */
export const getDismissedDuplicates = () =>
  request('/api/runners/not-duplicates', { headers: authHeaders() });

/** Marks two runners as different people, hiding the pair from the duplicates list. */
export const dismissDuplicate = (runnerAId, runnerBId) =>
  request('/api/runners/not-duplicates', {
    method: 'POST',
    headers: authHeaders(),
    body: JSON.stringify({ runnerAId, runnerBId }),
  });

/** Puts a "not a match" pair back on the duplicates list. */
export const restoreDuplicate = (runnerAId, runnerBId) =>
  request(`/api/runners/not-duplicates?${new URLSearchParams({ runnerAId, runnerBId })}`, {
    method: 'DELETE',
    headers: authHeaders(),
  });
