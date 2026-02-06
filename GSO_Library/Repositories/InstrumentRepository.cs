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
        return await connection.QueryAsync<Instrument>("SELECT id, name, created_at, updated_at, created_by FROM instruments");
    }

    public async Task<PaginatedResult<Instrument>> GetAllInstrumentsAsync(int page, int pageSize, string? sortBy = null, string? sortDirection = null, string? search = null)
    {
        using var connection = _connectionFactory.CreateConnection();
        var whereClause = string.IsNullOrWhiteSpace(search) ? "" : " WHERE name LIKE @Search";
        var searchParam = string.IsNullOrWhiteSpace(search) ? null : $"%{search}%";
        var totalCount = await connection.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM instruments{whereClause}", new { Search = searchParam });
        var orderColumn = _sortColumns.GetValueOrDefault(sortBy ?? "", "id");
        var orderDir = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC";
        var items = await connection.QueryAsync<Instrument>(
            $"SELECT id, name, created_at, updated_at, created_by FROM instruments{whereClause} ORDER BY {orderColumn} {orderDir} LIMIT @Limit OFFSET @Offset",
            new { Limit = pageSize, Offset = (page - 1) * pageSize, Search = searchParam });
        return new PaginatedResult<Instrument> { Items = items.ToList(), Page = page, PageSize = pageSize, TotalCount = totalCount };
    }

    public async Task<Instrument?> GetInstrumentByIdAsync(int id)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<Instrument>(
            "SELECT id, name, created_at, updated_at, created_by FROM instruments WHERE id = @Id", new { Id = id });
    }

    public async Task<Instrument> AddInstrumentAsync(Instrument instrument)
    {
        using var connection = _connectionFactory.CreateConnection();
        var id = await connection.InsertReturningIdAsync(
            "INSERT INTO instruments (name, created_at, updated_at, created_by) VALUES (@Name, @CreatedAt, @UpdatedAt, @CreatedBy)",
            new { instrument.Name, instrument.CreatedAt, instrument.UpdatedAt, instrument.CreatedBy });
        instrument.Id = id;
        InvalidateArrangementCache();
        return instrument;
    }

    public async Task<Instrument?> UpdateInstrumentAsync(int id, Instrument instrument)
    {
        using var connection = _connectionFactory.CreateConnection();
        var rows = await connection.ExecuteAsync(
            "UPDATE instruments SET name = @Name, updated_at = @UpdatedAt WHERE id = @Id",
            new { instrument.Name, instrument.UpdatedAt, Id = id });
        if (rows == 0) return null;
        instrument.Id = id;
        InvalidateArrangementCache();
        return instrument;
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
