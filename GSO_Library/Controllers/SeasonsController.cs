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
    private readonly IAuditService _auditService;
    private readonly ISeasonZipCacheService _zipCacheService;

    public SeasonsController(SeasonRepository seasonRepository, IAuditService auditService, ISeasonZipCacheService zipCacheService)
    {
        _seasonRepository = seasonRepository;
        _auditService = auditService;
        _zipCacheService = zipCacheService;
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
    [Authorize(Roles = "Admin,Librarian")]
    public async Task<ActionResult<Season>> AddSeason([FromBody] Season season)
    {
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
    [Authorize(Roles = "Admin,Librarian")]
    public async Task<ActionResult<Season>> UpdateSeason(int id, [FromBody] Season season)
    {
        season.UpdatedAt = DateTime.UtcNow;
        var updated = await _seasonRepository.UpdateSeasonAsync(id, season);
        if (updated == null)
            return NotFound();

        await _auditService.LogAsync(AuditEventType.SeasonUpdate, User.Identity?.Name, null, null,
            $"seasonId: {id} ({updated.Name})");
        return Ok(updated);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin,Librarian")]
    public async Task<IActionResult> DeleteSeason(int id)
    {
        var season = await _seasonRepository.GetSeasonByIdAsync(id);
        if (season == null)
            return NotFound();

        await _zipCacheService.InvalidateForSeasonAsync(id);
        var success = await _seasonRepository.DeleteSeasonAsync(id);
        if (!success)
            return NotFound();

        await _auditService.LogAsync(AuditEventType.SeasonDelete, User.Identity?.Name, null, null,
            $"seasonId: {id} ({season.Name})");
        return NoContent();
    }

    [HttpPost("{id}/arrangements/{arrangementId}")]
    [Authorize(Roles = "Admin,Librarian")]
    public async Task<IActionResult> AddArrangement(int id, int arrangementId)
    {
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
    [Authorize(Roles = "Admin,Librarian")]
    public async Task<IActionResult> RemoveArrangement(int id, int arrangementId)
    {
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
    [Authorize(Roles = "Admin,Librarian")]
    public async Task<ActionResult<object>> ConfigureShare(int id, [FromBody] ShareConfigRequest request)
    {
        var season = await _seasonRepository.GetSeasonByIdAsync(id);
        if (season == null) return NotFound();
        string? hash = request.Password is { Length: > 0 }
            ? new PasswordHasher<object>().HashPassword(null!, request.Password)
            : null;
        var token = await _seasonRepository.UpsertShareConfigAsync(
            id, request.IncludePdf, request.IncludeNotation, request.IncludePlayback,
            hash, request.ClearPassword);
        await _zipCacheService.InvalidateForSeasonAsync(id);
        return Ok(new { token });
    }

    [HttpDelete("{id}/share")]
    [Authorize(Roles = "Admin,Librarian")]
    public async Task<IActionResult> RevokeShare(int id)
    {
        var success = await _seasonRepository.RevokeShareTokenAsync(id);
        if (!success) return NotFound();
        await _zipCacheService.InvalidateForSeasonAsync(id);
        return NoContent();
    }

    [HttpPost("{id}/performances/{performanceId}")]
    [Authorize(Roles = "Admin,Librarian")]
    public async Task<IActionResult> AddPerformance(int id, int performanceId)
    {
        var result = await _seasonRepository.AddPerformanceAsync(id, performanceId);
        return result switch
        {
            null => NotFound("Season not found"),
            false => BadRequest("Performance not found or already linked"),
            true => NoContent(),
        };
    }

    [HttpDelete("{id}/performances/{performanceId}")]
    [Authorize(Roles = "Admin,Librarian")]
    public async Task<IActionResult> RemovePerformance(int id, int performanceId)
    {
        var result = await _seasonRepository.RemovePerformanceAsync(id, performanceId);
        return result switch
        {
            null => NotFound("Season not found"),
            false => NotFound("Performance not linked to this season"),
            true => NoContent(),
        };
    }
}
