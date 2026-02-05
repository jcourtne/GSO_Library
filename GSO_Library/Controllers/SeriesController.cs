using GSO_Library.Models;
using GSO_Library.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GSO_Library.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SeriesController : ControllerBase
{
    private readonly SeriesRepository _seriesRepository;

    public SeriesController(SeriesRepository seriesRepository)
    {
        _seriesRepository = seriesRepository;
    }

    [HttpGet]
    [Authorize]
    public async Task<ActionResult<PaginatedResult<Series>>> GetAllSeries(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? sortBy = null, [FromQuery] string? sortDirection = null)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var result = await _seriesRepository.GetAllSeriesAsync(page, pageSize, sortBy, sortDirection);
        return Ok(result);
    }

    [HttpGet("{id}")]
    [Authorize]
    public async Task<ActionResult<Series>> GetSeriesById(int id)
    {
        var series = await _seriesRepository.GetSeriesByIdAsync(id);
        if (series == null)
            return NotFound();

        return Ok(series);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<ActionResult<Series>> AddSeries([FromBody] Series series)
    {
        var now = DateTime.UtcNow;
        series.CreatedAt = now;
        series.UpdatedAt = now;
        series.CreatedBy = User.Identity?.Name;
        var createdSeries = await _seriesRepository.AddSeriesAsync(series);
        return CreatedAtAction(nameof(GetSeriesById), new { id = createdSeries.Id }, createdSeries);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<ActionResult<Series>> UpdateSeries(int id, [FromBody] Series series)
    {
        series.UpdatedAt = DateTime.UtcNow;
        var updated = await _seriesRepository.UpdateSeriesAsync(id, series);
        if (updated == null)
            return NotFound();

        return Ok(updated);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<IActionResult> DeleteSeries(int id)
    {
        var success = await _seriesRepository.DeleteSeriesAsync(id);
        if (!success)
            return NotFound();

        return NoContent();
    }
}
