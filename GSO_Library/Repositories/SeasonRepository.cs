using Dapper;
using GSO_Library.Data;
using GSO_Library.Models;
using Microsoft.Extensions.Caching.Memory;

namespace GSO_Library.Repositories;

public class SeasonRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IMemoryCache _cache;

    public SeasonRepository(IDbConnectionFactory connectionFactory, IMemoryCache cache)
    {
        _connectionFactory = connectionFactory;
        _cache = cache;
    }

    private static readonly Dictionary<string, string> _sortColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        ["id"]        = "id",
        ["name"]      = "name",
        ["startdate"] = "start_date",
        ["enddate"]   = "end_date",
        ["createdat"] = "created_at",
    };

    public async Task<PaginatedResult<Season>> GetAllSeasonsAsync(
        int page, int pageSize, string? sortBy = null, string? sortDirection = null,
        string? search = null, int[]? ensembleIds = null,
        DateTime? dateFrom = null, DateTime? dateTo = null)
    {
        using var connection = _connectionFactory.CreateConnection();
        var conditions = new List<string>();
        if (!string.IsNullOrWhiteSpace(search)) conditions.Add("LOWER(name) LIKE @Search");
        if (ensembleIds?.Length > 0) conditions.Add("ensemble_id = ANY(@EnsembleIds)");
        if (dateFrom.HasValue) conditions.Add("start_date >= @DateFrom");
        if (dateTo.HasValue) conditions.Add("start_date <= @DateTo");
        var whereClause = conditions.Count > 0 ? " WHERE " + string.Join(" AND ", conditions) : "";
        var searchParam = string.IsNullOrWhiteSpace(search) ? null : $"%{search.ToLower()}%";

        var countResult = await connection.QueryInListAsync<int>(
            $"SELECT COUNT(*) FROM seasons{whereClause}",
            new { Search = searchParam, EnsembleIds = ensembleIds, DateFrom = dateFrom, DateTo = dateTo });
        var totalCount = countResult.First();

        var orderColumn = _sortColumns.GetValueOrDefault(sortBy ?? "", "id");
        var orderDir = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC";

        var items = (await connection.QueryInListAsync<Season>(
            $"SELECT id, name, ensemble_id, start_date, end_date, notes, created_at, updated_at, created_by, share_token, share_include_pdf, share_include_notation, share_include_playback, share_password_hash FROM seasons{whereClause} ORDER BY {orderColumn} {orderDir} LIMIT @Limit OFFSET @Offset",
            new { Limit = pageSize, Offset = (page - 1) * pageSize, Search = searchParam, EnsembleIds = ensembleIds, DateFrom = dateFrom, DateTo = dateTo })).ToList();

        if (items.Count > 0)
        {
            var fetchedEnsembleIds = items.Select(s => s.EnsembleId).Distinct().ToArray();
            var ensembles = await connection.QueryInListAsync<Ensemble>(
                "SELECT id, name, description, website, contact_info, created_at, updated_at, created_by FROM ensembles WHERE id = ANY(@Ids)",
                new { Ids = fetchedEnsembleIds });
            var ensembleLookup = ensembles.ToDictionary(e => e.Id);
            foreach (var s in items)
                if (ensembleLookup.TryGetValue(s.EnsembleId, out var ens))
                    s.Ensemble = ens;
        }

        return new PaginatedResult<Season> { Items = items, Page = page, PageSize = pageSize, TotalCount = totalCount };
    }

    public async Task<Season?> GetSeasonByIdAsync(int id)
    {
        using var connection = _connectionFactory.CreateConnection();
        var season = await connection.QuerySingleOrDefaultAsync<Season>(
            "SELECT id, name, ensemble_id, start_date, end_date, notes, created_at, updated_at, created_by, share_token, share_include_pdf, share_include_notation, share_include_playback, share_password_hash FROM seasons WHERE id = @Id",
            new { Id = id });
        if (season == null) return null;

        season.Ensemble = await connection.QuerySingleOrDefaultAsync<Ensemble>(
            "SELECT id, name, description, website, contact_info, created_at, updated_at, created_by FROM ensembles WHERE id = @Id",
            new { Id = season.EnsembleId });

        // Load linked arrangements
        var arrangements = (await connection.QueryAsync<Arrangement>(
            @"SELECT a.id, a.name, a.description, a.duration_seconds, a.year, a.created_at, a.updated_at, a.created_by
              FROM arrangements a
              INNER JOIN season_arrangements sa ON a.id = sa.arrangement_id
              WHERE sa.season_id = @Id", new { Id = id })).ToList();

        if (arrangements.Count > 0)
        {
            var arrIds = arrangements.Select(a => a.Id).ToArray();
            var composers = await connection.QueryInListAsync<(int ArrangementId, string Name)>(
                "SELECT arrangement_id, name FROM arrangement_composers WHERE arrangement_id = ANY(@Ids) ORDER BY sort_order",
                new { Ids = arrIds });
            var arrangers = await connection.QueryInListAsync<(int ArrangementId, string Name)>(
                "SELECT arrangement_id, name FROM arrangement_arrangers WHERE arrangement_id = ANY(@Ids) ORDER BY sort_order",
                new { Ids = arrIds });
            var games = await connection.QueryInListAsync<(int ArrangementId, int GameId, string GameName)>(
                @"SELECT ag.arrangement_id, g.id, g.name FROM games g
                  INNER JOIN arrangement_games ag ON g.id = ag.game_id
                  WHERE ag.arrangement_id = ANY(@Ids)",
                new { Ids = arrIds });

            var composersByArr = composers.GroupBy(c => c.ArrangementId).ToDictionary(g => g.Key, g => g.Select(c => c.Name).ToList());
            var arrangersByArr = arrangers.GroupBy(a => a.ArrangementId).ToDictionary(g => g.Key, g => g.Select(a => a.Name).ToList());
            var gamesByArr = games.GroupBy(g => g.ArrangementId).ToDictionary(g => g.Key, g => g.Select(x => new Game { Id = x.GameId, Name = x.GameName }).ToList<Game>());

            var arrInstrumentRows = await connection.QueryInListAsync<(int ArrangementId, int Id, string Name, int? FamilyId, string? FamilyName)>(
                @"SELECT ai.arrangement_id, i.id, i.name, i.family_id, f.name as family_name
                  FROM instruments i
                  LEFT JOIN instrument_families f ON i.family_id = f.id
                  INNER JOIN arrangement_instruments ai ON i.id = ai.instrument_id
                  WHERE ai.arrangement_id = ANY(@Ids)
                  ORDER BY i.name",
                new { Ids = arrIds });
            var instrumentsByArr = arrInstrumentRows.GroupBy(x => x.ArrangementId)
                .ToDictionary(g => g.Key, g => (ICollection<Instrument>)g.Select(x => new Instrument { Id = x.Id, Name = x.Name, FamilyId = x.FamilyId, FamilyName = x.FamilyName }).ToList());

            var arrFiles = (await connection.QueryInListAsync<ArrangementFile>(
                @"SELECT id, arrangement_id, file_name, stored_file_name, content_type, file_size,
                         score_part_type, uploaded_at, created_by
                  FROM arrangement_files WHERE arrangement_id = ANY(@Ids)",
                new { Ids = arrIds })).ToList();
            var fileIds = arrFiles.Select(f => f.Id).ToArray();
            if (fileIds.Length > 0)
            {
                var fileInstrumentLinks = await connection.QueryInListAsync<(int FileId, int InstrumentId)>(
                    "SELECT file_id, instrument_id FROM arrangement_file_instruments WHERE file_id = ANY(@Ids)",
                    new { Ids = fileIds });
                var fileInstrumentMap = fileInstrumentLinks.GroupBy(l => l.FileId)
                    .ToDictionary(g => g.Key, g => g.Select(l => l.InstrumentId).ToList());
                foreach (var f in arrFiles)
                    if (fileInstrumentMap.TryGetValue(f.Id, out var instIds))
                        f.InstrumentIds = instIds;
            }
            var filesByArr = arrFiles.GroupBy(f => f.ArrangementId)
                .ToDictionary(g => g.Key, g => (ICollection<ArrangementFile>)g.ToList());

            foreach (var a in arrangements)
            {
                a.Composers = composersByArr.GetValueOrDefault(a.Id, []);
                a.Arrangers = arrangersByArr.GetValueOrDefault(a.Id, []);
                a.Games = gamesByArr.GetValueOrDefault(a.Id, []);
                a.Instruments = instrumentsByArr.GetValueOrDefault(a.Id, []);
                a.Files = filesByArr.GetValueOrDefault(a.Id, []);
            }
        }
        season.Arrangements = arrangements;

        // Load linked performances
        var performances = (await connection.QueryAsync<Performance>(
            @"SELECT p.id, p.name, p.link, p.performance_date, p.notes, p.ensemble_id, p.created_at, p.updated_at, p.created_by
              FROM performances p
              INNER JOIN season_performances sp ON p.id = sp.performance_id
              WHERE sp.season_id = @Id", new { Id = id })).ToList();
        season.Performances = performances;

        return season;
    }

    public async Task<Season> AddSeasonAsync(Season season)
    {
        using var connection = _connectionFactory.CreateConnection();
        var id = await connection.InsertReturningIdAsync(
            "INSERT INTO seasons (name, ensemble_id, start_date, end_date, notes, created_at, updated_at, created_by) VALUES (@Name, @EnsembleId, @StartDate, @EndDate, @Notes, @CreatedAt, @UpdatedAt, @CreatedBy)",
            new { season.Name, season.EnsembleId, season.StartDate, season.EndDate, season.Notes, season.CreatedAt, season.UpdatedAt, season.CreatedBy });
        season.Id = id;
        InvalidateArrangementCache();
        return season;
    }

    public async Task<Season?> UpdateSeasonAsync(int id, Season season)
    {
        using var connection = _connectionFactory.CreateConnection();
        var rows = await connection.ExecuteAsync(
            "UPDATE seasons SET name = @Name, ensemble_id = @EnsembleId, start_date = @StartDate, end_date = @EndDate, notes = @Notes, updated_at = @UpdatedAt WHERE id = @Id",
            new { season.Name, season.EnsembleId, season.StartDate, season.EndDate, season.Notes, season.UpdatedAt, Id = id });
        if (rows == 0) return null;
        InvalidateArrangementCache();
        return await GetSeasonByIdAsync(id);
    }

    public async Task<bool> DeleteSeasonAsync(int id)
    {
        using var connection = _connectionFactory.CreateConnection();
        var rows = await connection.ExecuteAsync("DELETE FROM seasons WHERE id = @Id", new { Id = id });
        if (rows == 0) return false;
        InvalidateArrangementCache();
        return true;
    }

    public async Task<bool?> AddArrangementAsync(int seasonId, int arrangementId)
    {
        using var connection = _connectionFactory.CreateConnection();
        var seasonExists = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM seasons WHERE id = @Id", new { Id = seasonId });
        if (seasonExists == 0) return null;

        try
        {
            var isSqlite = connection.GetType().Name.Contains("Sqlite", StringComparison.OrdinalIgnoreCase);
            var sql = isSqlite
                ? "INSERT OR IGNORE INTO season_arrangements (season_id, arrangement_id) VALUES (@SeasonId, @ArrangementId)"
                : "INSERT INTO season_arrangements (season_id, arrangement_id) VALUES (@SeasonId, @ArrangementId) ON CONFLICT DO NOTHING";
            var rows = await connection.ExecuteAsync(sql, new { SeasonId = seasonId, ArrangementId = arrangementId });
            if (rows == 0) return false;
            InvalidateArrangementCache();
            return true;
        }
        catch
        {
            return false; // FK violation: arrangement doesn't exist
        }
    }

    public async Task<bool?> RemoveArrangementAsync(int seasonId, int arrangementId)
    {
        using var connection = _connectionFactory.CreateConnection();
        var seasonExists = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM seasons WHERE id = @Id", new { Id = seasonId });
        if (seasonExists == 0) return null;

        var rows = await connection.ExecuteAsync(
            "DELETE FROM season_arrangements WHERE season_id = @SeasonId AND arrangement_id = @ArrangementId",
            new { SeasonId = seasonId, ArrangementId = arrangementId });
        if (rows == 0) return false;
        InvalidateArrangementCache();
        return true;
    }

    public async Task<bool?> AddPerformanceAsync(int seasonId, int performanceId)
    {
        using var connection = _connectionFactory.CreateConnection();
        var seasonExists = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM seasons WHERE id = @Id", new { Id = seasonId });
        if (seasonExists == 0) return null;

        try
        {
            var isSqlite = connection.GetType().Name.Contains("Sqlite", StringComparison.OrdinalIgnoreCase);
            var sql = isSqlite
                ? "INSERT OR IGNORE INTO season_performances (season_id, performance_id) VALUES (@SeasonId, @PerformanceId)"
                : "INSERT INTO season_performances (season_id, performance_id) VALUES (@SeasonId, @PerformanceId) ON CONFLICT DO NOTHING";
            var rows = await connection.ExecuteAsync(sql, new { SeasonId = seasonId, PerformanceId = performanceId });
            if (rows == 0) return false;
            return true;
        }
        catch
        {
            return false; // FK violation: performance doesn't exist
        }
    }

    public async Task<bool?> RemovePerformanceAsync(int seasonId, int performanceId)
    {
        using var connection = _connectionFactory.CreateConnection();
        var seasonExists = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM seasons WHERE id = @Id", new { Id = seasonId });
        if (seasonExists == 0) return null;

        var rows = await connection.ExecuteAsync(
            "DELETE FROM season_performances WHERE season_id = @SeasonId AND performance_id = @PerformanceId",
            new { SeasonId = seasonId, PerformanceId = performanceId });
        if (rows == 0) return false;
        return true;
    }

    public async Task<string> UpsertShareConfigAsync(int seasonId, bool includePdf, bool includeNotation,
        bool includePlayback, string? newPasswordHash, bool clearPassword)
    {
        using var connection = _connectionFactory.CreateConnection();
        var existing = await connection.QuerySingleOrDefaultAsync<string?>(
            "SELECT share_token FROM seasons WHERE id = @Id", new { Id = seasonId });
        var token = existing ?? Guid.NewGuid().ToString("N");

        if (clearPassword)
            await connection.ExecuteAsync(
                @"UPDATE seasons SET share_token=@Token, share_include_pdf=@Pdf,
                  share_include_notation=@Notation, share_include_playback=@Playback,
                  share_password_hash=NULL WHERE id=@Id",
                new { Token = token, Pdf = includePdf, Notation = includeNotation, Playback = includePlayback, Id = seasonId });
        else if (newPasswordHash != null)
            await connection.ExecuteAsync(
                @"UPDATE seasons SET share_token=@Token, share_include_pdf=@Pdf,
                  share_include_notation=@Notation, share_include_playback=@Playback,
                  share_password_hash=@Hash WHERE id=@Id",
                new { Token = token, Pdf = includePdf, Notation = includeNotation, Playback = includePlayback, Hash = newPasswordHash, Id = seasonId });
        else
            await connection.ExecuteAsync(
                @"UPDATE seasons SET share_token=@Token, share_include_pdf=@Pdf,
                  share_include_notation=@Notation, share_include_playback=@Playback WHERE id=@Id",
                new { Token = token, Pdf = includePdf, Notation = includeNotation, Playback = includePlayback, Id = seasonId });

        return token;
    }

    public async Task<bool> RevokeShareTokenAsync(int seasonId)
    {
        using var connection = _connectionFactory.CreateConnection();
        var rows = await connection.ExecuteAsync(
            "UPDATE seasons SET share_token=NULL, share_password_hash=NULL WHERE id=@Id",
            new { Id = seasonId });
        return rows > 0;
    }

    public async Task<Season?> GetSeasonByShareTokenAsync(string token)
    {
        using var connection = _connectionFactory.CreateConnection();
        var id = await connection.QuerySingleOrDefaultAsync<int?>(
            "SELECT id FROM seasons WHERE share_token=@Token", new { Token = token });
        if (id == null) return null;
        var season = await GetSeasonByIdAsync(id.Value);
        if (season == null) return null;
        season.SharePasswordHash = await connection.QuerySingleOrDefaultAsync<string?>(
            "SELECT share_password_hash FROM seasons WHERE id=@Id", new { Id = id.Value });
        return season;
    }

    public async Task<IEnumerable<int>> GetSeasonIdsByArrangementAsync(int arrangementId)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QueryAsync<int>(
            "SELECT season_id FROM season_arrangements WHERE arrangement_id=@Id",
            new { Id = arrangementId });
    }

    private void InvalidateArrangementCache()
    {
        _cache.Remove(ArrangementRepository.ArrangementsCacheKey);
    }

}
