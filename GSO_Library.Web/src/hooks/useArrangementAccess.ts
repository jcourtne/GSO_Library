import { useQuery } from '@tanstack/react-query';
import { authApi } from '../api/auth';
import { useAuth } from './useAuth';
import type { Arrangement } from '../types';

/**
 * Client-side mirrors of the arrangement authorization the API enforces, for greying out
 * actions the server would reject anyway.
 *
 * - `canDownloadNonPlayback` — may the user download every file of an arrangement (scores,
 *   notation), not just the playback files everyone can get. Mirrors `ArrangementsController.DownloadFile`.
 * - `canAddToSeason` — the API only links an arrangement to a season whose ensemble the
 *   arrangement belongs to (`SeasonsController.AddArrangement`).
 */
export function useArrangementAccess() {
  const { canDownloadAll, isEnsembleLibrarian, isEnsembleDownloader } = useAuth();
  const ensembleScoped = isEnsembleLibrarian() || isEnsembleDownloader();

  const { data: myEnsembles = [] } = useQuery({
    queryKey: ['my-ensembles'],
    queryFn: () => authApi.getMyEnsembles(),
    enabled: ensembleScoped,
  });

  const inMyEnsembles = (a: Arrangement) =>
    (a.ensembles ?? []).some((e) => myEnsembles.some((me) => me.id === e.id));

  const canDownloadNonPlayback = (a: Arrangement) =>
    canDownloadAll() || (ensembleScoped && inMyEnsembles(a));

  const canAddToSeason = (a: Arrangement, seasonEnsembleId: number) =>
    (a.ensembles ?? []).some((e) => e.id === seasonEnsembleId);

  return { canDownloadNonPlayback, canAddToSeason };
}
