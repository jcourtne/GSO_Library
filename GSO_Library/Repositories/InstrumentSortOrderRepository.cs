using Dapper;
using GSO_Library.Data;
using GSO_Library.Dtos;
using GSO_Library.Models;

namespace GSO_Library.Repositories;

public class InstrumentSortOrderRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public InstrumentSortOrderRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<List<InstrumentSortOrder>> GetAllAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<(int SortOrderId, string SortOrderName, bool IsDefault, int? InstrumentId, string? InstrumentName, int? Position)>(
            """
            SELECT iso.id, iso.name, iso.is_default,
                   i.id, i.name, isoi.position
            FROM instrument_sort_orders iso
            LEFT JOIN instrument_sort_order_items isoi ON isoi.sort_order_id = iso.id
            LEFT JOIN instruments i ON i.id = isoi.instrument_id
            ORDER BY iso.id, isoi.position
            """);

        return BuildSortOrders(rows);
    }

    public async Task<InstrumentSortOrder?> GetByIdAsync(int id)
    {
        using var connection = _connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<(int SortOrderId, string SortOrderName, bool IsDefault, int? InstrumentId, string? InstrumentName, int? Position)>(
            """
            SELECT iso.id, iso.name, iso.is_default,
                   i.id, i.name, isoi.position
            FROM instrument_sort_orders iso
            LEFT JOIN instrument_sort_order_items isoi ON isoi.sort_order_id = iso.id
            LEFT JOIN instruments i ON i.id = isoi.instrument_id
            WHERE iso.id = @Id
            ORDER BY isoi.position
            """, new { Id = id });

        return BuildSortOrders(rows).FirstOrDefault();
    }

    public async Task<InstrumentSortOrder?> GetDefaultAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<(int SortOrderId, string SortOrderName, bool IsDefault, int? InstrumentId, string? InstrumentName, int? Position)>(
            """
            SELECT iso.id, iso.name, iso.is_default,
                   i.id, i.name, isoi.position
            FROM instrument_sort_orders iso
            LEFT JOIN instrument_sort_order_items isoi ON isoi.sort_order_id = iso.id
            LEFT JOIN instruments i ON i.id = isoi.instrument_id
            WHERE iso.is_default = TRUE
            ORDER BY isoi.position
            """);

        return BuildSortOrders(rows).FirstOrDefault();
    }

    /// <summary>
    /// Returns ALL instruments ordered for a given sort order:
    /// explicitly sorted instruments first (by position), then remaining alphabetically.
    /// </summary>
    public async Task<List<Instrument>> GetOrderedInstrumentsAsync(int sortOrderId)
    {
        using var connection = _connectionFactory.CreateConnection();
        return (await connection.QueryAsync<Instrument>(
            """
            SELECT i.id, i.name, i.family_id, i.created_at, i.updated_at, i.created_by,
                   f.name as family_name, isoi.position
            FROM instruments i
            LEFT JOIN instrument_sort_order_items isoi
                ON isoi.instrument_id = i.id AND isoi.sort_order_id = @SortOrderId
            LEFT JOIN instrument_families f ON f.id = i.family_id
            ORDER BY isoi.position NULLS LAST, i.name ASC
            """, new { SortOrderId = sortOrderId })).ToList();
    }

    public async Task<InstrumentSortOrder> CreateAsync(InstrumentSortOrderRequest request)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (request.IsDefault)
            await connection.ExecuteAsync("UPDATE instrument_sort_orders SET is_default = FALSE");

        var id = await connection.InsertReturningIdAsync(
            "INSERT INTO instrument_sort_orders (name, is_default) VALUES (@Name, @IsDefault)",
            new { request.Name, request.IsDefault });

        await InsertItemsAsync(connection, id, request.InstrumentIds);

        return (await GetByIdAsync(id))!;
    }

    public async Task<InstrumentSortOrder?> UpdateAsync(int id, InstrumentSortOrderRequest request)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (request.IsDefault)
            await connection.ExecuteAsync("UPDATE instrument_sort_orders SET is_default = FALSE WHERE id != @Id", new { Id = id });

        var rows = await connection.ExecuteAsync(
            "UPDATE instrument_sort_orders SET name = @Name, is_default = @IsDefault WHERE id = @Id",
            new { request.Name, request.IsDefault, Id = id });

        if (rows == 0) return null;

        await connection.ExecuteAsync(
            "DELETE FROM instrument_sort_order_items WHERE sort_order_id = @Id", new { Id = id });
        await InsertItemsAsync(connection, id, request.InstrumentIds);

        return await GetByIdAsync(id);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        using var connection = _connectionFactory.CreateConnection();
        var rows = await connection.ExecuteAsync(
            "DELETE FROM instrument_sort_orders WHERE id = @Id", new { Id = id });
        return rows > 0;
    }

    private static async Task InsertItemsAsync(System.Data.IDbConnection connection, int sortOrderId, List<int> instrumentIds)
    {
        for (var i = 0; i < instrumentIds.Count; i++)
        {
            await connection.ExecuteAsync(
                "INSERT INTO instrument_sort_order_items (sort_order_id, instrument_id, position) VALUES (@SortOrderId, @InstrumentId, @Position)",
                new { SortOrderId = sortOrderId, InstrumentId = instrumentIds[i], Position = i + 1 });
        }
    }

    private static List<InstrumentSortOrder> BuildSortOrders(
        IEnumerable<(int SortOrderId, string SortOrderName, bool IsDefault, int? InstrumentId, string? InstrumentName, int? Position)> rows)
    {
        var result = new List<InstrumentSortOrder>();
        var lookup = new Dictionary<int, InstrumentSortOrder>();

        foreach (var row in rows)
        {
            if (!lookup.TryGetValue(row.SortOrderId, out var so))
            {
                so = new InstrumentSortOrder { Id = row.SortOrderId, Name = row.SortOrderName, IsDefault = row.IsDefault };
                lookup[row.SortOrderId] = so;
                result.Add(so);
            }

            if (row.InstrumentId.HasValue)
                so.Instruments.Add(new Instrument { Id = row.InstrumentId.Value, Name = row.InstrumentName! });
        }

        return result;
    }
}
