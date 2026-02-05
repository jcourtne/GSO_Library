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

    public async Task<IEnumerable<Instrument>> GetAllInstrumentsAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QueryAsync<Instrument>("SELECT id, name FROM instruments");
    }

    public async Task<PaginatedResult<Instrument>> GetAllInstrumentsAsync(int page, int pageSize)
    {
        using var connection = _connectionFactory.CreateConnection();
        var totalCount = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM instruments");
        var items = await connection.QueryAsync<Instrument>(
            "SELECT id, name FROM instruments ORDER BY id LIMIT @Limit OFFSET @Offset",
            new { Limit = pageSize, Offset = (page - 1) * pageSize });
        return new PaginatedResult<Instrument> { Items = items.ToList(), Page = page, PageSize = pageSize, TotalCount = totalCount };
    }

    public async Task<Instrument?> GetInstrumentByIdAsync(int id)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<Instrument>(
            "SELECT id, name FROM instruments WHERE id = @Id", new { Id = id });
    }

    public async Task<Instrument> AddInstrumentAsync(Instrument instrument)
    {
        using var connection = _connectionFactory.CreateConnection();
        var id = await connection.InsertReturningIdAsync(
            "INSERT INTO instruments (name) VALUES (@Name)", new { instrument.Name });
        instrument.Id = id;
        InvalidateArrangementCache();
        return instrument;
    }

    public async Task<Instrument?> UpdateInstrumentAsync(int id, Instrument instrument)
    {
        using var connection = _connectionFactory.CreateConnection();
        var rows = await connection.ExecuteAsync(
            "UPDATE instruments SET name = @Name WHERE id = @Id",
            new { instrument.Name, Id = id });
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
