using Dapper;
using GSO_Library.Data;
using GSO_Library.Dtos;
using GSO_Library.Models;
using Microsoft.Extensions.Caching.Memory;

namespace GSO_Library.Repositories;

public class ArrangementRepository
{
    // Cache key exposed for other repositories to invalidate when their entities change
    public const string ArrangementsCacheKey = "ArrangementsWithIncludes";

    private static readonly SemaphoreSlim _cacheLock = new(1, 1);
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IMemoryCache _cache;

    public ArrangementRepository(IDbConnectionFactory connectionFactory, IMemoryCache cache)
    {
        _connectionFactory = connectionFactory;
        _cache = cache;
    }

    // Note: Cached entries must be treated as read-only. Do not mutate returned objects.
    private async Task<List<Arrangement>> GetCachedArrangementsAsync()
    {
        // Fast path: check cache without lock
        if (_cache.TryGetValue(ArrangementsCacheKey, out List<Arrangement>? arrangements))
        {
            return arrangements!;
        }

        // Slow path: acquire lock and double-check
        await _cacheLock.WaitAsync();
        try
        {
            // Double-check after acquiring lock
            if (_cache.TryGetValue(ArrangementsCacheKey, out arrangements))
            {
                return arrangements!;
            }

            using var connection = _connectionFactory.CreateConnection();

            // Query all tables in separate queries, then assemble in memory
            var allArrangements = (await connection.QueryAsync<Arrangement>(
                "SELECT id, name, description, arranger, composer, key, duration_seconds, year, created_at, updated_at, created_by FROM arrangements")).ToList();

            var allFiles = (await connection.QueryAsync<ArrangementFile>(
                "SELECT id, file_name, stored_file_name, content_type, file_size, uploaded_at, arrangement_id, created_by FROM arrangement_files")).ToList();

            var allGames = (await connection.QueryAsync<Game>(
                "SELECT id, name, description, series_id, created_at, updated_at, created_by FROM games")).ToList();

            var allSeries = (await connection.QueryAsync<Series>(
                "SELECT id, name, description, created_at, updated_at, created_by FROM series")).ToList();

            var allInstruments = (await connection.QueryAsync<Instrument>(
                "SELECT id, name, created_at, updated_at, created_by FROM instruments")).ToList();

            var allPerformances = (await connection.QueryAsync<Performance>(
                "SELECT id, name, link, performance_date, notes, created_at, updated_at, created_by FROM performances")).ToList();

            var arrangementGames = (await connection.QueryAsync<(int ArrangementId, int GameId)>(
                "SELECT arrangement_id, game_id FROM arrangement_games")).ToList();

            var arrangementInstruments = (await connection.QueryAsync<(int ArrangementId, int InstrumentId)>(
                "SELECT arrangement_id, instrument_id FROM arrangement_instruments")).ToList();

            var arrangementPerformances = (await connection.QueryAsync<(int ArrangementId, int PerformanceId)>(
                "SELECT arrangement_id, performance_id FROM arrangement_performances")).ToList();

            // Build lookups
            var seriesLookup = allSeries.ToDictionary(s => s.Id);
            var gameLookup = allGames.ToDictionary(g => g.Id);
            var instrumentLookup = allInstruments.ToDictionary(i => i.Id);
            var performanceLookup = allPerformances.ToDictionary(p => p.Id);

            // Assign Series to Games
            foreach (var game in allGames)
                game.Series = seriesLookup.GetValueOrDefault(game.SeriesId);

            // Build junction lookups
            var gamesByArrangement = arrangementGames.GroupBy(ag => ag.ArrangementId)
                .ToDictionary(g => g.Key, g => g.Select(ag => gameLookup.GetValueOrDefault(ag.GameId)).Where(x => x != null).ToList()!);

            var instrumentsByArrangement = arrangementInstruments.GroupBy(ai => ai.ArrangementId)
                .ToDictionary(g => g.Key, g => g.Select(ai => instrumentLookup.GetValueOrDefault(ai.InstrumentId)).Where(x => x != null).ToList()!);

            var performancesByArrangement = arrangementPerformances.GroupBy(ap => ap.ArrangementId)
                .ToDictionary(g => g.Key, g => g.Select(ap => performanceLookup.GetValueOrDefault(ap.PerformanceId)).Where(x => x != null).ToList()!);

            var filesByArrangement = allFiles.GroupBy(f => f.ArrangementId)
                .ToDictionary(g => g.Key, g => g.ToList());

            // Assemble navigation properties
            foreach (var a in allArrangements)
            {
                a.Games = gamesByArrangement.GetValueOrDefault(a.Id, [])!;
                a.Instruments = instrumentsByArrangement.GetValueOrDefault(a.Id, [])!;
                a.Performances = performancesByArrangement.GetValueOrDefault(a.Id, [])!;
                a.Files = filesByArrangement.GetValueOrDefault(a.Id, []);
            }

            arrangements = allArrangements;

            _cache.Set(ArrangementsCacheKey, arrangements, new MemoryCacheEntryOptions
            {
                SlidingExpiration = TimeSpan.FromMinutes(30)
            });

            return arrangements;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    public void InvalidateCache()
    {
        _cache.Remove(ArrangementsCacheKey);
    }

    public async Task<Arrangement?> GetArrangementByIdAsync(int id)
    {
        var arrangements = await GetCachedArrangementsAsync();
        return arrangements.FirstOrDefault(a => a.Id == id);
    }

    public async Task<PaginatedResult<Arrangement>> GetArrangementsAsync(
        int page, int pageSize, int? gameId = null, int? seriesId = null, int? instrumentId = null, int? performanceId = null,
        string? sortBy = null, string? sortDirection = null)
    {
        var arrangements = await GetCachedArrangementsAsync();
        IEnumerable<Arrangement> filtered = arrangements;

        if (gameId.HasValue)
            filtered = filtered.Where(a => a.Games.Any(g => g.Id == gameId.Value));

        if (seriesId.HasValue)
            filtered = filtered.Where(a => a.Games.Any(g => g.SeriesId == seriesId.Value));

        if (instrumentId.HasValue)
            filtered = filtered.Where(a => a.Instruments.Any(i => i.Id == instrumentId.Value));

        if (performanceId.HasValue)
            filtered = filtered.Where(a => a.Performances.Any(p => p.Id == performanceId.Value));

        var desc = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);
        filtered = (sortBy?.ToLowerInvariant()) switch
        {
            "name" => desc ? filtered.OrderByDescending(a => a.Name) : filtered.OrderBy(a => a.Name),
            "arranger" => desc ? filtered.OrderByDescending(a => a.Arranger) : filtered.OrderBy(a => a.Arranger),
            "composer" => desc ? filtered.OrderByDescending(a => a.Composer) : filtered.OrderBy(a => a.Composer),
            "key" => desc ? filtered.OrderByDescending(a => a.Key) : filtered.OrderBy(a => a.Key),
            "year" => desc ? filtered.OrderByDescending(a => a.Year) : filtered.OrderBy(a => a.Year),
            "durationseconds" => desc ? filtered.OrderByDescending(a => a.DurationSeconds) : filtered.OrderBy(a => a.DurationSeconds),
            "createdat" => desc ? filtered.OrderByDescending(a => a.CreatedAt) : filtered.OrderBy(a => a.CreatedAt),
            _ => desc ? filtered.OrderByDescending(a => a.Id) : filtered.OrderBy(a => a.Id),
        };

        var totalCount = filtered.Count();
        var items = filtered.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return new PaginatedResult<Arrangement> { Items = items, Page = page, PageSize = pageSize, TotalCount = totalCount };
    }

    public async Task<Arrangement> AddArrangementAsync(ArrangementRequest request, string? createdBy = null)
    {
        var now = DateTime.UtcNow;
        using var connection = _connectionFactory.CreateConnection();
        var id = await connection.InsertReturningIdAsync(
            @"INSERT INTO arrangements (name, description, arranger, composer, key, duration_seconds, year, created_at, updated_at, created_by)
              VALUES (@Name, @Description, @Arranger, @Composer, @Key, @DurationSeconds, @Year, @CreatedAt, @UpdatedAt, @CreatedBy)",
            new { request.Name, request.Description, request.Arranger, request.Composer, request.Key, request.DurationSeconds, request.Year, CreatedAt = now, UpdatedAt = now, CreatedBy = createdBy });

        var arrangement = new Arrangement
        {
            Id = id,
            Name = request.Name,
            Description = request.Description,
            Arranger = request.Arranger,
            Composer = request.Composer,
            Key = request.Key,
            DurationSeconds = request.DurationSeconds,
            Year = request.Year,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = createdBy
        };

        InvalidateCache();
        return arrangement;
    }

    public async Task<Arrangement?> UpdateArrangementAsync(int id, ArrangementRequest request)
    {
        var now = DateTime.UtcNow;
        using var connection = _connectionFactory.CreateConnection();
        var rows = await connection.ExecuteAsync(
            @"UPDATE arrangements SET name = @Name, description = @Description, arranger = @Arranger,
              composer = @Composer, key = @Key, duration_seconds = @DurationSeconds, year = @Year,
              updated_at = @UpdatedAt
              WHERE id = @Id",
            new { request.Name, request.Description, request.Arranger, request.Composer, request.Key, request.DurationSeconds, request.Year, UpdatedAt = now, Id = id });

        if (rows == 0) return null;

        InvalidateCache();
        return new Arrangement
        {
            Id = id,
            Name = request.Name,
            Description = request.Description,
            Arranger = request.Arranger,
            Composer = request.Composer,
            Key = request.Key,
            DurationSeconds = request.DurationSeconds,
            Year = request.Year,
            UpdatedAt = now
        };
    }

    public async Task<List<ArrangementFile>?> DeleteArrangementAsync(int id)
    {
        using var connection = _connectionFactory.CreateConnection();

        // Get files before deleting (CASCADE will remove them)
        var files = (await connection.QueryAsync<ArrangementFile>(
            "SELECT id, file_name, stored_file_name, content_type, file_size, uploaded_at, arrangement_id, created_by FROM arrangement_files WHERE arrangement_id = @Id",
            new { Id = id })).ToList();

        var rows = await connection.ExecuteAsync("DELETE FROM arrangements WHERE id = @Id", new { Id = id });
        if (rows == 0) return null;

        InvalidateCache();
        return files;
    }

    public async Task<bool?> AddGameAsync(int arrangementId, int gameId)
    {
        using var connection = _connectionFactory.CreateConnection();

        var arrangementExists = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM arrangements WHERE id = @Id", new { Id = arrangementId });
        if (arrangementExists == 0) return null;

        var gameExists = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM games WHERE id = @Id", new { Id = gameId });
        if (gameExists == 0) return false;

        var linkExists = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM arrangement_games WHERE arrangement_id = @ArrangementId AND game_id = @GameId",
            new { ArrangementId = arrangementId, GameId = gameId });
        if (linkExists > 0) return false;

        await connection.ExecuteAsync(
            "INSERT INTO arrangement_games (arrangement_id, game_id) VALUES (@ArrangementId, @GameId)",
            new { ArrangementId = arrangementId, GameId = gameId });
        InvalidateCache();
        return true;
    }

