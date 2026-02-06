using GSO_Library.Models;
using GSO_Library.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GSO_Library.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EnsemblesController : ControllerBase
{
    private readonly EnsembleRepository _ensembleRepository;

    public EnsemblesController(EnsembleRepository ensembleRepository)
    {
        _ensembleRepository = ensembleRepository;
    }

    [HttpGet]
    [Authorize]
    public async Task<ActionResult<PaginatedResult<Ensemble>>> GetAllEnsembles(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? sortBy = null, [FromQuery] string? sortDirection = null)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var result = await _ensembleRepository.GetAllEnsemblesAsync(page, pageSize, sortBy, sortDirection);
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
    [Authorize(Roles = "Admin,Editor")]
    public async Task<ActionResult<Ensemble>> AddEnsemble([FromBody] Ensemble ensemble)
    {
        var now = DateTime.UtcNow;
        ensemble.CreatedAt = now;
        ensemble.UpdatedAt = now;
        ensemble.CreatedBy = User.Identity?.Name;
        var created = await _ensembleRepository.AddEnsembleAsync(ensemble);
        return CreatedAtAction(nameof(GetEnsembleById), new { id = created.Id }, created);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<ActionResult<Ensemble>> UpdateEnsemble(int id, [FromBody] Ensemble ensemble)
    {
        ensemble.UpdatedAt = DateTime.UtcNow;
        var updated = await _ensembleRepository.UpdateEnsembleAsync(id, ensemble);
        if (updated == null)
            return NotFound();

        return Ok(updated);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<IActionResult> DeleteEnsemble(int id)
    {
        var success = await _ensembleRepository.DeleteEnsembleAsync(id);
        if (!success)
            return NotFound();

        return NoContent();
    }
}
