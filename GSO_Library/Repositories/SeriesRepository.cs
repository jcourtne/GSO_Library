using Dapper;
using GSO_Library.Data;
using GSO_Library.Models;
using Microsoft.Extensions.Caching.Memory;

namespace GSO_Library.Repositories;

public class SeriesRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IMemoryCache _cache;

    public SeriesRepository(IDbConnectionFactory connectionFactory, IMemoryCache cache)
    {
        _connectionFactory = connectionFactory;
        _cache = cache;
    }

    public async Task<IEnumerable<Series>> GetAllSeriesAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        var seriesList = (await connection.QueryAsync<Series>("SELECT id, name, description FROM series")).ToList();
        if (seriesList.Count == 0) return seriesList;

        var seriesIds = seriesList.Select(s => s.Id).ToList();
        var games = await connection.QueryAsync<Game>(
            "SELECT id, name, description, series_id FROM games WHERE series_id IN @Ids",
            new { Ids = seriesIds });

        var gameLookup = games.GroupBy(g => g.SeriesId).ToDictionary(g => g.Key, g => g.ToList());
        foreach (var s in seriesList)
            s.Games = gameLookup.GetValueOrDefault(s.Id, []);

        return seriesList;
    }

    public async Task<Series?> GetSeriesByIdAsync(int id)
    {
        using var connection = _connectionFactory.CreateConnection();
        var series = await connection.QuerySingleOrDefaultAsync<Series>(
            "SELECT id, name, description FROM series WHERE id = @Id", new { Id = id });
        if (series == null) return null;

        var games = await connection.QueryAsync<Game>(
            "SELECT id, name, description, series_id FROM games WHERE series_id = @Id", new { Id = id });
        series.Games = games.ToList();

        return series;
    }

    public async Task<PaginatedResult<Series>> GetAllSeriesAsync(int page, int pageSize)
    {
        using var connection = _connectionFactory.CreateConnection();
        var totalCount = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM series");
        var seriesList = (await connection.QueryAsync<Series>(
            "SELECT id, name, description FROM series ORDER BY id LIMIT @Limit OFFSET @Offset",
            new { Limit = pageSize, Offset = (page - 1) * pageSize })).ToList();

        if (seriesList.Count > 0)
        {
            var seriesIds = seriesList.Select(s => s.Id).ToList();
            var games = await connection.QueryAsync<Game>(
                "SELECT id, name, description, series_id FROM games WHERE series_id IN @Ids",
                new { Ids = seriesIds });
            var gameLookup = games.GroupBy(g => g.SeriesId).ToDictionary(g => g.Key, g => g.ToList());
            foreach (var s in seriesList)
                s.Games = gameLookup.GetValueOrDefault(s.Id, []);
        }

        return new PaginatedResult<Series> { Items = seriesList, Page = page, PageSize = pageSize, TotalCount = totalCount };
    }

    public async Task<Series> AddSeriesAsync(Series series)
    {
        using var connection = _connectionFactory.CreateConnection();
        var id = await connection.InsertReturningIdAsync(
            "INSERT INTO series (name, description) VALUES (@Name, @Description)",
            new { series.Name, series.Description });
        series.Id = id;
        InvalidateArrangementCache();
        return series;
    }

    public async Task<Series?> UpdateSeriesAsync(int id, Series series)
    {
        using var connection = _connectionFactory.CreateConnection();
        var rows = await connection.ExecuteAsync(
            "UPDATE series SET name = @Name, description = @Description WHERE id = @Id",
            new { series.Name, series.Description, Id = id });
        if (rows == 0) return null;
        series.Id = id;
        InvalidateArrangementCache();
        return series;
    }

    public async Task<bool> DeleteSeriesAsync(int id)
    {
        using var connection = _connectionFactory.CreateConnection();
        var rows = await connection.ExecuteAsync("DELETE FROM series WHERE id = @Id", new { Id = id });
        if (rows == 0) return false;
        InvalidateArrangementCache();
        return true;
    }

    private void InvalidateArrangementCache()
    {
        _cache.Remove(ArrangementRepository.ArrangementsCacheKey);
    }
}
