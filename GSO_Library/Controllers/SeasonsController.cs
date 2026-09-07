using System.Security.Claims;
using GSO_Library.Dtos;
using GSO_Library.Models;
using GSO_Library.Repositories;
using GSO_Library.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace GSO_Library.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SeasonsController : ControllerBase
{
    private readonly SeasonRepository _seasonRepository;
    private readonly ArrangementRepository _arrangementRepository;
    private readonly EnsembleRepository _ensembleRepository;
    private readonly IAuditService _auditService;
    private readonly ISeasonZipCacheService _zipCacheService;
    private readonly ISeasonZipWarmupQueue _zipWarmupQueue;

    public SeasonsController(SeasonRepository seasonRepository, ArrangementRepository arrangementRepository,
        EnsembleRepository ensembleRepository, IAuditService auditService, ISeasonZipCacheService zipCacheService,
        ISeasonZipWarmupQueue zipWarmupQueue)
    {
        _seasonRepository = seasonRepository;
        _arrangementRepository = arrangementRepository;
        _ensembleRepository = ensembleRepository;
        _auditService = auditService;
        _zipCacheService = zipCacheService;
        _zipWarmupQueue = zipWarmupQueue;
    }

    // Admin/Librarian may write any season. An Ensemble Librarian may only write seasons
    // tied to one of their own ensembles.
    private async Task<bool> CanWriteForEnsembleAsync(int ensembleId)
    {
        if (User.IsInRole(Roles.Admin) || User.IsInRole(Roles.Librarian))
            return true;
        if (!User.IsInRole(Roles.EnsembleLibrarian))
            return false;
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return false;
        var ensembles = await _ensembleRepository.GetEnsemblesForUserAsync(userId);
        return ensembles.Any(e => e.Id == ensembleId);
    }

    // Loads the season and checks ensemble-scoped write access. Returns the season on success,
    // or an error result (404/403) to return directly.
    private async Task<(Season? season, ActionResult? error)> LoadSeasonForWriteAsync(int id)
    {
        var season = await _seasonRepository.GetSeasonByIdAsync(id);
        if (season == null)
            return (null, NotFound());
        if (!await CanWriteForEnsembleAsync(season.EnsembleId))
            return (null, Forbid());
        return (season, null);
    }

    [HttpGet]
    [Authorize]
    public async Task<ActionResult<PaginatedResult<Season>>> GetAllSeasons(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? sortBy = null, [FromQuery] string? sortDirection = null,
        [FromQuery] string? search = null, [FromQuery] int[]? ensembleIds = null,
        [FromQuery] DateTime? dateFrom = null, [FromQuery] DateTime? dateTo = null)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var result = await _seasonRepository.GetAllSeasonsAsync(page, pageSize, sortBy, sortDirection, search, ensembleIds, dateFrom, dateTo);
        return Ok(result);
    }

    [HttpGet("{id}")]
    [Authorize]
    public async Task<ActionResult<Season>> GetSeasonById(int id)
    {
        var season = await _seasonRepository.GetSeasonByIdAsync(id);
        if (season == null)
            return NotFound();

        return Ok(season);
    }

    [HttpPost]
    [Authorize(Roles = Roles.EditorsAndEnsembleLibrarian)]
    public async Task<ActionResult<Season>> AddSeason([FromBody] Season season)
    {
        if (!await CanWriteForEnsembleAsync(season.EnsembleId))
            return Forbid();

        var now = DateTime.UtcNow;
        season.CreatedAt = now;
        season.UpdatedAt = now;
        season.CreatedBy = User.Identity?.Name;
        var created = await _seasonRepository.AddSeasonAsync(season);
        await _auditService.LogAsync(AuditEventType.SeasonCreate, User.Identity?.Name, null, null,
            $"seasonId: {created.Id} ({created.Name})");
        return CreatedAtAction(nameof(GetSeasonById), new { id = created.Id }, created);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = Roles.EditorsAndEnsembleLibrarian)]
    public async Task<ActionResult<Season>> UpdateSeason(int id, [FromBody] Season season)
    {
        var (_, error) = await LoadSeasonForWriteAsync(id);
        if (error != null) return error;
        // Prevent moving a season into an ensemble the user doesn't belong to.
        if (!await CanWriteForEnsembleAsync(season.EnsembleId))
            return Forbid();

        season.UpdatedAt = DateTime.UtcNow;
        var updated = await _seasonRepository.UpdateSeasonAsync(id, season);
        if (updated == null)
            return NotFound();

        // The ensemble may have changed, which prunes mismatched arrangements and changes
        // the zip contents.
        await _zipCacheService.InvalidateForSeasonAsync(id);

        await _auditService.LogAsync(AuditEventType.SeasonUpdate, User.Identity?.Name, null, null,
            $"seasonId: {id} ({updated.Name})");
        return Ok(updated);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = Roles.EditorsAndEnsembleLibrarian)]
    public async Task<IActionResult> DeleteSeason(int id)
    {
        var (season, error) = await LoadSeasonForWriteAsync(id);
        if (error != null) return error;

        await _zipCacheService.InvalidateForSeasonAsync(id);
        var success = await _seasonRepository.DeleteSeasonAsync(id);
        if (!success)
            return NotFound();

        await _auditService.LogAsync(AuditEventType.SeasonDelete, User.Identity?.Name, null, null,
            $"seasonId: {id} ({season!.Name})");
        return NoContent();
    }

    [HttpPost("{id}/arrangements/{arrangementId}")]
    [Authorize(Roles = Roles.EditorsAndEnsembleLibrarian)]
    public async Task<IActionResult> AddArrangement(int id, int arrangementId)
    {
        var (season, error) = await LoadSeasonForWriteAsync(id);
        if (error != null) return error;

        // A season may only contain arrangements its ensemble is allowed to download: either a
        // public arrangement (no ensemble) or one linked to this season's own ensemble. This keeps
        // the season zip / public share from exposing file types the season's ensemble members
        // (and Ensemble Librarians) would not otherwise be able to download.
        var arrangement = await _arrangementRepository.GetArrangementByIdAsync(arrangementId);
        if (arrangement == null)
            return BadRequest("Arrangement not found or already linked");
        if (!arrangement.IsPublic && !arrangement.Ensembles.Any(e => e.Id == season!.EnsembleId))
            return Forbid();

        var result = await _seasonRepository.AddArrangementAsync(id, arrangementId);
        if (result == true) await _zipCacheService.InvalidateForSeasonAsync(id);
        return result switch
        {
            null => NotFound("Season not found"),
            false => BadRequest("Arrangement not found or already linked"),
            true => NoContent(),
        };
    }

    [HttpDelete("{id}/arrangements/{arrangementId}")]
    [Authorize(Roles = Roles.EditorsAndEnsembleLibrarian)]
    public async Task<IActionResult> RemoveArrangement(int id, int arrangementId)
    {
        var (_, error) = await LoadSeasonForWriteAsync(id);
        if (error != null) return error;

        var result = await _seasonRepository.RemoveArrangementAsync(id, arrangementId);
        if (result == true) await _zipCacheService.InvalidateForSeasonAsync(id);
        return result switch
        {
            null => NotFound("Season not found"),
            false => NotFound("Arrangement not linked to this season"),
            true => NoContent(),
        };
    }

    [HttpPost("{id}/share")]
    [Authorize(Roles = Roles.EditorsAndEnsembleLibrarian)]
    public async Task<ActionResult<object>> ConfigureShare(int id, [FromBody] ShareConfigRequest request)
    {
        var (_, error) = await LoadSeasonForWriteAsync(id);
        if (error != null) return error;

        string? hash = request.Password is { Length: > 0 }
            ? new PasswordHasher<object>().HashPassword(null!, request.Password)
            : null;
        var token = await _seasonRepository.UpsertShareConfigAsync(
            id, request.IncludePdf, request.IncludeNotation, request.IncludePlayback,
            hash, request.ClearPassword);
        await _zipCacheService.InvalidateForSeasonAsync(id);
        // Pre-build the "download all" zip in the background so the first public visitor
        // gets a cache hit. Enqueue strictly after invalidation so it isn't deleted.
        _zipWarmupQueue.Enqueue(new SeasonZipWarmupRequest(id));
        return Ok(new { token });
    }

    [HttpDelete("{id}/share")]
    [Authorize(Roles = Roles.EditorsAndEnsembleLibrarian)]
    public async Task<IActionResult> RevokeShare(int id)
    {
        var (_, error) = await LoadSeasonForWriteAsync(id);
        if (error != null) return error;

        var success = await _seasonRepository.RevokeShareTokenAsync(id);
        if (!success) return NotFound();
        await _zipCacheService.InvalidateForSeasonAsync(id);
        return NoContent();
    }

    [HttpPost("{id}/performances/{performanceId}")]
    [Authorize(Roles = Roles.EditorsAndEnsembleLibrarian)]
    public async Task<IActionResult> AddPerformance(int id, int performanceId)
    {
        var (_, error) = await LoadSeasonForWriteAsync(id);
        if (error != null) return error;

        var result = await _seasonRepository.AddPerformanceAsync(id, performanceId);
        return result switch
        {
            null => NotFound("Season not found"),
            false => BadRequest("Performance not found or already linked"),
            true => NoContent(),
        };
    }

    [HttpDelete("{id}/performances/{performanceId}")]
    [Authorize(Roles = Roles.EditorsAndEnsembleLibrarian)]
    public async Task<IActionResult> RemovePerformance(int id, int performanceId)
    {
        var (_, error) = await LoadSeasonForWriteAsync(id);
        if (error != null) return error;

        var result = await _seasonRepository.RemovePerformanceAsync(id, performanceId);
        return result switch
        {
            null => NotFound("Season not found"),
            false => NotFound("Performance not linked to this season"),
            true => NoContent(),
        };
    }
}
