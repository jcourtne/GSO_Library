using System.Data;
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
            @"INSERT INTO arrangement_files (file_name, stored_file_name, content_type, file_size, uploaded_at, arrangement_id, created_by, score_part_type)
              VALUES (@FileName, @StoredFileName, @ContentType, @FileSize, @UploadedAt, @ArrangementId, @CreatedBy, @ScorePartType)",
            new { file.FileName, file.StoredFileName, file.ContentType, file.FileSize, file.UploadedAt, file.ArrangementId, file.CreatedBy, file.ScorePartType });
        file.Id = id;
        return file;
    }

    public async Task<List<ArrangementFile>> GetFilesByArrangementIdAsync(int arrangementId)
    {
        using var connection = _connectionFactory.CreateConnection();
        var files = (await connection.QueryAsync<ArrangementFile>(
            "SELECT id, file_name, stored_file_name, content_type, file_size, uploaded_at, arrangement_id, created_by, score_part_type FROM arrangement_files WHERE arrangement_id = @Id",
            new { Id = arrangementId })).ToList();
        await PopulateInstrumentIds(connection, files);
        return files;
    }

    public async Task<ArrangementFile?> GetFileAsync(int arrangementId, int fileId)
    {
        using var connection = _connectionFactory.CreateConnection();
        var file = await connection.QuerySingleOrDefaultAsync<ArrangementFile>(
            "SELECT id, file_name, stored_file_name, content_type, file_size, uploaded_at, arrangement_id, created_by, score_part_type FROM arrangement_files WHERE id = @FileId AND arrangement_id = @ArrangementId",
            new { FileId = fileId, ArrangementId = arrangementId });
        if (file != null)
            await PopulateInstrumentIds(connection, [file]);
        return file;
    }

    public async Task<bool> UpdateFileMetadataAsync(int arrangementId, int fileId, string? scorePartType, int[]? instrumentIds)
    {
        using var connection = _connectionFactory.CreateConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();

        var rows = await connection.ExecuteAsync(
            @"UPDATE arrangement_files
              SET score_part_type = @ScorePartType
              WHERE id = @FileId AND arrangement_id = @ArrangementId",
            new { ScorePartType = scorePartType, FileId = fileId, ArrangementId = arrangementId }, tx);

        if (rows == 0)
        {
            tx.Rollback();
            return false;
        }

        await connection.ExecuteAsync(
            "DELETE FROM arrangement_file_instruments WHERE file_id = @FileId",
            new { FileId = fileId }, tx);

        foreach (var instrumentId in instrumentIds ?? [])
        {
            await connection.ExecuteAsync(
                "INSERT INTO arrangement_file_instruments (file_id, instrument_id) VALUES (@FileId, @InstrumentId)",
                new { FileId = fileId, InstrumentId = instrumentId }, tx);
        }

        tx.Commit();
        return true;
    }

    public async Task<bool> DeleteFileAsync(int arrangementId, int fileId)
    {
        using var connection = _connectionFactory.CreateConnection();
        var rows = await connection.ExecuteAsync(
            "DELETE FROM arrangement_files WHERE id = @FileId AND arrangement_id = @ArrangementId",
            new { FileId = fileId, ArrangementId = arrangementId });
        return rows > 0;
    }

    private static async Task PopulateInstrumentIds(IDbConnection connection, IEnumerable<ArrangementFile> files)
    {
        var ids = files.Select(f => f.Id).ToList();
        if (ids.Count == 0) return;
        var links = await connection.QueryInListAsync<FileInstrumentLink>(
            "SELECT file_id, instrument_id FROM arrangement_file_instruments WHERE file_id = ANY(@Ids)",
            new { Ids = ids.ToArray() });
        var map = links.GroupBy(l => l.FileId)
            .ToDictionary(g => g.Key, g => g.Select(l => l.InstrumentId).ToList());
        foreach (var f in files)
            if (map.TryGetValue(f.Id, out var instIds))
                f.InstrumentIds = instIds;
    }

    private sealed class FileInstrumentLink
    {
        public int FileId { get; init; }
        public int InstrumentId { get; init; }
    }
}
