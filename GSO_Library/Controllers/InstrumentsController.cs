using GSO_Library.Models;
using GSO_Library.Repositories;
using GSO_Library.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GSO_Library.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InstrumentsController(InstrumentRepository instrumentRepository, IAuditService auditService) : ControllerBase
{
    [HttpGet]
    [Authorize]
    public async Task<ActionResult<PaginatedResult<Instrument>>> GetAllInstruments(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? sortBy = null, [FromQuery] string? sortDirection = null,
        [FromQuery] string? search = null)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var result = await instrumentRepository.GetAllInstrumentsAsync(page, pageSize, sortBy, sortDirection, search);
        return Ok(result);
    }

    [HttpGet("{id}")]
    [Authorize]
    public async Task<ActionResult<Instrument>> GetInstrumentById(int id)
    {
        var instrument = await instrumentRepository.GetInstrumentByIdAsync(id);
        if (instrument == null)
            return NotFound();

        return Ok(instrument);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Librarian")]
    public async Task<ActionResult<Instrument>> AddInstrument([FromBody] Instrument instrument)
    {
        var now = DateTime.UtcNow;
        instrument.CreatedAt = now;
        instrument.UpdatedAt = now;
        instrument.CreatedBy = User.Identity?.Name;
        var created = await instrumentRepository.AddInstrumentAsync(instrument);
        await auditService.LogAsync(AuditEventType.InstrumentCreate, User.Identity?.Name, null, null,
            $"instrumentId: {created.Id} ({created.Name})");
        return CreatedAtAction(nameof(GetInstrumentById), new { id = created.Id }, created);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,Librarian")]
    public async Task<ActionResult<Instrument>> UpdateInstrument(int id, [FromBody] Instrument instrument)
    {
        instrument.UpdatedAt = DateTime.UtcNow;
        var updated = await instrumentRepository.UpdateInstrumentAsync(id, instrument);
        if (updated == null)
            return NotFound();

        return Ok(updated);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin,Librarian")]
    public async Task<IActionResult> DeleteInstrument(int id)
    {
        var instrument = await instrumentRepository.GetInstrumentByIdAsync(id);
        if (instrument == null)
            return NotFound();

        var success = await instrumentRepository.DeleteInstrumentAsync(id);
        if (!success)
            return NotFound();

        await auditService.LogAsync(AuditEventType.InstrumentDelete, User.Identity?.Name, null, null,
            $"instrumentId: {id} ({instrument.Name})");
        return NoContent();
    }
}
