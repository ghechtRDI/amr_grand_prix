import { useCallback, useState } from 'react';
import { useAuth } from './useAuth';
import * as runnerService from '../services/runnerService';
import EditRunnerDialog from '@/components/runner-management/EditRunnerDialog';

export const RACER_EDITOR_ROLES = ['Admin', 'Manager'];

/**
 * "Edit Racer" from public pages (the athlete directory, a runner's page). Public payloads leave
 * out DOB, email and alternate names, so the full admin record is fetched when the editor opens.
 * One dialog is shared by the whole page instead of one per table row.
 *
 * Returns `canEdit` (Admin/Manager), `openEditor(runnerId)`, `loadingId` (the runner being
 * fetched, for a busy button), `error`, and the `dialog` element to render once.
 */
export function useEditRacer({ onSaved } = {}) {
  const { hasAnyRole } = useAuth();
  const [runner, setRunner] = useState(null);
  const [loadingId, setLoadingId] = useState(null);
  const [error, setError] = useState(null);

  const openEditor = useCallback(async (runnerId) => {
    setError(null);
    setLoadingId(runnerId);
    try {
      setRunner(await runnerService.getRunnerForManagement(runnerId));
    } catch (err) {
      setError(err.message);
    } finally {
      setLoadingId(null);
    }
  }, []);

  const dialog = (
    <EditRunnerDialog
      runner={runner}
      open={!!runner}
      onOpenChange={(open) => !open && setRunner(null)}
      onSaved={onSaved}
    />
  );

  return { canEdit: hasAnyRole(RACER_EDITOR_ROLES), openEditor, loadingId, error, clearError: () => setError(null), dialog };
}
