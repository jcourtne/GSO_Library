using Dapper;
using GSO_Library.Data;
using GSO_Library.Models;
using Microsoft.Extensions.Caching.Memory;

namespace GSO_Library.Repositories;

public class PerformanceRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IMemoryCache _cache;

    public PerformanceRepository(IDbConnectionFactory connectionFactory, IMemoryCache cache)
    {
        _connectionFactory = connectionFactory;
        _cache = cache;
    }

    public async Task<IEnumerable<Performance>> GetAllPerformancesAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QueryAsync<Performance>(
            "SELECT id, name, link, performance_date, notes FROM performances");
    }

    public async Task<PaginatedResult<Performance>> GetAllPerformancesAsync(int page, int pageSize)
    {
        using var connection = _connectionFactory.CreateConnection();
        var totalCount = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM performances");
        var items = await connection.QueryAsync<Performance>(
            "SELECT id, name, link, performance_date, notes FROM performances ORDER BY id LIMIT @Limit OFFSET @Offset",
            new { Limit = pageSize, Offset = (page - 1) * pageSize });
        return new PaginatedResult<Performance> { Items = items.ToList(), Page = page, PageSize = pageSize, TotalCount = totalCount };
    }

    public async Task<Performance?> GetPerformanceByIdAsync(int id)
    {
        using var connection = _connectionFactory.CreateConnection();
        var performance = await connection.QuerySingleOrDefaultAsync<Performance>(
            "SELECT id, name, link, performance_date, notes FROM performances WHERE id = @Id", new { Id = id });
        if (performance == null) return null;

        // Load linked arrangements via junction table
        var arrangements = await connection.QueryAsync<Arrangement>(
            @"SELECT a.id, a.name, a.description, a.arranger, a.composer, a.key, a.duration_seconds, a.year
              FROM arrangements a
              INNER JOIN arrangement_performances ap ON a.id = ap.arrangement_id
              WHERE ap.performance_id = @Id", new { Id = id });
        performance.Arrangements = arrangements.ToList();

        return performance;
    }

    public async Task<Performance> AddPerformanceAsync(Performance performance)
    {
        using var connection = _connectionFactory.CreateConnection();
        var id = await connection.InsertReturningIdAsync(
            "INSERT INTO performances (name, link, performance_date, notes) VALUES (@Name, @Link, @PerformanceDate, @Notes)",
            new { performance.Name, performance.Link, performance.PerformanceDate, performance.Notes });
        performance.Id = id;
        InvalidateArrangementCache();
        return performance;
    }

    public async Task<Performance?> UpdatePerformanceAsync(int id, Performance performance)
    {
        using var connection = _connectionFactory.CreateConnection();
        var rows = await connection.ExecuteAsync(
            "UPDATE performances SET name = @Name, link = @Link, performance_date = @PerformanceDate, notes = @Notes WHERE id = @Id",
            new { performance.Name, performance.Link, performance.PerformanceDate, performance.Notes, Id = id });
        if (rows == 0) return null;
        performance.Id = id;
        InvalidateArrangementCache();
        return performance;
    }

    public async Task<bool> DeletePerformanceAsync(int id)
    {
        using var connection = _connectionFactory.CreateConnection();
        var rows = await connection.ExecuteAsync("DELETE FROM performances WHERE id = @Id", new { Id = id });
        if (rows == 0) return false;
        InvalidateArrangementCache();
        return true;
    }

    private void InvalidateArrangementCache()
    {
        _cache.Remove(ArrangementRepository.ArrangementsCacheKey);
    }
}
