using System.Security.Claims;
using GSO_Library.Models;
using GSO_Library.Repositories;
using GSO_Library.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace GSO_Library.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EnsemblesController : ControllerBase
{
    private readonly EnsembleRepository _ensembleRepository;
    private readonly IAuditService _auditService;
    private readonly UserManager<ApplicationUser> _userManager;

    public EnsemblesController(EnsembleRepository ensembleRepository, IAuditService auditService, UserManager<ApplicationUser> userManager)
    {
        _ensembleRepository = ensembleRepository;
        _auditService = auditService;
        _userManager = userManager;
    }

    private async Task<bool> IsMemberAsync(int ensembleId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return false;
        var ensembles = await _ensembleRepository.GetEnsemblesForUserAsync(userId);
        return ensembles.Any(e => e.Id == ensembleId);
    }

    [HttpGet]
    [Authorize]
    public async Task<ActionResult<PaginatedResult<Ensemble>>> GetAllEnsembles(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? sortBy = null, [FromQuery] string? sortDirection = null,
        [FromQuery] string? search = null)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var result = await _ensembleRepository.GetAllEnsemblesAsync(page, pageSize, sortBy, sortDirection, search);
        return Ok(result);
    }

    [HttpGet("{id}")]
    [Authorize]
    public async Task<ActionResult<Ensemble>> GetEnsembleById(int id)
    {
        var ensemble = await _ensembleRepository.GetEnsembleByIdAsync(id);
        if (ensemble == null)
            return NotFound();

        return Ok(ensemble);
    }

    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<Ensemble>> AddEnsemble([FromBody] Ensemble ensemble)
    {
        var now = DateTime.UtcNow;
        ensemble.CreatedAt = now;
        ensemble.UpdatedAt = now;
        ensemble.CreatedBy = User.Identity?.Name;
        var created = await _ensembleRepository.AddEnsembleAsync(ensemble);
        await _auditService.LogAsync(AuditEventType.EnsembleCreate, User.Identity?.Name, null, null,
            $"ensembleId: {created.Id} ({created.Name})");
        return CreatedAtAction(nameof(GetEnsembleById), new { id = created.Id }, created);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = Roles.AdminAndEnsembleLibrarian)]
    public async Task<ActionResult<Ensemble>> UpdateEnsemble(int id, [FromBody] Ensemble ensemble)
    {
        // Admins may edit any ensemble; an Ensemble Librarian only their own.
        if (!User.IsInRole(Roles.Admin) && !await IsMemberAsync(id))
            return Forbid();

        ensemble.UpdatedAt = DateTime.UtcNow;
        var updated = await _ensembleRepository.UpdateEnsembleAsync(id, ensemble);
        if (updated == null)
            return NotFound();

        return Ok(updated);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> DeleteEnsemble(int id)
    {
        var ensemble = await _ensembleRepository.GetEnsembleByIdAsync(id);
        if (ensemble == null)
            return NotFound();

        var success = await _ensembleRepository.DeleteEnsembleAsync(id);
        if (!success)
            return NotFound();

        await _auditService.LogAsync(AuditEventType.EnsembleDelete, User.Identity?.Name, null, null,
            $"ensembleId: {id} ({ensemble.Name})");
        return NoContent();
    }

    [HttpGet("{id}/members")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> GetMembers(int id)
    {
        var ensemble = await _ensembleRepository.GetEnsembleByIdAsync(id);
        if (ensemble == null)
            return NotFound();

        var members = await _ensembleRepository.GetEnsembleMembersAsync(id);
        return Ok(members);
    }

    [HttpPost("{id}/members/{userId}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> AddMember(int id, string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return NotFound("User not found.");

        var result = await _ensembleRepository.AddMemberAsync(id, userId);
        if (result == null)
            return NotFound("Ensemble not found.");
        if (result == false)
            return Conflict("User is already a member of this ensemble.");

        await _auditService.LogAsync(AuditEventType.UserEnsembleAdd, User.Identity?.Name, user.UserName, null,
            $"ensembleId: {id}, userId: {userId}");
        return NoContent();
    }

    [HttpDelete("{id}/members/{userId}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> RemoveMember(int id, string userId)
    {
        var result = await _ensembleRepository.RemoveMemberAsync(id, userId);
        if (result == null)
            return NotFound("Ensemble not found.");
        if (result == false)
            return NotFound("User is not a member of this ensemble.");

        var user = await _userManager.FindByIdAsync(userId);
        await _auditService.LogAsync(AuditEventType.UserEnsembleRemove, User.Identity?.Name, user?.UserName, null,
            $"ensembleId: {id}, userId: {userId}");
        return NoContent();
    }
}
