using Dapper;
using GSO_Library.Data;
using GSO_Library.Models;
using Microsoft.Extensions.Caching.Memory;

namespace GSO_Library.Repositories;

public class GroupInstrumentationRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IMemoryCache _cache;

    public GroupInstrumentationRepository(IDbConnectionFactory connectionFactory, IMemoryCache cache)
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

    public async Task<GroupInstrumentation?> GetGroupInstrumentationByIdAsync(int id)
    {
        using var connection = _connectionFactory.CreateConnection();
        var groupInstrumentation = await connection.QuerySingleOrDefaultAsync<GroupInstrumentation>(
            "SELECT id, name, description, created_at, updated_at, created_by FROM group_instrumentations WHERE id = @Id",
            new { Id = id });
        if (groupInstrumentation == null) return null;

        // Load linked arrangements via junction table
        var arrangements = (await connection.QueryAsync<Arrangement>(
            @"SELECT a.id, a.name, a.description, a.duration_seconds, a.year, a.created_at, a.updated_at, a.created_by
              FROM arrangements a
              INNER JOIN arrangement_group_instrumentations agi ON a.id = agi.arrangement_id
              WHERE agi.group_instrumentation_id = @Id", new { Id = id })).ToList();

        // Load composers/arrangers for linked arrangements
        var arrIds = arrangements.Select(a => a.Id).ToArray();
        if (arrIds.Length > 0)
        {
            var composers = await connection.QueryInListAsync<(int ArrangementId, string Name)>(
                "SELECT arrangement_id, name FROM arrangement_composers WHERE arrangement_id = ANY(@Ids) ORDER BY sort_order", new { Ids = arrIds });
            var arrangers = await connection.QueryInListAsync<(int ArrangementId, string Name)>(
                "SELECT arrangement_id, name FROM arrangement_arrangers WHERE arrangement_id = ANY(@Ids) ORDER BY sort_order", new { Ids = arrIds });
            var composersByArr = composers.GroupBy(c => c.ArrangementId).ToDictionary(g => g.Key, g => g.Select(c => c.Name).ToList());
            var arrangersByArr = arrangers.GroupBy(a => a.ArrangementId).ToDictionary(g => g.Key, g => g.Select(a => a.Name).ToList());
            foreach (var a in arrangements)
            {
                a.Composers = composersByArr.GetValueOrDefault(a.Id, []);
                a.Arrangers = arrangersByArr.GetValueOrDefault(a.Id, []);
            }
        }
        groupInstrumentation.Arrangements = arrangements;

        return groupInstrumentation;
    }

    public async Task<PaginatedResult<GroupInstrumentation>> GetAllGroupInstrumentationsAsync(int page, int pageSize, string? sortBy = null, string? sortDirection = null, string? search = null)
    {
        using var connection = _connectionFactory.CreateConnection();
        var whereClause = string.IsNullOrWhiteSpace(search) ? "" : " WHERE LOWER(name) LIKE @Search";
        var searchParam = string.IsNullOrWhiteSpace(search) ? null : $"%{search.ToLower()}%";
        var totalCount = await connection.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM group_instrumentations{whereClause}", new { Search = searchParam });
        var orderColumn = _sortColumns.GetValueOrDefault(sortBy ?? "", "id");
        var orderDir = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC";
        var items = (await connection.QueryAsync<GroupInstrumentation>(
            $"SELECT id, name, description, created_at, updated_at, created_by FROM group_instrumentations{whereClause} ORDER BY {orderColumn} {orderDir} LIMIT @Limit OFFSET @Offset",
            new { Limit = pageSize, Offset = (page - 1) * pageSize, Search = searchParam })).ToList();

        return new PaginatedResult<GroupInstrumentation> { Items = items, Page = page, PageSize = pageSize, TotalCount = totalCount };
    }

    public async Task<(GroupInstrumentation? GroupInstrumentation, bool DuplicateName)> AddGroupInstrumentationAsync(GroupInstrumentation groupInstrumentation)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (await connection.NameExistsAsync("group_instrumentations", groupInstrumentation.Name))
            return (null, true);
        int id;
        try
        {
            id = await connection.InsertReturningIdAsync(
                "INSERT INTO group_instrumentations (name, description, created_at, updated_at, created_by) VALUES (@Name, @Description, @CreatedAt, @UpdatedAt, @CreatedBy)",
                new { groupInstrumentation.Name, groupInstrumentation.Description, groupInstrumentation.CreatedAt, groupInstrumentation.UpdatedAt, groupInstrumentation.CreatedBy });
        }
        catch (Exception ex) when (ex.IsUniqueConstraintViolation())
        {
            return (null, true);
        }
        groupInstrumentation.Id = id;
        InvalidateArrangementCache();
        return (groupInstrumentation, false);
    }

    public async Task<(GroupInstrumentation? GroupInstrumentation, bool DuplicateName)> UpdateGroupInstrumentationAsync(int id, GroupInstrumentation groupInstrumentation)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (await connection.NameExistsAsync("group_instrumentations", groupInstrumentation.Name, excludeId: id))
            return (null, true);
        int rows;
        try
        {
            rows = await connection.ExecuteAsync(
                "UPDATE group_instrumentations SET name = @Name, description = @Description, updated_at = @UpdatedAt WHERE id = @Id",
                new { groupInstrumentation.Name, groupInstrumentation.Description, groupInstrumentation.UpdatedAt, Id = id });
        }
        catch (Exception ex) when (ex.IsUniqueConstraintViolation())
        {
            return (null, true);
        }
        if (rows == 0) return (null, false);
        groupInstrumentation.Id = id;
        InvalidateArrangementCache();
        return (groupInstrumentation, false);
    }

    public async Task<bool> DeleteGroupInstrumentationAsync(int id)
    {
        using var connection = _connectionFactory.CreateConnection();
        var rows = await connection.ExecuteAsync("DELETE FROM group_instrumentations WHERE id = @Id", new { Id = id });
        if (rows == 0) return false;
        InvalidateArrangementCache();
        return true;
    }

    private void InvalidateArrangementCache()
    {
        _cache.Remove(ArrangementRepository.ArrangementsCacheKey);
    }
}
