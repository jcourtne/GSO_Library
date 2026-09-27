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

    private static readonly Dictionary<string, string> _sortColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        ["id"] = "id",
        ["name"] = "name",
        ["createdat"] = "created_at"
    };

    public async Task<IEnumerable<Series>> GetAllSeriesAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        var seriesList = (await connection.QueryAsync<Series>("SELECT id, name, description, created_at, updated_at, created_by FROM series")).ToList();
        if (seriesList.Count == 0) return seriesList;

        var seriesIds = seriesList.Select(s => s.Id).ToArray();
        var games = await connection.QueryInListAsync<Game>(
            "SELECT id, name, description, series_id, created_at, updated_at, created_by FROM games WHERE series_id = ANY(@Ids)",
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
            "SELECT id, name, description, created_at, updated_at, created_by FROM series WHERE id = @Id", new { Id = id });
        if (series == null) return null;

        var games = await connection.QueryAsync<Game>(
            "SELECT id, name, description, series_id, created_at, updated_at, created_by FROM games WHERE series_id = @Id", new { Id = id });
        series.Games = games.ToList();

        return series;
    }

    public async Task<PaginatedResult<Series>> GetAllSeriesAsync(int page, int pageSize, string? sortBy = null, string? sortDirection = null, string? search = null)
    {
        using var connection = _connectionFactory.CreateConnection();
        var whereClause = string.IsNullOrWhiteSpace(search) ? "" : " WHERE LOWER(name) LIKE @Search";
        var searchParam = string.IsNullOrWhiteSpace(search) ? null : $"%{search.ToLower()}%";
        var totalCount = await connection.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM series{whereClause}", new { Search = searchParam });
        var orderColumn = _sortColumns.GetValueOrDefault(sortBy ?? "", "id");
        var orderDir = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC";
        var seriesList = (await connection.QueryAsync<Series>(
            $"SELECT id, name, description, created_at, updated_at, created_by FROM series{whereClause} ORDER BY {orderColumn} {orderDir} LIMIT @Limit OFFSET @Offset",
            new { Limit = pageSize, Offset = (page - 1) * pageSize, Search = searchParam })).ToList();

        if (seriesList.Count > 0)
        {
            var seriesIds = seriesList.Select(s => s.Id).ToArray();
            var games = await connection.QueryInListAsync<Game>(
                "SELECT id, name, description, series_id, created_at, updated_at, created_by FROM games WHERE series_id = ANY(@Ids)",
                new { Ids = seriesIds });
            var gameLookup = games.GroupBy(g => g.SeriesId).ToDictionary(g => g.Key, g => g.ToList());
            foreach (var s in seriesList)
                s.Games = gameLookup.GetValueOrDefault(s.Id, []);
        }

        return new PaginatedResult<Series> { Items = seriesList, Page = page, PageSize = pageSize, TotalCount = totalCount };
    }

    public async Task<(Series? Series, bool DuplicateName)> AddSeriesAsync(Series series)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (await NameExistsAsync(connection, series.Name))
            return (null, true);
        int id;
        try
        {
            id = await connection.InsertReturningIdAsync(
                "INSERT INTO series (name, description, created_at, updated_at, created_by) VALUES (@Name, @Description, @CreatedAt, @UpdatedAt, @CreatedBy)",
                new { series.Name, series.Description, series.CreatedAt, series.UpdatedAt, series.CreatedBy });
        }
        catch (Exception ex) when (ex.IsUniqueConstraintViolation())
        {
            return (null, true);
        }
        series.Id = id;
        InvalidateArrangementCache();
        return (series, false);
    }

    public async Task<(Series? Series, bool DuplicateName)> UpdateSeriesAsync(int id, Series series)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (await NameExistsAsync(connection, series.Name, excludeId: id))
            return (null, true);
        int rows;
        try
        {
            rows = await connection.ExecuteAsync(
                "UPDATE series SET name = @Name, description = @Description, updated_at = @UpdatedAt WHERE id = @Id",
                new { series.Name, series.Description, series.UpdatedAt, Id = id });
        }
        catch (Exception ex) when (ex.IsUniqueConstraintViolation())
        {
            return (null, true);
        }
        if (rows == 0) return (null, false);
        series.Id = id;
        InvalidateArrangementCache();
        return (series, false);
    }

    private static async Task<bool> NameExistsAsync(System.Data.IDbConnection connection, string name, int? excludeId = null)
    {
        var sql = "SELECT COUNT(*) FROM series WHERE LOWER(name) = LOWER(@Name)";
        if (excludeId.HasValue) sql += " AND id != @ExcludeId";
        var count = await connection.ExecuteScalarAsync<int>(sql, new { Name = name, ExcludeId = excludeId });
        return count > 0;
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
