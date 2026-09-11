using System.Security.Claims;
using GSO_Library.Models;

namespace GSO_Library.Services;

public interface IEnsembleAccessService
{
    Task<IReadOnlyList<Ensemble>> GetEnsemblesForUserAsync(ClaimsPrincipal user);
    Task<bool> IsMemberAsync(ClaimsPrincipal user, int ensembleId);
    Task<bool> IsMemberOfAnyAsync(ClaimsPrincipal user, IEnumerable<int> ensembleIds);
    Task<bool> CanWriteForEnsembleAsync(ClaimsPrincipal user, int? ensembleId);
}
