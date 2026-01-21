using GSO_Library.Models;
using GSO_Library.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GSO_Library.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ArrangementsController : ControllerBase
{
    private readonly ArrangementRepository _arrangementRepository;

    public ArrangementsController(ArrangementRepository arrangementRepository)
    {
        _arrangementRepository = arrangementRepository;
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<ActionResult<Arrangement>> AddArrangement([FromBody] Arrangement arrangement)
    {
        var createdArrangement = await _arrangementRepository.AddArrangementAsync(arrangement);
        return CreatedAtAction(nameof(GetArrangementById), new { id = createdArrangement.Id }, createdArrangement);
    }

    [HttpGet("{id}")]
    [Authorize]
    public async Task<ActionResult<Arrangement>> GetArrangementById(int id)
    {
        var arrangement = await _arrangementRepository.GetArrangementByIdAsync(id);
        if (arrangement == null)
            return NotFound();

        return Ok(arrangement);
    }

    [HttpGet]
    [Authorize]
    public async Task<ActionResult<IEnumerable<Arrangement>>> GetAllArrangements([FromQuery] int? gameId, [FromQuery] int? seriesId)
    {
        IEnumerable<Arrangement> arrangements;

        if (gameId.HasValue)
        {
            arrangements = await _arrangementRepository.GetArrangementsByGameIdAsync(gameId.Value);
        }
        else if (seriesId.HasValue)
        {
            arrangements = await _arrangementRepository.GetArrangementsBySeriesIdAsync(seriesId.Value);
        }
        else
        {
            arrangements = await _arrangementRepository.GetAllArrangementsAsync();
        }

        return Ok(arrangements);
    }

    [HttpPost("{arrangementId}/performances")]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<ActionResult<Performance>> AddPerformance(int arrangementId, [FromBody] Performance performance)
    {
        var createdPerformance = await _arrangementRepository.AddPerformanceAsync(arrangementId, performance);
        if (createdPerformance == null)
            return NotFound("Arrangement not found");

        return CreatedAtAction(nameof(AddPerformance), new { arrangementId, performanceId = createdPerformance.Id }, createdPerformance);
    }

    [HttpDelete("{arrangementId}/performances/{performanceId}")]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<IActionResult> RemovePerformance(int arrangementId, int performanceId)
    {
        var success = await _arrangementRepository.RemovePerformanceAsync(arrangementId, performanceId);
        if (!success)
            return NotFound("Performance not found or does not belong to this arrangement");

        return NoContent();
    }
}
