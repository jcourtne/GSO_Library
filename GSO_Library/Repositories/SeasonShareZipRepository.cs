using Dapper;
using GSO_Library.Data;
using GSO_Library.Models;

namespace GSO_Library.Repositories;

public class SeasonShareZipRepository(IDbConnectionFactory connectionFactory)
{
    public async Task<SeasonShareZip?> GetAsync(int seasonId, string zipKey)
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<SeasonShareZip>(
            "SELECT id, season_id, zip_key, folder_path, stored_file_name, created_at FROM season_share_zips WHERE season_id = @SeasonId AND zip_key = @ZipKey",
            new { SeasonId = seasonId, ZipKey = zipKey });
    }

    public async Task UpsertAsync(int seasonId, string zipKey, string folderPath, string storedFileName)
    {
        using var connection = connectionFactory.CreateConnection();
        var typeName = connection.GetType().Name;
        if (typeName.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            await connection.ExecuteAsync(
                "INSERT OR REPLACE INTO season_share_zips (season_id, zip_key, folder_path, stored_file_name, created_at) VALUES (@SeasonId, @ZipKey, @FolderPath, @StoredFileName, @CreatedAt)",
                new { SeasonId = seasonId, ZipKey = zipKey, FolderPath = folderPath, StoredFileName = storedFileName, CreatedAt = DateTime.UtcNow });
        }
        else
        {
            await connection.ExecuteAsync(
                @"INSERT INTO season_share_zips (season_id, zip_key, folder_path, stored_file_name, created_at)
                  VALUES (@SeasonId, @ZipKey, @FolderPath, @StoredFileName, @CreatedAt)
                  ON CONFLICT (season_id, zip_key) DO UPDATE SET
                    folder_path = EXCLUDED.folder_path,
                    stored_file_name = EXCLUDED.stored_file_name,
                    created_at = EXCLUDED.created_at",
                new { SeasonId = seasonId, ZipKey = zipKey, FolderPath = folderPath, StoredFileName = storedFileName, CreatedAt = DateTime.UtcNow });
        }
    }

    public async Task<IEnumerable<SeasonShareZip>> GetAllForSeasonAsync(int seasonId)
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.QueryAsync<SeasonShareZip>(
            "SELECT id, season_id, zip_key, folder_path, stored_file_name, created_at FROM season_share_zips WHERE season_id = @SeasonId",
            new { SeasonId = seasonId });
    }

    public async Task DeleteForSeasonAsync(int seasonId)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            "DELETE FROM season_share_zips WHERE season_id = @SeasonId",
            new { SeasonId = seasonId });
    }
}
