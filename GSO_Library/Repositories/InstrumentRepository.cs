using Dapper;
using GSO_Library.Data;
using GSO_Library.Models;
using Microsoft.Extensions.Caching.Memory;

namespace GSO_Library.Repositories;

public class InstrumentRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IMemoryCache _cache;

    public InstrumentRepository(IDbConnectionFactory connectionFactory, IMemoryCache cache)
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

    public async Task<IEnumerable<Instrument>> GetAllInstrumentsAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QueryAsync<Instrument>(
            "SELECT i.id, i.name, i.family_id, i.created_at, i.updated_at, i.created_by, f.name as family_name " +
            "FROM instruments i LEFT JOIN instrument_families f ON i.family_id = f.id");
    }

    public async Task<PaginatedResult<Instrument>> GetAllInstrumentsAsync(int page, int pageSize, string? sortBy = null, string? sortDirection = null, string? search = null)
    {
        using var connection = _connectionFactory.CreateConnection();
        var whereClause = string.IsNullOrWhiteSpace(search) ? "" : " WHERE LOWER(i.name) LIKE @Search";
        var searchParam = string.IsNullOrWhiteSpace(search) ? null : $"%{search.ToLower()}%";
        var totalCount = await connection.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM instruments i{whereClause}", new { Search = searchParam });
        var orderColumn = _sortColumns.GetValueOrDefault(sortBy ?? "", "id");
        var orderDir = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC";
        var items = await connection.QueryAsync<Instrument>(
            $"SELECT i.id, i.name, i.family_id, i.created_at, i.updated_at, i.created_by, f.name as family_name " +
            $"FROM instruments i LEFT JOIN instrument_families f ON i.family_id = f.id{whereClause} ORDER BY i.{orderColumn} {orderDir} LIMIT @Limit OFFSET @Offset",
            new { Limit = pageSize, Offset = (page - 1) * pageSize, Search = searchParam });
        return new PaginatedResult<Instrument> { Items = items.ToList(), Page = page, PageSize = pageSize, TotalCount = totalCount };
    }

    public async Task<Instrument?> GetInstrumentByIdAsync(int id)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<Instrument>(
            "SELECT i.id, i.name, i.family_id, i.created_at, i.updated_at, i.created_by, f.name as family_name " +
            "FROM instruments i LEFT JOIN instrument_families f ON i.family_id = f.id WHERE i.id = @Id",
            new { Id = id });
    }

    public async Task<(Instrument? Instrument, bool DuplicateName)> AddInstrumentAsync(Instrument instrument)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (await NameExistsAsync(connection, instrument.Name))
            return (null, true);
        int id;
        try
        {
            id = await connection.InsertReturningIdAsync(
                "INSERT INTO instruments (name, family_id, created_at, updated_at, created_by) VALUES (@Name, @FamilyId, @CreatedAt, @UpdatedAt, @CreatedBy)",
                new { instrument.Name, instrument.FamilyId, instrument.CreatedAt, instrument.UpdatedAt, instrument.CreatedBy });
        }
        catch (Exception ex) when (ex.IsUniqueConstraintViolation())
        {
            return (null, true);
        }
        instrument.Id = id;
        InvalidateArrangementCache();
        return (instrument, false);
    }

    public async Task<(Instrument? Instrument, bool DuplicateName)> UpdateInstrumentAsync(int id, Instrument instrument)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (await NameExistsAsync(connection, instrument.Name, excludeId: id))
            return (null, true);
        int rows;
        try
        {
            rows = await connection.ExecuteAsync(
                "UPDATE instruments SET name = @Name, family_id = @FamilyId, updated_at = @UpdatedAt WHERE id = @Id",
                new { instrument.Name, instrument.FamilyId, instrument.UpdatedAt, Id = id });
        }
        catch (Exception ex) when (ex.IsUniqueConstraintViolation())
        {
            return (null, true);
        }
        if (rows == 0) return (null, false);
        instrument.Id = id;
        InvalidateArrangementCache();
        return (instrument, false);
    }

    private static async Task<bool> NameExistsAsync(System.Data.IDbConnection connection, string name, int? excludeId = null)
    {
        var sql = "SELECT COUNT(*) FROM instruments WHERE LOWER(name) = LOWER(@Name)";
        if (excludeId.HasValue) sql += " AND id != @ExcludeId";
        var count = await connection.ExecuteScalarAsync<int>(sql, new { Name = name, ExcludeId = excludeId });
        return count > 0;
    }

    public async Task<bool> DeleteInstrumentAsync(int id)
    {
        using var connection = _connectionFactory.CreateConnection();
        var rows = await connection.ExecuteAsync("DELETE FROM instruments WHERE id = @Id", new { Id = id });
        if (rows == 0) return false;
        InvalidateArrangementCache();
        return true;
    }

    private void InvalidateArrangementCache()
    {
        _cache.Remove(ArrangementRepository.ArrangementsCacheKey);
    }
}
