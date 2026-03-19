using Dapper;
using GSO_Library.Data;
using GSO_Library.Models;

namespace GSO_Library.Repositories;

public class PerformanceFileRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public PerformanceFileRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<bool> PerformanceExistsAsync(int performanceId)
    {
        using var connection = _connectionFactory.CreateConnection();
        var count = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM performances WHERE id = @Id", new { Id = performanceId });
        return count > 0;
    }

    public async Task<PerformanceFile> AddFileAsync(PerformanceFile file)
    {
        using var connection = _connectionFactory.CreateConnection();
        var id = await connection.InsertReturningIdAsync(
            @"INSERT INTO performance_files (performance_id, file_name, stored_file_name, content_type, file_size, uploaded_at, created_by)
              VALUES (@PerformanceId, @FileName, @StoredFileName, @ContentType, @FileSize, @UploadedAt, @CreatedBy)",
            new { file.PerformanceId, file.FileName, file.StoredFileName, file.ContentType, file.FileSize, file.UploadedAt, file.CreatedBy });
        file.Id = id;
        return file;
    }

    public async Task<List<PerformanceFile>> GetFilesByPerformanceIdAsync(int performanceId)
    {
        using var connection = _connectionFactory.CreateConnection();
        var files = await connection.QueryAsync<PerformanceFile>(
            "SELECT id, performance_id, file_name, stored_file_name, content_type, file_size, uploaded_at, created_by FROM performance_files WHERE performance_id = @Id",
            new { Id = performanceId });
        return files.ToList();
    }

    public async Task<PerformanceFile?> GetFileAsync(int performanceId, int fileId)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<PerformanceFile>(
            "SELECT id, performance_id, file_name, stored_file_name, content_type, file_size, uploaded_at, created_by FROM performance_files WHERE id = @FileId AND performance_id = @PerformanceId",
            new { FileId = fileId, PerformanceId = performanceId });
    }

    public async Task<bool> DeleteFileAsync(int performanceId, int fileId)
    {
        using var connection = _connectionFactory.CreateConnection();
        var rows = await connection.ExecuteAsync(
            "DELETE FROM performance_files WHERE id = @FileId AND performance_id = @PerformanceId",
            new { FileId = fileId, PerformanceId = performanceId });
        return rows > 0;
    }
}
