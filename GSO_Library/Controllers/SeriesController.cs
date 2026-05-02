using GSO_Library.Models;
using GSO_Library.Repositories;
using GSO_Library.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GSO_Library.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SeriesController(SeriesRepository seriesRepository, IAuditService auditService) : ControllerBase
{
    [HttpGet]
    [Authorize]
    public async Task<ActionResult<PaginatedResult<Series>>> GetAllSeries(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? sortBy = null, [FromQuery] string? sortDirection = null,
        [FromQuery] string? search = null)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var result = await seriesRepository.GetAllSeriesAsync(page, pageSize, sortBy, sortDirection, search);
        return Ok(result);
    }

    [HttpGet("{id}")]
    [Authorize]
    public async Task<ActionResult<Series>> GetSeriesById(int id)
    {
        var series = await seriesRepository.GetSeriesByIdAsync(id);
        if (series == null)
            return NotFound();

        return Ok(series);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Librarian")]
    public async Task<ActionResult<Series>> AddSeries([FromBody] Series series)
    {
        var now = DateTime.UtcNow;
        series.CreatedAt = now;
        series.UpdatedAt = now;
        series.CreatedBy = User.Identity?.Name;
        var createdSeries = await seriesRepository.AddSeriesAsync(series);
        await auditService.LogAsync(AuditEventType.SeriesCreate, User.Identity?.Name, null, null,
            $"seriesId: {createdSeries.Id} ({createdSeries.Name})");
        return CreatedAtAction(nameof(GetSeriesById), new { id = createdSeries.Id }, createdSeries);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,Librarian")]
    public async Task<ActionResult<Series>> UpdateSeries(int id, [FromBody] Series series)
    {
        series.UpdatedAt = DateTime.UtcNow;
        var updated = await seriesRepository.UpdateSeriesAsync(id, series);
        if (updated == null)
            return NotFound();

        return Ok(updated);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin,Librarian")]
    public async Task<IActionResult> DeleteSeries(int id)
    {
        var series = await seriesRepository.GetSeriesByIdAsync(id);
        if (series == null)
            return NotFound();

        var success = await seriesRepository.DeleteSeriesAsync(id);
        if (!success)
            return NotFound();

        await auditService.LogAsync(AuditEventType.SeriesDelete, User.Identity?.Name, null, null,
            $"seriesId: {id} ({series.Name})");
        return NoContent();
    }
}
