using Dapper;
using GSO_Library.Data;
using GSO_Library.Models;
using Microsoft.Extensions.Caching.Memory;

namespace GSO_Library.Repositories;

public class EnsembleRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IMemoryCache _cache;

    public EnsembleRepository(IDbConnectionFactory connectionFactory, IMemoryCache cache)
    {
        _connectionFactory = connectionFactory;
        _cache = cache;
    }

    private static readonly Dictionary<string, string> _sortColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        ["id"] = "id",
        ["name"] = "name",
        ["createdat"] = "created_at"
    };

    public async Task<IEnumerable<Ensemble>> GetAllEnsemblesAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QueryAsync<Ensemble>(
            "SELECT id, name, description, website, contact_info, created_at, updated_at, created_by FROM ensembles");
    }

    public async Task<PaginatedResult<Ensemble>> GetAllEnsemblesAsync(int page, int pageSize, string? sortBy = null, string? sortDirection = null)
    {
        using var connection = _connectionFactory.CreateConnection();
        var totalCount = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM ensembles");
        var orderColumn = _sortColumns.GetValueOrDefault(sortBy ?? "", "id");
        var orderDir = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC";
        var items = (await connection.QueryAsync<Ensemble>(
            $"SELECT id, name, description, website, contact_info, created_at, updated_at, created_by FROM ensembles ORDER BY {orderColumn} {orderDir} LIMIT @Limit OFFSET @Offset",
            new { Limit = pageSize, Offset = (page - 1) * pageSize })).ToList();

        if (items.Count > 0)
        {
            var ensembleIds = items.Select(e => e.Id).ToArray();
            var performances = await connection.QueryInListAsync<Performance>(
                "SELECT id, name, link, performance_date, notes, ensemble_id, created_at, updated_at, created_by FROM performances WHERE ensemble_id = ANY(@Ids)",
                new { Ids = ensembleIds });
            var perfLookup = performances.GroupBy(p => p.EnsembleId!.Value).ToDictionary(g => g.Key, g => g.ToList());
            foreach (var e in items)
                e.Performances = perfLookup.GetValueOrDefault(e.Id, []);
        }

        return new PaginatedResult<Ensemble> { Items = items, Page = page, PageSize = pageSize, TotalCount = totalCount };
    }

    public async Task<Ensemble?> GetEnsembleByIdAsync(int id)
    {
        using var connection = _connectionFactory.CreateConnection();
        var ensemble = await connection.QuerySingleOrDefaultAsync<Ensemble>(
            "SELECT id, name, description, website, contact_info, created_at, updated_at, created_by FROM ensembles WHERE id = @Id", new { Id = id });
        if (ensemble == null) return null;

        var performances = await connection.QueryAsync<Performance>(
            "SELECT id, name, link, performance_date, notes, ensemble_id, created_at, updated_at, created_by FROM performances WHERE ensemble_id = @Id", new { Id = id });
        ensemble.Performances = performances.ToList();

        return ensemble;
    }

    public async Task<Ensemble> AddEnsembleAsync(Ensemble ensemble)
    {
        using var connection = _connectionFactory.CreateConnection();
        var id = await connection.InsertReturningIdAsync(
            "INSERT INTO ensembles (name, description, website, contact_info, created_at, updated_at, created_by) VALUES (@Name, @Description, @Website, @ContactInfo, @CreatedAt, @UpdatedAt, @CreatedBy)",
            new { ensemble.Name, ensemble.Description, ensemble.Website, ensemble.ContactInfo, ensemble.CreatedAt, ensemble.UpdatedAt, ensemble.CreatedBy });
        ensemble.Id = id;
        InvalidateArrangementCache();
        return ensemble;
    }

    public async Task<Ensemble?> UpdateEnsembleAsync(int id, Ensemble ensemble)
    {
        using var connection = _connectionFactory.CreateConnection();
        var rows = await connection.ExecuteAsync(
            "UPDATE ensembles SET name = @Name, description = @Description, website = @Website, contact_info = @ContactInfo, updated_at = @UpdatedAt WHERE id = @Id",
            new { ensemble.Name, ensemble.Description, ensemble.Website, ensemble.ContactInfo, ensemble.UpdatedAt, Id = id });
        if (rows == 0) return null;
        ensemble.Id = id;
        InvalidateArrangementCache();
        return ensemble;
    }

    public async Task<bool> DeleteEnsembleAsync(int id)
    {
        using var connection = _connectionFactory.CreateConnection();
        var rows = await connection.ExecuteAsync("DELETE FROM ensembles WHERE id = @Id", new { Id = id });
        if (rows == 0) return false;
        InvalidateArrangementCache();
        return true;
    }

    private void InvalidateArrangementCache()
    {
        _cache.Remove(ArrangementRepository.ArrangementsCacheKey);
    }
}
