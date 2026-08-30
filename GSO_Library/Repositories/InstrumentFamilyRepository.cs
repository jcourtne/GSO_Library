using Dapper;
using GSO_Library.Data;
using GSO_Library.Models;

namespace GSO_Library.Repositories;

public class InstrumentFamilyRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public InstrumentFamilyRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IEnumerable<InstrumentFamily>> GetAllAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QueryAsync<InstrumentFamily>(
            "SELECT id, name FROM instrument_families ORDER BY id");
    }
}
