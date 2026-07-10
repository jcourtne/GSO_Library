using GSO_Library.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GSO_Library.Controllers;

[ApiController]
[Route("api/instrument-families")]
[Authorize]
public class InstrumentFamiliesController : ControllerBase
{
    private readonly InstrumentFamilyRepository _repo;

    public InstrumentFamiliesController(InstrumentFamilyRepository repo)
    {
        _repo = repo;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var families = await _repo.GetAllAsync();
        return Ok(families);
    }
}
