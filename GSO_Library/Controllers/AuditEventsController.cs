using GSO_Library.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GSO_Library.Controllers;

[ApiController]
[Route("api/audit-events")]
[Authorize(Roles = "Admin")]
public class AuditEventsController(AuditEventRepository repository) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string[]? eventTypes,
        [FromQuery] string[]? usernames,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
        => Ok(await repository.GetAllAsync(eventTypes, usernames, from, to));

    [HttpGet("usernames")]
    public async Task<IActionResult> GetUsernames()
        => Ok(await repository.GetDistinctUsernamesAsync());
}
