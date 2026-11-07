import * as tokenService from '../../services/tokenService';

/**
 * Fetches a pending upload batch (GET /api/results/batch/{id}/resume) and shapes it into the
 * results upload wizard's data, landing on the Data Review step - including the latest saved
 * review draft, which DataReviewStep restores.
 */
export async function fetchResumeWizardData(uploadBatchId) {
  const res = await fetch(`/api/results/batch/${uploadBatchId}/resume`, {
    headers: { Authorization: `Bearer ${tokenService.getAccessToken()}` },
  });
  if (!res.ok) {
    const txt = await res.text();
    throw new Error(txt || `HTTP ${res.status}`);
  }
  const data = await res.json();
  return {
    raceSelection: {
      raceId: data.raceId,
      raceName: data.raceName,
      raceDate: data.raceDate,
      isGrandPrixRace: data.isGrandPrixRace,
      courseVariant: data.courseVariant || '',
      raceSeriesId: data.raceSeriesId,
      raceVariantId: data.raceVariantId,
      includedVariantIds: data.includedVariantIds,
      knownVariants: data.includedVariantNames,
    },
    uploadBatchId: data.uploadBatchId,
    parsedResults: data.parsedResults,
    totalRows: data.totalRows,
    validRows: data.validRows,
    rowsWithIssues: data.rowsWithIssues,
    // Saved Data Review draft (edited rows + race picks), restored by DataReviewStep.
    reviewDraft: data.reviewDraft || null,
    reviewDraftSavedAt: data.reviewDraftSavedAt || null,
  };
}
