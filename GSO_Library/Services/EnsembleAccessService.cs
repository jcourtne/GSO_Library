using System.Security.Claims;
using GSO_Library.Models;
using GSO_Library.Repositories;

namespace GSO_Library.Services;

/// <summary>
/// Ensemble-membership and ensemble-scoped write-access checks shared by controllers.
/// Registered scoped, so <see cref="GetEnsemblesForUserAsync"/>'s cache is naturally
/// per-request (mirrors what ArrangementsController used to do with a private field).
/// </summary>
public class EnsembleAccessService : IEnsembleAccessService
{
    private readonly EnsembleRepository _ensembleRepository;
    private IReadOnlyList<Ensemble>? _cache;

    public EnsembleAccessService(EnsembleRepository ensembleRepository)
    {
        _ensembleRepository = ensembleRepository;
    }

    public async Task<IReadOnlyList<Ensemble>> GetEnsemblesForUserAsync(ClaimsPrincipal user)
    {
        if (_cache != null) return _cache;
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return _cache = [];
        return _cache = (await _ensembleRepository.GetEnsemblesForUserAsync(userId)).ToList();
    }

    public async Task<bool> IsMemberAsync(ClaimsPrincipal user, int ensembleId)
    {
        var ensembles = await GetEnsemblesForUserAsync(user);
        return ensembles.Any(e => e.Id == ensembleId);
    }

    public async Task<bool> IsMemberOfAnyAsync(ClaimsPrincipal user, IEnumerable<int> ensembleIds)
    {
        var ensembles = await GetEnsemblesForUserAsync(user);
        return ensembleIds.Any(id => ensembles.Any(e => e.Id == id));
    }

    public async Task<bool> CanWriteForEnsembleAsync(ClaimsPrincipal user, int? ensembleId)
    {
        if (user.IsInRole(Roles.Admin) || user.IsInRole(Roles.Librarian)) return true;
        if (!user.IsInRole(Roles.EnsembleLibrarian) || ensembleId is null) return false;
        return await IsMemberAsync(user, ensembleId.Value);
    }
}
