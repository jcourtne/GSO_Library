using Dapper;
using GSO_Library.Data;
using GSO_Library.Models;

namespace GSO_Library.Repositories;

public class ArrangementFileRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public ArrangementFileRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<bool> ArrangementExistsAsync(int arrangementId)
    {
        using var connection = _connectionFactory.CreateConnection();
        var count = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM arrangements WHERE id = @Id", new { Id = arrangementId });
        return count > 0;
    }

    public async Task<ArrangementFile> AddFileAsync(ArrangementFile file)
    {
        using var connection = _connectionFactory.CreateConnection();
        var id = await connection.InsertReturningIdAsync(
            @"INSERT INTO arrangement_files (file_name, stored_file_name, content_type, file_size, uploaded_at, arrangement_id, created_by)
              VALUES (@FileName, @StoredFileName, @ContentType, @FileSize, @UploadedAt, @ArrangementId, @CreatedBy)",
            new { file.FileName, file.StoredFileName, file.ContentType, file.FileSize, file.UploadedAt, file.ArrangementId, file.CreatedBy });
        file.Id = id;
        return file;
    }

    public async Task<List<ArrangementFile>> GetFilesByArrangementIdAsync(int arrangementId)
    {
        using var connection = _connectionFactory.CreateConnection();
        var files = await connection.QueryAsync<ArrangementFile>(
            "SELECT id, file_name, stored_file_name, content_type, file_size, uploaded_at, arrangement_id, created_by FROM arrangement_files WHERE arrangement_id = @Id",
            new { Id = arrangementId });
        return files.ToList();
    }

    public async Task<ArrangementFile?> GetFileAsync(int arrangementId, int fileId)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<ArrangementFile>(
            "SELECT id, file_name, stored_file_name, content_type, file_size, uploaded_at, arrangement_id, created_by FROM arrangement_files WHERE id = @FileId AND arrangement_id = @ArrangementId",
            new { FileId = fileId, ArrangementId = arrangementId });
    }

    public async Task<bool> DeleteFileAsync(int arrangementId, int fileId)
    {
        using var connection = _connectionFactory.CreateConnection();
        var rows = await connection.ExecuteAsync(
            "DELETE FROM arrangement_files WHERE id = @FileId AND arrangement_id = @ArrangementId",
            new { FileId = fileId, ArrangementId = arrangementId });
        return rows > 0;
    }
}
