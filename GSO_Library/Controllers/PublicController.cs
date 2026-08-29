using GSO_Library.Dtos;
using GSO_Library.Models;
using GSO_Library.Repositories;
using GSO_Library.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace GSO_Library.Controllers;

[ApiController]
[Route("api/public")]
[AllowAnonymous]
public class PublicController(
    SeasonRepository seasonRepo,
    ISeasonZipCacheService zipCache,
    InstrumentSortOrderRepository sortOrderRepo) : ControllerBase
{
    private static bool ValidatePassword(Season season, string? provided)
    {
        if (season.SharePasswordHash == null) return true;
        if (provided == null) return false;
        return new PasswordHasher<object>()
            .VerifyHashedPassword(null!, season.SharePasswordHash, provided) != PasswordVerificationResult.Failed;
    }

    [HttpGet("seasons/{token}")]
    public async Task<ActionResult<SeasonPublicDto>> GetPublicSeason(string token)
    {
        var season = await seasonRepo.GetSeasonByShareTokenAsync(token);
        if (season == null) return NotFound();

        var pw = Request.Headers["X-Share-Password"].FirstOrDefault();
        if (!ValidatePassword(season, pw))
            return Ok(new SeasonPublicDto { Name = season.Name, RequiresPassword = true });

        var sortPositions = await GetDefaultSortPositionsAsync();
        return Ok(BuildDto(season, sortPositions));
    }

    [HttpPost("seasons/{token}/prepare-download")]
    public async Task<IActionResult> PrepareDownload(string token,
        [FromQuery] string? scorePartType = null, [FromQuery] int? instrumentId = null,
        [FromQuery] int? familyId = null, [FromQuery] int? arrangementId = null)
    {
        if (scorePartType != null && !ScorePartType.All.Contains(scorePartType))
            return BadRequest("Invalid scorePartType");

        var season = await seasonRepo.GetSeasonByShareTokenAsync(token);
        if (season == null) return NotFound();

        var pw = Request.Headers["X-Share-Password"].FirstOrDefault();
        if (!ValidatePassword(season, pw)) return Unauthorized();

        if (arrangementId.HasValue && season.Arrangements.All(a => a.Id != arrangementId.Value))
            return BadRequest("Invalid arrangementId");

        var zipKey = zipCache.BuildZipKey(scorePartType, instrumentId, familyId, arrangementId);
        await zipCache.EnsureGeneratedAsync(season, zipKey, scorePartType, instrumentId, familyId, arrangementId);
        return NoContent();
    }

    [HttpGet("seasons/{token}/download")]
    public async Task<IActionResult> Download(string token,
        [FromQuery] string? scorePartType = null, [FromQuery] int? instrumentId = null,
        [FromQuery] int? familyId = null, [FromQuery] int? arrangementId = null)
    {
        if (scorePartType != null && !ScorePartType.All.Contains(scorePartType))
            return BadRequest("Invalid scorePartType");

        var season = await seasonRepo.GetSeasonByShareTokenAsync(token);
        if (season == null) return NotFound();

        var pw = Request.Headers["X-Share-Password"].FirstOrDefault();
        if (!ValidatePassword(season, pw)) return Unauthorized();

        if (arrangementId.HasValue && season.Arrangements.All(a => a.Id != arrangementId.Value))
            return BadRequest("Invalid arrangementId");

        var zipKey = zipCache.BuildZipKey(scorePartType, instrumentId, familyId, arrangementId);
        var (stream, zipFileName) = await zipCache.GetOrGenerateAsync(season, zipKey, scorePartType, instrumentId, familyId, arrangementId);
        return File(stream, "application/zip", zipFileName);
    }

    private async Task<Dictionary<int, int>> GetDefaultSortPositionsAsync()
    {
        var defaultOrder = await sortOrderRepo.GetDefaultAsync();
        if (defaultOrder == null) return new Dictionary<int, int>();
        return defaultOrder.Instruments
            .Select((inst, i) => (inst.Id, Pos: i))
            .ToDictionary(x => x.Id, x => x.Pos);
    }

    private static SeasonPublicDto BuildDto(Season season, Dictionary<int, int> sortPositions)
    {
        var validInstrumentIds = new HashSet<int>(
            season.Arrangements.SelectMany(a => a.Instruments.Select(i => i.Id)));

        var filteredFiles = season.Arrangements
            .SelectMany(a => a.Files.Select(f => (Arr: a, File: f)))
            .Where(x => MatchesTypeConfig(x.File, season))
            .ToList();

        var sections = new List<DownloadSectionDto>();

        static Dictionary<int, int> CountByArrangement<T>(IEnumerable<T> items, Func<T, int> arrangementId)
            => items.GroupBy(arrangementId).ToDictionary(g => g.Key, g => g.Count());

        // Conductor
        var conductorFiles = filteredFiles.Where(x => x.File.ScorePartType == ScorePartType.ConductorScore).ToList();
        if (conductorFiles.Count > 0)
            sections.Add(new DownloadSectionDto
            {
                Label = "Conductor's Score",
                ScorePartType = ScorePartType.ConductorScore,
                FileCount = conductorFiles.Count,
                LastUpdated = conductorFiles.Max(x => (DateTime?)x.File.UploadedAt),
                ArrangementFileCounts = CountByArrangement(conductorFiles, x => x.Arr.Id),
            });

        // Per-instrument, sorted by default score order then alphabetically
        var instrumentMap = new Dictionary<int, string>();
        var instrumentFamilyMap = new Dictionary<int, (int? FamilyId, string? FamilyName)>();
        foreach (var arr in season.Arrangements)
            foreach (var inst in arr.Instruments)
            {
                instrumentMap.TryAdd(inst.Id, inst.Name);
                instrumentFamilyMap.TryAdd(inst.Id, (inst.FamilyId, inst.FamilyName));
            }

        var instrumentGroups = filteredFiles
            .Where(x => x.File.ScorePartType == ScorePartType.InstrumentPart && x.File.InstrumentIds.Count > 0)
            .SelectMany(x => x.File.InstrumentIds
                .Where(id => validInstrumentIds.Contains(id))
                .Select(id => (x.Arr, x.File, InstrumentId: id)))
            .GroupBy(x => x.InstrumentId)
            .OrderBy(g => sortPositions.TryGetValue(g.Key, out var pos) ? pos : int.MaxValue)
            .ThenBy(g => instrumentMap.GetValueOrDefault(g.Key, ""));

        foreach (var group in instrumentGroups)
        {
            var (familyId, familyName) = instrumentFamilyMap.GetValueOrDefault(group.Key, (null, null));
            sections.Add(new DownloadSectionDto
            {
                Label = instrumentMap.GetValueOrDefault(group.Key, $"Instrument {group.Key}"),
                ScorePartType = ScorePartType.InstrumentPart,
                InstrumentId = group.Key,
                FamilyId = familyId,
                FamilyName = familyName,
                FileCount = group.Count(),
                LastUpdated = group.Max(x => (DateTime?)x.File.UploadedAt),
                ArrangementFileCounts = CountByArrangement(group, x => x.Arr.Id),
            });
        }

        // Insert generic sections within their family group (after last instrument of that family),
        // falling back to before unlisted when no instruments of that family exist.
        void InsertGenericSection(DownloadSectionDto generic)
        {
            var lastIdx = -1;
            for (var i = sections.Count - 1; i >= 0; i--)
            {
                if (sections[i].FamilyName == generic.FamilyName) { lastIdx = i; break; }
            }
            if (lastIdx >= 0) sections.Insert(lastIdx + 1, generic);
            else sections.Add(generic);
        }

        var percussionFiles = filteredFiles.Where(x => x.File.ScorePartType == ScorePartType.PercussionPart).ToList();
        if (percussionFiles.Count > 0)
        {
            var percFamilyId = instrumentFamilyMap.Values
                .Where(v => v.FamilyName == "Percussion").Select(v => v.FamilyId).FirstOrDefault();
            InsertGenericSection(new DownloadSectionDto
            {
                Label = "Percussion (Generic)",
                ScorePartType = ScorePartType.PercussionPart,
                FamilyName = "Percussion",
                FamilyId = percFamilyId,
                FileCount = percussionFiles.Count,
                LastUpdated = percussionFiles.Max(x => (DateTime?)x.File.UploadedAt),
                ArrangementFileCounts = CountByArrangement(percussionFiles, x => x.Arr.Id),
            });
        }

        var voiceFiles = filteredFiles.Where(x => x.File.ScorePartType == ScorePartType.VoicePart).ToList();
        if (voiceFiles.Count > 0)
        {
            var voiceFamilyId = instrumentFamilyMap.Values
                .Where(v => v.FamilyName == "Voice").Select(v => v.FamilyId).FirstOrDefault();
            InsertGenericSection(new DownloadSectionDto
            {
                Label = "Voice (Generic)",
                ScorePartType = ScorePartType.VoicePart,
                FamilyName = "Voice",
                FamilyId = voiceFamilyId,
                FileCount = voiceFiles.Count,
                LastUpdated = voiceFiles.Max(x => (DateTime?)x.File.UploadedAt),
                ArrangementFileCounts = CountByArrangement(voiceFiles, x => x.Arr.Id),
            });
        }

        // Notation Files
        var notationSectionFiles = filteredFiles
            .Where(x => NotationExtensions.Contains(Path.GetExtension(x.File.FileName)?.ToLowerInvariant() ?? ""))
            .ToList();
        if (notationSectionFiles.Count > 0)
            sections.Add(new DownloadSectionDto
            {
                Label = "Notation Files",
                ScorePartType = ScorePartType.NotationFiles,
                FileCount = notationSectionFiles.Count,
                LastUpdated = notationSectionFiles.Max(x => (DateTime?)x.File.UploadedAt),
                ArrangementFileCounts = CountByArrangement(notationSectionFiles, x => x.Arr.Id),
            });

        // Playback Files
        var playbackSectionFiles = filteredFiles
            .Where(x => PlaybackExtensions.Contains(Path.GetExtension(x.File.FileName)?.ToLowerInvariant() ?? ""))
            .ToList();
        if (playbackSectionFiles.Count > 0)
            sections.Add(new DownloadSectionDto
            {
                Label = "Playback Files",
                ScorePartType = ScorePartType.PlaybackFiles,
                FileCount = playbackSectionFiles.Count,
                LastUpdated = playbackSectionFiles.Max(x => (DateTime?)x.File.UploadedAt),
                ArrangementFileCounts = CountByArrangement(playbackSectionFiles, x => x.Arr.Id),
            });

        return new SeasonPublicDto
        {
            Name = season.Name,
            EnsembleName = season.Ensemble?.Name,
            StartDate = season.StartDate?.ToString("yyyy-MM-dd"),
            EndDate = season.EndDate?.ToString("yyyy-MM-dd"),
            RequiresPassword = false,
            Arrangements = season.Arrangements.Select(a => new ArrangementSummaryDto
            {
                Id = a.Id,
                Name = a.Name,
                Composers = a.Composers.ToList(),
                Arrangers = a.Arrangers.ToList(),
            }).ToList(),
            DownloadSections = sections,
        };
    }

    private static readonly HashSet<string> PdfExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".pdf" };
    private static readonly HashSet<string> NotationExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".xml", ".mxl", ".mscz", ".dorico", ".sib" };
    private static readonly HashSet<string> PlaybackExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".mid", ".midi", ".mp3", ".wav", ".flac", ".ogg" };

    private static bool MatchesTypeConfig(ArrangementFile file, Season season)
    {
        var ext = Path.GetExtension(file.FileName)?.ToLowerInvariant() ?? "";
        if (PdfExtensions.Contains(ext)) return season.ShareIncludePdf;
        if (NotationExtensions.Contains(ext)) return season.ShareIncludeNotation;
        if (PlaybackExtensions.Contains(ext)) return season.ShareIncludePlayback;
        return false;
    }
}
