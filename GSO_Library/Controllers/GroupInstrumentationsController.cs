using GSO_Library.Models;
using GSO_Library.Repositories;
using GSO_Library.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GSO_Library.Controllers;

[ApiController]
[Route("api/group-instrumentations")]
public class GroupInstrumentationsController(GroupInstrumentationRepository groupInstrumentationRepository, IAuditService auditService) : ControllerBase
{
    [HttpGet]
    [Authorize]
    public async Task<ActionResult<PaginatedResult<GroupInstrumentation>>> GetAllGroupInstrumentations(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? sortBy = null, [FromQuery] string? sortDirection = null,
        [FromQuery] string? search = null)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var result = await groupInstrumentationRepository.GetAllGroupInstrumentationsAsync(page, pageSize, sortBy, sortDirection, search);
        return Ok(result);
    }

    [HttpGet("{id}")]
    [Authorize]
    public async Task<ActionResult<GroupInstrumentation>> GetGroupInstrumentationById(int id)
    {
        var groupInstrumentation = await groupInstrumentationRepository.GetGroupInstrumentationByIdAsync(id);
        if (groupInstrumentation == null)
            return NotFound();

        return Ok(groupInstrumentation);
    }

    [HttpPost]
    [Authorize(Roles = Roles.EditorsAndEnsembleLibrarian)]
    public async Task<ActionResult<GroupInstrumentation>> AddGroupInstrumentation([FromBody] GroupInstrumentation groupInstrumentation)
    {
        var now = DateTime.UtcNow;
        groupInstrumentation.CreatedAt = now;
        groupInstrumentation.UpdatedAt = now;
        groupInstrumentation.CreatedBy = User.Identity?.Name;
        var (created, duplicate) = await groupInstrumentationRepository.AddGroupInstrumentationAsync(groupInstrumentation);
        if (duplicate)
            return Conflict($"A group instrumentation named '{groupInstrumentation.Name}' already exists.");
        await auditService.LogAsync(AuditEventType.GroupInstrumentationCreate, User.Identity?.Name, null, null,
            $"groupInstrumentationId: {created!.Id} ({created.Name})");
        return CreatedAtAction(nameof(GetGroupInstrumentationById), new { id = created.Id }, created);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = Roles.EditorsAndEnsembleLibrarian)]
    public async Task<ActionResult<GroupInstrumentation>> UpdateGroupInstrumentation(int id, [FromBody] GroupInstrumentation groupInstrumentation)
    {
        groupInstrumentation.UpdatedAt = DateTime.UtcNow;
        var (updated, duplicate) = await groupInstrumentationRepository.UpdateGroupInstrumentationAsync(id, groupInstrumentation);
        if (duplicate)
            return Conflict($"A group instrumentation named '{groupInstrumentation.Name}' already exists.");
        if (updated == null)
            return NotFound();

        return Ok(updated);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = Roles.EditorsAndEnsembleLibrarian)]
    public async Task<IActionResult> DeleteGroupInstrumentation(int id)
    {
        var groupInstrumentation = await groupInstrumentationRepository.GetGroupInstrumentationByIdAsync(id);
        if (groupInstrumentation == null)
            return NotFound();

        var success = await groupInstrumentationRepository.DeleteGroupInstrumentationAsync(id);
        if (!success)
            return NotFound();

        await auditService.LogAsync(AuditEventType.GroupInstrumentationDelete, User.Identity?.Name, null, null,
            $"groupInstrumentationId: {id} ({groupInstrumentation.Name})");
        return NoContent();
    }
}
