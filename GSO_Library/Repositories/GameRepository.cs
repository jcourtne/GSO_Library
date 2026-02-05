using Dapper;
using GSO_Library.Data;
using GSO_Library.Models;
using Microsoft.Extensions.Caching.Memory;

namespace GSO_Library.Repositories;

public class GameRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IMemoryCache _cache;

    public GameRepository(IDbConnectionFactory connectionFactory, IMemoryCache cache)
    {
        _connectionFactory = connectionFactory;
        _cache = cache;
    }

    private static readonly Dictionary<string, string> _sortColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        ["id"] = "g.id",
        ["name"] = "g.name",
        ["createdat"] = "g.created_at"
    };

    public async Task<IEnumerable<Game>> GetAllGamesAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        var games = (await connection.QueryAsync<Game, Series, Game>(
            @"SELECT g.id, g.name, g.description, g.series_id, g.created_at, g.updated_at, g.created_by,
                     s.id, s.name, s.description
              FROM games g
              LEFT JOIN series s ON g.series_id = s.id",
            (game, series) => { game.Series = series; return game; },
            splitOn: "id")).ToList();
        return games;
    }

    public async Task<Game?> GetGameByIdAsync(int id)
    {
        using var connection = _connectionFactory.CreateConnection();
        var game = (await connection.QueryAsync<Game, Series, Game>(
            @"SELECT g.id, g.name, g.description, g.series_id, g.created_at, g.updated_at, g.created_by,
                     s.id, s.name, s.description
              FROM games g
              LEFT JOIN series s ON g.series_id = s.id
              WHERE g.id = @Id",
            (game, series) => { game.Series = series; return game; },
            new { Id = id },
            splitOn: "id")).FirstOrDefault();

        if (game == null) return null;

        // Load linked arrangements via junction table
        var arrangements = await connection.QueryAsync<Arrangement>(
            @"SELECT a.id, a.name, a.description, a.arranger, a.composer, a.key, a.duration_seconds, a.year, a.created_at, a.updated_at, a.created_by
              FROM arrangements a
              INNER JOIN arrangement_games ag ON a.id = ag.arrangement_id
              WHERE ag.game_id = @Id", new { Id = id });
        game.Arrangements = arrangements.ToList();

        return game;
    }

    public async Task<PaginatedResult<Game>> GetAllGamesAsync(int page, int pageSize, string? sortBy = null, string? sortDirection = null)
    {
        using var connection = _connectionFactory.CreateConnection();
        var totalCount = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM games");
        var orderColumn = _sortColumns.GetValueOrDefault(sortBy ?? "", "g.id");
        var orderDir = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC";
        var games = (await connection.QueryAsync<Game, Series, Game>(
            $@"SELECT g.id, g.name, g.description, g.series_id, g.created_at, g.updated_at, g.created_by,
                      s.id, s.name, s.description
               FROM games g
               LEFT JOIN series s ON g.series_id = s.id
               ORDER BY {orderColumn} {orderDir} LIMIT @Limit OFFSET @Offset",
            (game, series) => { game.Series = series; return game; },
            new { Limit = pageSize, Offset = (page - 1) * pageSize },
            splitOn: "id")).ToList();

        return new PaginatedResult<Game> { Items = games, Page = page, PageSize = pageSize, TotalCount = totalCount };
    }

    public async Task<Game> AddGameAsync(Game game)
    {
        using var connection = _connectionFactory.CreateConnection();
        var id = await connection.InsertReturningIdAsync(
            "INSERT INTO games (name, description, series_id, created_at, updated_at, created_by) VALUES (@Name, @Description, @SeriesId, @CreatedAt, @UpdatedAt, @CreatedBy)",
            new { game.Name, game.Description, game.SeriesId, game.CreatedAt, game.UpdatedAt, game.CreatedBy });
        game.Id = id;
        InvalidateArrangementCache();
        return game;
    }

    public async Task<Game?> UpdateGameAsync(int id, Game game)
    {
        using var connection = _connectionFactory.CreateConnection();
        var rows = await connection.ExecuteAsync(
            "UPDATE games SET name = @Name, description = @Description, series_id = @SeriesId, updated_at = @UpdatedAt WHERE id = @Id",
            new { game.Name, game.Description, game.SeriesId, game.UpdatedAt, Id = id });
        if (rows == 0) return null;
        game.Id = id;
        InvalidateArrangementCache();
        return game;
    }

    public async Task<bool> DeleteGameAsync(int id)
    {
        using var connection = _connectionFactory.CreateConnection();
        var rows = await connection.ExecuteAsync("DELETE FROM games WHERE id = @Id", new { Id = id });
        if (rows == 0) return false;
        InvalidateArrangementCache();
        return true;
    }

    private void InvalidateArrangementCache()
    {
        _cache.Remove(ArrangementRepository.ArrangementsCacheKey);
    }
}
