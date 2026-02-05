using GSO_Library.Models;
using GSO_Library.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GSO_Library.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PerformancesController : ControllerBase
{
    private readonly PerformanceRepository _performanceRepository;

    public PerformancesController(PerformanceRepository performanceRepository)
    {
        _performanceRepository = performanceRepository;
    }

    [HttpGet]
    [Authorize]
    public async Task<ActionResult<PaginatedResult<Performance>>> GetAllPerformances(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? sortBy = null, [FromQuery] string? sortDirection = null)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var result = await _performanceRepository.GetAllPerformancesAsync(page, pageSize, sortBy, sortDirection);
        return Ok(result);
    }

    [HttpGet("{id}")]
    [Authorize]
    public async Task<ActionResult<Performance>> GetPerformanceById(int id)
    {
        var performance = await _performanceRepository.GetPerformanceByIdAsync(id);
        if (performance == null)
            return NotFound();

        return Ok(performance);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<ActionResult<Performance>> AddPerformance([FromBody] Performance performance)
    {
        var now = DateTime.UtcNow;
        performance.CreatedAt = now;
        performance.UpdatedAt = now;
        performance.CreatedBy = User.Identity?.Name;
        var created = await _performanceRepository.AddPerformanceAsync(performance);
        return CreatedAtAction(nameof(GetPerformanceById), new { id = created.Id }, created);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<ActionResult<Performance>> UpdatePerformance(int id, [FromBody] Performance performance)
    {
        performance.UpdatedAt = DateTime.UtcNow;
        var updated = await _performanceRepository.UpdatePerformanceAsync(id, performance);
        if (updated == null)
            return NotFound();

        return Ok(updated);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<IActionResult> DeletePerformance(int id)
    {
        var success = await _performanceRepository.DeletePerformanceAsync(id);
        if (!success)
            return NotFound();

        return NoContent();
    }
}
