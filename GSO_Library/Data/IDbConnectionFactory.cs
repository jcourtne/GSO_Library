using System.Data;

namespace GSO_Library.Data;

public interface IDbConnectionFactory
{
    IDbConnection CreateConnection();
}
