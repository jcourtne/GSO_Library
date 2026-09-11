import { useQuery } from '@tanstack/react-query';
import { authApi } from '../api/auth';
import { useAuth } from './useAuth';
import type { Arrangement } from '../types';

/**
 * Client-side mirrors of the arrangement authorization the API enforces, for greying out
 * actions the server would reject anyway.
 *
 * - `canDownloadNonPlayback` — may the user download every file of an arrangement (scores,
 *   notation), not just the playback files everyone can get. Mirrors `ArrangementsController.DownloadFile`:
 *   full-download roles, an ensemble member of the arrangement's ensemble (or any ensemble-scoped
 *   user for a public arrangement), or a Submitter who created the arrangement.
 * - `canAddToSeason` — the API only links an arrangement to a season whose ensemble the
 *   arrangement belongs to (`SeasonsController.AddArrangement`).
 * - `canEditArrangement` / `canDeleteArrangement` / `canLinkEnsemble` — mirror
 *   `ArrangementsController.EnforceWriteAccessAsync` plus its `DeleteArrangement`/`AddEnsemble`
 *   carve-outs: an Ensemble Librarian can edit any public arrangement, but deleting one or
 *   linking an ensemble to it still requires ownership (or shared-ensemble membership for a
 *   non-public arrangement, as before).
 */
export function useArrangementAccess() {
  const { canEdit, canDownloadAll, isSubmitter, isEnsembleLibrarian, isEnsembleDownloader, username } = useAuth();
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

  const isOwner = (a: Arrangement) =>
    !!username && !!a.createdBy && a.createdBy.toLowerCase() === username.toLowerCase();

  const canDownloadNonPlayback = (a: Arrangement) =>
    canDownloadAll()
    || (isSubmitter() && isOwner(a))
    || (ensembleScoped && (isPublic(a) || inMyEnsembles(a)));

  const canAddToSeason = (a: Arrangement, seasonEnsembleId: number) =>
    isPublic(a) || (a.ensembles ?? []).some((e) => e.id === seasonEnsembleId);

  const canEditArrangement = (a: Arrangement) =>
    canEdit()
    || (isSubmitter() && isOwner(a))
    || (isEnsembleLibrarian() && (isOwner(a) || inMyEnsembles(a) || isPublic(a)));

  const canDeleteArrangement = (a: Arrangement) =>
    canEdit()
    || (isSubmitter() && isOwner(a))
    || (isEnsembleLibrarian() && (isOwner(a) || inMyEnsembles(a)));

  const canLinkEnsemble = (a: Arrangement) =>
    canEdit()
    || (isSubmitter() && isOwner(a))
    || (isEnsembleLibrarian() && (isOwner(a) || inMyEnsembles(a)));

  return { canDownloadNonPlayback, canAddToSeason, canEditArrangement, canDeleteArrangement, canLinkEnsemble };
}