    public async Task<bool?> RemoveGameAsync(int arrangementId, int gameId)
    {
        using var connection = _connectionFactory.CreateConnection();

        var arrangementExists = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM arrangements WHERE id = @Id", new { Id = arrangementId });
        if (arrangementExists == 0) return null;

        var rows = await connection.ExecuteAsync(
            "DELETE FROM arrangement_games WHERE arrangement_id = @ArrangementId AND game_id = @GameId",
            new { ArrangementId = arrangementId, GameId = gameId });
        if (rows == 0) return false;

        InvalidateCache();
        return true;
    }

    public async Task<bool?> AddInstrumentAsync(int arrangementId, int instrumentId)
    {
        using var connection = _connectionFactory.CreateConnection();

        var arrangementExists = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM arrangements WHERE id = @Id", new { Id = arrangementId });
        if (arrangementExists == 0) return null;

        var instrumentExists = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM instruments WHERE id = @Id", new { Id = instrumentId });
        if (instrumentExists == 0) return false;

        var linkExists = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM arrangement_instruments WHERE arrangement_id = @ArrangementId AND instrument_id = @InstrumentId",
            new { ArrangementId = arrangementId, InstrumentId = instrumentId });
        if (linkExists > 0) return false;

        await connection.ExecuteAsync(
            "INSERT INTO arrangement_instruments (arrangement_id, instrument_id) VALUES (@ArrangementId, @InstrumentId)",
            new { ArrangementId = arrangementId, InstrumentId = instrumentId });
        InvalidateCache();
        return true;
    }

    public async Task<bool?> RemoveInstrumentAsync(int arrangementId, int instrumentId)
    {
        using var connection = _connectionFactory.CreateConnection();

        var arrangementExists = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM arrangements WHERE id = @Id", new { Id = arrangementId });
        if (arrangementExists == 0) return null;

        var rows = await connection.ExecuteAsync(
            "DELETE FROM arrangement_instruments WHERE arrangement_id = @ArrangementId AND instrument_id = @InstrumentId",
            new { ArrangementId = arrangementId, InstrumentId = instrumentId });
        if (rows == 0) return false;

        InvalidateCache();
        return true;
    }

    public async Task<bool?> AddPerformanceAsync(int arrangementId, int performanceId)
    {
        using var connection = _connectionFactory.CreateConnection();

        var arrangementExists = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM arrangements WHERE id = @Id", new { Id = arrangementId });
        if (arrangementExists == 0) return null;

        var performanceExists = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM performances WHERE id = @Id", new { Id = performanceId });
        if (performanceExists == 0) return false;

        var linkExists = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM arrangement_performances WHERE arrangement_id = @ArrangementId AND performance_id = @PerformanceId",
            new { ArrangementId = arrangementId, PerformanceId = performanceId });
        if (linkExists > 0) return false;

        await connection.ExecuteAsync(
            "INSERT INTO arrangement_performances (arrangement_id, performance_id) VALUES (@ArrangementId, @PerformanceId)",
            new { ArrangementId = arrangementId, PerformanceId = performanceId });
        InvalidateCache();
        return true;
    }

    public async Task<bool?> RemovePerformanceAsync(int arrangementId, int performanceId)
    {
        using var connection = _connectionFactory.CreateConnection();

        var arrangementExists = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM arrangements WHERE id = @Id", new { Id = arrangementId });
        if (arrangementExists == 0) return null;

        var rows = await connection.ExecuteAsync(
            "DELETE FROM arrangement_performances WHERE arrangement_id = @ArrangementId AND performance_id = @PerformanceId",
            new { ArrangementId = arrangementId, PerformanceId = performanceId });
        if (rows == 0) return false;

        InvalidateCache();
        return true;
    }
}
