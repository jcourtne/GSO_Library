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
public class PublicController(SeasonRepository seasonRepo, ISeasonZipCacheService zipCache) : ControllerBase
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

        return Ok(BuildDto(season));
    }

    [HttpGet("seasons/{token}/download")]
    public async Task<IActionResult> Download(string token,
        [FromQuery] string? scorePartType = null, [FromQuery] int? instrumentId = null)
    {
        if (scorePartType != null && !ScorePartType.All.Contains(scorePartType))
            return BadRequest("Invalid scorePartType");

        var season = await seasonRepo.GetSeasonByShareTokenAsync(token);
        if (season == null) return NotFound();

        var pw = Request.Headers["X-Share-Password"].FirstOrDefault();
        if (!ValidatePassword(season, pw)) return Unauthorized();

        var zipKey = zipCache.BuildZipKey(scorePartType, instrumentId);
        var (stream, zipFileName) = await zipCache.GetOrGenerateAsync(season, zipKey, scorePartType, instrumentId);
        return File(stream, "application/zip", zipFileName);
    }

    private static SeasonPublicDto BuildDto(Season season)
    {
        var validInstrumentIds = new HashSet<int>(
            season.Arrangements.SelectMany(a => a.Instruments.Select(i => i.Id)));

        var filteredFiles = season.Arrangements
            .SelectMany(a => a.Files.Select(f => (Arr: a, File: f)))
            .Where(x => MatchesTypeConfig(x.File, season))
            .ToList();

        var sections = new List<DownloadSectionDto>();

        // Conductor
        var conductorFiles = filteredFiles.Where(x => x.File.ScorePartType == ScorePartType.ConductorScore).ToList();
        if (conductorFiles.Count > 0)
            sections.Add(new DownloadSectionDto
            {
                Label = "Conductor's Score",
                ScorePartType = ScorePartType.ConductorScore,
                FileCount = conductorFiles.Count,
                LastUpdated = conductorFiles.Max(x => (DateTime?)x.File.UploadedAt),
            });

        // Per-instrument (alphabetical)
        var instrumentMap = new Dictionary<int, string>();
        foreach (var arr in season.Arrangements)
            foreach (var inst in arr.Instruments)
                instrumentMap.TryAdd(inst.Id, inst.Name);

        var instrumentGroups = filteredFiles
            .Where(x => x.File.ScorePartType == ScorePartType.InstrumentPart &&
                        x.File.InstrumentId.HasValue &&
                        validInstrumentIds.Contains(x.File.InstrumentId.Value))
            .GroupBy(x => x.File.InstrumentId!.Value)
            .OrderBy(g => instrumentMap.GetValueOrDefault(g.Key, ""));

        foreach (var group in instrumentGroups)
        {
            sections.Add(new DownloadSectionDto
            {
                Label = instrumentMap.GetValueOrDefault(group.Key, $"Instrument {group.Key}"),
                ScorePartType = ScorePartType.InstrumentPart,
                InstrumentId = group.Key,
                FileCount = group.Count(),
                LastUpdated = group.Max(x => (DateTime?)x.File.UploadedAt),
            });
        }

        // Percussion
        var percussionFiles = filteredFiles.Where(x => x.File.ScorePartType == ScorePartType.PercussionPart).ToList();
        if (percussionFiles.Count > 0)
            sections.Add(new DownloadSectionDto
            {
                Label = "Percussion (Generic)",
                ScorePartType = ScorePartType.PercussionPart,
                FileCount = percussionFiles.Count,
                LastUpdated = percussionFiles.Max(x => (DateTime?)x.File.UploadedAt),
            });

        // Unlisted
        var unlistedFiles = filteredFiles.Where(x =>
            x.File.ScorePartType == null ||
            x.File.ScorePartType == ScorePartType.UnlistedPart ||
            (x.File.ScorePartType == ScorePartType.InstrumentPart &&
             (!x.File.InstrumentId.HasValue || !validInstrumentIds.Contains(x.File.InstrumentId.Value))))
            .ToList();
        if (unlistedFiles.Count > 0)
            sections.Add(new DownloadSectionDto
            {
                Label = "Unlisted Parts",
                ScorePartType = ScorePartType.UnlistedPart,
                FileCount = unlistedFiles.Count,
                LastUpdated = unlistedFiles.Max(x => (DateTime?)x.File.UploadedAt),
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
