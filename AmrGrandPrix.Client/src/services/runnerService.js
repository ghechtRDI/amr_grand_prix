/**
 * Runner Service
 * Admin racer management: listing/editing runner records, finding likely duplicates, and merging
 * one runner record into another.
 */

import { authHeaders, request } from './api';

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
