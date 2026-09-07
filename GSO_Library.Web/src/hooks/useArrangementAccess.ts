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

  // An arrangement with no ensemble is public: anyone with a download/ensemble-download role
  // can get its files, and any ensemble can add it to a season.
  const isPublic = (a: Arrangement) => (a.ensembles ?? []).length === 0;

  const inMyEnsembles = (a: Arrangement) =>
    (a.ensembles ?? []).some((e) => myEnsembles.some((me) => me.id === e.id));

  const canDownloadNonPlayback = (a: Arrangement) =>
    canDownloadAll() || (ensembleScoped && (isPublic(a) || inMyEnsembles(a)));

  const canAddToSeason = (a: Arrangement, seasonEnsembleId: number) =>
    isPublic(a) || (a.ensembles ?? []).some((e) => e.id === seasonEnsembleId);

  return { canDownloadNonPlayback, canAddToSeason };
}
