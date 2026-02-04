using GSO_Library.Models;
using GSO_Library.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GSO_Library.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InstrumentsController : ControllerBase
{
    private readonly InstrumentRepository _instrumentRepository;

    public InstrumentsController(InstrumentRepository instrumentRepository)
    {
        _instrumentRepository = instrumentRepository;
    }

    [HttpGet]
    [Authorize]
    public async Task<ActionResult<PaginatedResult<Instrument>>> GetAllInstruments([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var result = await _instrumentRepository.GetAllInstrumentsAsync(page, pageSize);
        return Ok(result);
    }

    [HttpGet("{id}")]
    [Authorize]
    public async Task<ActionResult<Instrument>> GetInstrumentById(int id)
    {
        var instrument = await _instrumentRepository.GetInstrumentByIdAsync(id);
        if (instrument == null)
            return NotFound();

        return Ok(instrument);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<ActionResult<Instrument>> AddInstrument([FromBody] Instrument instrument)
    {
        var created = await _instrumentRepository.AddInstrumentAsync(instrument);
        return CreatedAtAction(nameof(GetInstrumentById), new { id = created.Id }, created);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<ActionResult<Instrument>> UpdateInstrument(int id, [FromBody] Instrument instrument)
    {
        var updated = await _instrumentRepository.UpdateInstrumentAsync(id, instrument);
        if (updated == null)
            return NotFound();

        return Ok(updated);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<IActionResult> DeleteInstrument(int id)
    {
        var success = await _instrumentRepository.DeleteInstrumentAsync(id);
        if (!success)
            return NotFound();

        return NoContent();
    }
}
