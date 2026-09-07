import { useQuery } from '@tanstack/react-query';
import { authApi } from '../api/auth';
import { useAuth } from './useAuth';

/**
 * Current user's ensemble memberships plus a helper for ensemble-scoped write access.
 * Admins/Librarians can edit anything; an Ensemble Librarian can only edit resources
 * tied to one of their own ensembles (the API enforces the same rule).
 */
export function useMyEnsembles() {
  const { canEdit, isEnsembleLibrarian } = useAuth();
  const ensembleLibrarian = isEnsembleLibrarian();

  const { data: myEnsembles = [] } = useQuery({
    queryKey: ['my-ensembles'],
    queryFn: () => authApi.getMyEnsembles(),
    enabled: ensembleLibrarian,
  });

  const canEditForEnsemble = (ensembleId?: number | null) =>
    canEdit() ||
    (ensembleLibrarian && ensembleId != null && myEnsembles.some((e) => e.id === ensembleId));

  // May the user create a new ensemble-scoped resource (for at least one ensemble)?
  const canCreateForEnsemble = canEdit() || ensembleLibrarian;

  // true when the user can edit resources for ANY ensemble (Admin/Librarian), so ensemble
  // pickers etc. don't need to be restricted.
  const canEditAllEnsembles = canEdit();

  return {
    myEnsembles,
    canEditForEnsemble,
    canCreateForEnsemble,
    canEditAllEnsembles,
    isEnsembleLibrarian: ensembleLibrarian,
  };
}
