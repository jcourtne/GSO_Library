using System.Collections.Concurrent;
using System.IO.Compression;
using GSO_Library.Models;
using GSO_Library.Repositories;

namespace GSO_Library.Services;

public class SeasonZipCacheService(
    SeasonShareZipRepository zipRepo,
    IFileStorageService fileStorage,
    SeasonRepository seasonRepo) : ISeasonZipCacheService
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _generateLocks = new();

    private static SemaphoreSlim GetGenerateLock(int seasonId, string zipKey) =>
        _generateLocks.GetOrAdd($"{seasonId}:{zipKey}", _ => new SemaphoreSlim(1, 1));
    private static readonly HashSet<string> PdfExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".pdf" };
    private static readonly HashSet<string> NotationExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".xml", ".mxl", ".mscz", ".dorico", ".sib" };
    private static readonly HashSet<string> PlaybackExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".mid", ".midi", ".mp3", ".wav", ".flac", ".ogg" };

    public string BuildZipKey(string? scorePartType, int? instrumentId, int? familyId = null)
    {
        if (familyId.HasValue) return $"family_{familyId}";
        if (scorePartType == null && instrumentId == null) return "all";
        if (instrumentId.HasValue) return $"instrument_{instrumentId}";
        return scorePartType switch
        {
            ScorePartType.ConductorScore => "conductor",
            ScorePartType.PercussionPart => "percussion",
            ScorePartType.VoicePart      => "voice",
            ScorePartType.UnlistedPart   => "unlisted",
            _                            => Sanitize(scorePartType ?? "misc"),
        };
    }

    public async Task<(Stream stream, string zipFileName)> GetOrGenerateAsync(
        Season season, string zipKey, string? scorePartType, int? instrumentId, int? familyId = null)
    {
        // Fast path: check cache without acquiring the lock
        var cached = await zipRepo.GetAsync(season.Id, zipKey);
        if (cached != null)
        {
            Stream? s = null;
            try
            {
                s = await fileStorage.GetFileAsync(cached.FolderPath, cached.StoredFileName);
                return (s, BuildZipFileName(season, zipKey, instrumentId, familyId));
            }
            catch
            {
                s?.Dispose();
                // File gone externally — fall through to regenerate under lock
            }
        }

        var sem = GetGenerateLock(season.Id, zipKey);
        await sem.WaitAsync();
        try
        {
            // Double-check after acquiring lock — another request may have generated it first
            var rechecked = await zipRepo.GetAsync(season.Id, zipKey);
            if (rechecked != null)
            {
                Stream? s = null;
                try
                {
                    s = await fileStorage.GetFileAsync(rechecked.FolderPath, rechecked.StoredFileName);
                    return (s, BuildZipFileName(season, zipKey, instrumentId, familyId));
                }
                catch
                {
                    s?.Dispose();
                }
            }

            var validInstrumentIds = new HashSet<int>(
                season.Arrangements.SelectMany(a => a.Instruments.Select(i => i.Id)));

            HashSet<int>? familyInstrumentIds = null;
            HashSet<string>? familyExtraTypes = null;
            if (familyId.HasValue)
            {
                var familyInstruments = season.Arrangements
                    .SelectMany(a => a.Instruments)
                    .Where(i => i.FamilyId == familyId)
                    .ToList();
                familyInstrumentIds = new HashSet<int>(familyInstruments.Select(i => i.Id));
                var familyName = familyInstruments.FirstOrDefault()?.FamilyName;
                familyExtraTypes = familyName switch
                {
                    "Percussion" => new HashSet<string> { ScorePartType.PercussionPart },
                    "Voice"      => new HashSet<string> { ScorePartType.VoicePart },
                    _            => null,
                };
            }

            var entries = season.Arrangements
                .SelectMany(a => a.Files.Select(f => (ArrName: a.Name, File: f)))
                .Where(x => MatchesTypeConfig(x.File, season) && MatchesFilter(x.File, scorePartType, instrumentId, validInstrumentIds, familyInstrumentIds, familyExtraTypes))
                .ToList();

            var folderPath = $"shares/{season.Id}";
            var storedFileName = $"{zipKey}.zip";
            var tempPath = Path.GetTempFileName();
            try
            {
                using (var fs = new FileStream(tempPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None, 65536, useAsync: true))
                {
                    using (var archive = new ZipArchive(fs, ZipArchiveMode.Create, leaveOpen: true))
                    {
                        foreach (var (arrName, file) in entries)
                        {
                            var entry = archive.CreateEntry(
                                $"{Sanitize(arrName)}/{Sanitize(file.FileName)}",
                                CompressionLevel.Fastest);
                            using var entryStream = entry.Open();
                            await using var src = await fileStorage.GetFileAsync(
                                $"arrangements/{file.ArrangementId}", file.StoredFileName);
                            await src.CopyToAsync(entryStream);
                        }
                    }
                    fs.Position = 0;
                    await fileStorage.SaveFileAsync(folderPath, storedFileName, fs);
                }
                await zipRepo.UpsertAsync(season.Id, zipKey, folderPath, storedFileName);
            }
            finally
            {
                try { File.Delete(tempPath); } catch { /* best effort */ }
            }
            return (await fileStorage.GetFileAsync(folderPath, storedFileName), BuildZipFileName(season, zipKey, instrumentId, familyId));
        }
        finally
        {
            sem.Release();
        }
    }

    public async Task InvalidateForSeasonAsync(int seasonId)
    {
        var zips = await zipRepo.GetAllForSeasonAsync(seasonId);
        foreach (var zip in zips)
        {
            try { await fileStorage.DeleteFileAsync(zip.FolderPath, zip.StoredFileName); }
            catch { /* best effort */ }
        }
        await zipRepo.DeleteForSeasonAsync(seasonId);
    }

    public async Task InvalidateForArrangementAsync(int arrangementId)
    {
        var seasonIds = await seasonRepo.GetSeasonIdsByArrangementAsync(arrangementId);
        foreach (var id in seasonIds)
            await InvalidateForSeasonAsync(id);
    }

    private static bool MatchesTypeConfig(ArrangementFile file, Season season)
    {
        var ext = Path.GetExtension(file.FileName)?.ToLowerInvariant() ?? "";
        if (PdfExtensions.Contains(ext)) return season.ShareIncludePdf;
        if (NotationExtensions.Contains(ext)) return season.ShareIncludeNotation;
        if (PlaybackExtensions.Contains(ext)) return season.ShareIncludePlayback;
        return false;
    }

    private static bool MatchesFilter(
        ArrangementFile file, string? scorePartType, int? instrumentId, HashSet<int> validInstrumentIds,
        HashSet<int>? familyInstrumentIds = null, HashSet<string>? familyExtraTypes = null)
    {
        if (familyInstrumentIds != null)
            return (file.ScorePartType == ScorePartType.InstrumentPart &&
                    file.InstrumentIds.Any(id => familyInstrumentIds.Contains(id)))
                || (familyExtraTypes != null && familyExtraTypes.Contains(file.ScorePartType ?? ""));

        if (scorePartType == null && instrumentId == null)
            return true;

        if (instrumentId.HasValue)
            return file.ScorePartType == ScorePartType.InstrumentPart && file.InstrumentIds.Contains(instrumentId.Value);

        if (scorePartType == ScorePartType.UnlistedPart)
            return file.ScorePartType == null ||
                   file.ScorePartType == ScorePartType.UnlistedPart ||
                   (file.ScorePartType == ScorePartType.InstrumentPart &&
                    !file.InstrumentIds.Any(id => validInstrumentIds.Contains(id)));

        return file.ScorePartType == scorePartType;
    }

    private string BuildZipFileName(Season season, string zipKey, int? instrumentId, int? familyId = null)
    {
        var baseName = Sanitize(season.Name);
        if (familyId.HasValue)
        {
            var familyName = season.Arrangements
                .SelectMany(a => a.Instruments)
                .FirstOrDefault(i => i.FamilyId == familyId)?.FamilyName ?? $"Family {familyId}";
            return $"{baseName} - {Sanitize(familyName)}.zip";
        }
        return zipKey switch
        {
            "all"       => $"{baseName}.zip",
            "conductor"  => $"{baseName} - Conductor's Score.zip",
            "percussion" => $"{baseName} - Percussion (Generic).zip",
            "voice"      => $"{baseName} - Voice (Generic).zip",
            "unlisted"   => $"{baseName} - Unlisted Parts.zip",
            _ when instrumentId.HasValue =>
                $"{baseName} - {Sanitize(GetInstrumentName(season, instrumentId.Value))}.zip",
            _ => $"{baseName}.zip",
        };
    }

    private static string GetInstrumentName(Season season, int instrumentId)
        => season.Arrangements
            .SelectMany(a => a.Instruments)
            .FirstOrDefault(i => i.Id == instrumentId)?.Name
            ?? $"Instrument {instrumentId}";

    private static string Sanitize(string name)
        => System.Text.RegularExpressions.Regex.Replace(name, @"[\\/:*?""<>|]", "_");
}
