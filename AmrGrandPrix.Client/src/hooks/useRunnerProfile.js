import { useEffect, useState } from 'react';

/**
 * Loads the public runner page payload (GET /api/runners/:id/profile) shared by the runner
 * landing page and the per-series page: basic info, series summaries, and every result with
 * computed places and PR flags.
 */
export function useRunnerProfile(runnerId) {
  const [profile, setProfile] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);

    fetch(`/api/runners/${runnerId}/profile`)
      .then((r) => {
        if (r.status === 404) throw new Error('Runner not found');
        if (!r.ok) throw new Error(`Failed to load runner (HTTP ${r.status})`);
        return r.json();
      })
      .then((data) => !cancelled && setProfile(data))
      .catch((e) => !cancelled && setError(e.message))
      .finally(() => !cancelled && setLoading(false));

    return () => {
      cancelled = true;
    };
  }, [runnerId]);

  return { profile, loading, error };
}
