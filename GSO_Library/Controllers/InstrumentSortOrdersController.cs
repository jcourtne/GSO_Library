using GSO_Library.Dtos;
using GSO_Library.Models;
using GSO_Library.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GSO_Library.Controllers;

[ApiController]
[Route("api/instrument-sort-orders")]
public class InstrumentSortOrdersController : ControllerBase
{
    private readonly InstrumentSortOrderRepository _repo;

    public InstrumentSortOrdersController(InstrumentSortOrderRepository repo)
    {
        _repo = repo;
    }

    [HttpGet]
    [Authorize]
    public async Task<ActionResult<List<InstrumentSortOrder>>> GetAll()
    {
        return Ok(await _repo.GetAllAsync());
    }

    [HttpGet("default")]
    [Authorize]
    public async Task<ActionResult<InstrumentSortOrder>> GetDefault()
    {
        var result = await _repo.GetDefaultAsync();
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [Authorize]
    public async Task<ActionResult<InstrumentSortOrder>> GetById(int id)
    {
        var result = await _repo.GetByIdAsync(id);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpGet("{id:int}/instruments")]
    [Authorize]
    public async Task<ActionResult<List<Instrument>>> GetInstruments(int id)
    {
        var sortOrder = await _repo.GetByIdAsync(id);
        if (sortOrder == null) return NotFound();
        return Ok(await _repo.GetOrderedInstrumentsAsync(id));
    }

    [HttpPost]
    [Authorize(Roles = Roles.Editors)]
    public async Task<ActionResult<InstrumentSortOrder>> Create([FromBody] InstrumentSortOrderRequest request)
    {
        var created = await _repo.CreateAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Editors)]
    public async Task<ActionResult<InstrumentSortOrder>> Update(int id, [FromBody] InstrumentSortOrderRequest request)
    {
        var updated = await _repo.UpdateAsync(id, request);
        if (updated == null) return NotFound();
        return Ok(updated);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Editors)]
    public async Task<IActionResult> Delete(int id)
    {
        var success = await _repo.DeleteAsync(id);
        if (!success) return NotFound();
        return NoContent();
    }
}
