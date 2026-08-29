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

    public string BuildZipKey(string? scorePartType, int? instrumentId, int? familyId = null, int? arrangementId = null)
    {
        var baseKey = BuildBaseZipKey(scorePartType, instrumentId, familyId);
        return arrangementId.HasValue ? $"arr_{arrangementId.Value}_{baseKey}" : baseKey;
    }

    private static string BuildBaseZipKey(string? scorePartType, int? instrumentId, int? familyId)
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
            ScorePartType.NotationFiles  => "notation",
            ScorePartType.PlaybackFiles  => "playback",
            _                            => Sanitize(scorePartType ?? "misc"),
        };
    }

    public async Task EnsureGeneratedAsync(
        Season season, string zipKey, string? scorePartType, int? instrumentId, int? familyId = null, int? arrangementId = null)
    {
        var cached = await zipRepo.GetAsync(season.Id, zipKey);
        if (cached != null && await FileExistsAsync(cached.FolderPath, cached.StoredFileName))
            return;

        var sem = GetGenerateLock(season.Id, zipKey);
        await sem.WaitAsync();
        try
        {
            var rechecked = await zipRepo.GetAsync(season.Id, zipKey);
            if (rechecked != null && await FileExistsAsync(rechecked.FolderPath, rechecked.StoredFileName))
                return;

            await GenerateAndSaveAsync(season, zipKey, scorePartType, instrumentId, familyId, arrangementId);
        }
        finally
        {
            sem.Release();
        }
    }

    public async Task<(Stream stream, string zipFileName)> GetOrGenerateAsync(
        Season season, string zipKey, string? scorePartType, int? instrumentId, int? familyId = null, int? arrangementId = null)
    {
        // Fast path: check cache without acquiring the lock
        var cached = await zipRepo.GetAsync(season.Id, zipKey);
        if (cached != null)
        {
            Stream? s = null;
            try
            {
                s = await fileStorage.GetFileAsync(cached.FolderPath, cached.StoredFileName);
                return (s, BuildZipFileName(season, zipKey, instrumentId, familyId, arrangementId));
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
                    return (s, BuildZipFileName(season, zipKey, instrumentId, familyId, arrangementId));
                }
                catch
                {
                    s?.Dispose();
                }
            }

            var (folderPath, storedFileName) = await GenerateAndSaveAsync(season, zipKey, scorePartType, instrumentId, familyId, arrangementId);
            return (await fileStorage.GetFileAsync(folderPath, storedFileName), BuildZipFileName(season, zipKey, instrumentId, familyId, arrangementId));
        }
        finally
        {
            sem.Release();
        }
    }

    private async Task<bool> FileExistsAsync(string folderPath, string storedFileName)
    {
        try
        {
            await using var s = await fileStorage.GetFileAsync(folderPath, storedFileName);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task<(string folderPath, string storedFileName)> GenerateAndSaveAsync(
        Season season, string zipKey, string? scorePartType, int? instrumentId, int? familyId, int? arrangementId = null)
    {
        // Family/unlisted semantics are computed from the whole season so they match what the
        // public DTO displayed; only the source file enumeration is narrowed to one arrangement.
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

        var arrangements = arrangementId.HasValue
            ? season.Arrangements.Where(a => a.Id == arrangementId.Value)
            : season.Arrangements;

        var entries = arrangements
            .SelectMany(a => a.Files.Select(f => (ArrName: a.Name, File: f)))
            .Where(x => MatchesTypeConfig(x.File, season) && MatchesFilter(x.File, scorePartType, instrumentId, validInstrumentIds, familyInstrumentIds, familyExtraTypes))
            .ToList();

        // When scoped to a single arrangement, drop the arrangement-name folder — every file
        // shares it — and guard against rare same-name collisions within that one arrangement.
        var usedEntryNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string EntryName(string arrName, string fileName)
        {
            if (!arrangementId.HasValue)
                return $"{Sanitize(arrName)}/{Sanitize(fileName)}";
            var name = Sanitize(fileName);
            if (usedEntryNames.Add(name)) return name;
            var stem = Path.GetFileNameWithoutExtension(name);
            var ext = Path.GetExtension(name);
            for (var n = 2; ; n++)
            {
                var candidate = $"{stem} ({n}){ext}";
                if (usedEntryNames.Add(candidate)) return candidate;
            }
        }

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
                            EntryName(arrName, file.FileName),
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

        return (folderPath, storedFileName);
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

        if (scorePartType == ScorePartType.NotationFiles)
            return NotationExtensions.Contains(Path.GetExtension(file.FileName)?.ToLowerInvariant() ?? "");

        if (scorePartType == ScorePartType.PlaybackFiles)
            return PlaybackExtensions.Contains(Path.GetExtension(file.FileName)?.ToLowerInvariant() ?? "");

        return file.ScorePartType == scorePartType;
    }

    private string BuildZipFileName(Season season, string zipKey, int? instrumentId, int? familyId = null, int? arrangementId = null)
    {
        var parts = new List<string> { Sanitize(season.Name) };

        if (arrangementId.HasValue)
        {
            var arrName = season.Arrangements
                .FirstOrDefault(a => a.Id == arrangementId.Value)?.Name ?? $"Arrangement {arrangementId}";
            parts.Add(Sanitize(arrName));

            // Strip the "arr_{id}_" prefix so the section label logic below sees the base key.
            var prefix = $"arr_{arrangementId.Value}_";
            if (zipKey.StartsWith(prefix, StringComparison.Ordinal))
                zipKey = zipKey[prefix.Length..];
        }

        var label = SectionLabelForKey(season, zipKey, instrumentId, familyId);
        if (label != null) parts.Add(label);

        return $"{string.Join(" - ", parts)}.zip";
    }

    private static string? SectionLabelForKey(Season season, string baseKey, int? instrumentId, int? familyId)
    {
        if (familyId.HasValue)
        {
            var familyName = season.Arrangements
                .SelectMany(a => a.Instruments)
                .FirstOrDefault(i => i.FamilyId == familyId)?.FamilyName ?? $"Family {familyId}";
            return Sanitize(familyName);
        }
        return baseKey switch
        {
            "all"        => null,
            "conductor"  => "Conductor's Score",
            "percussion" => "Percussion (Generic)",
            "voice"      => "Voice (Generic)",
            "unlisted"   => "Unlisted Parts",
            "notation"   => "Notation Files",
            "playback"   => "Playback Files",
            _ when instrumentId.HasValue => Sanitize(GetInstrumentName(season, instrumentId.Value)),
            _ => null,
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
