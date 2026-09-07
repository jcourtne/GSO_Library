using GSO_Library.Models;
using GSO_Library.Repositories;
using GSO_Library.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GSO_Library.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GamesController(GameRepository gameRepository, IAuditService auditService) : ControllerBase
{
    [HttpGet]
    [Authorize]
    public async Task<ActionResult<PaginatedResult<Game>>> GetAllGames(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? sortBy = null, [FromQuery] string? sortDirection = null,
        [FromQuery] string? search = null, [FromQuery] int[]? seriesIds = null)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var result = await gameRepository.GetAllGamesAsync(page, pageSize, sortBy, sortDirection, search, seriesIds);
        return Ok(result);
    }

    [HttpGet("{id}")]
    [Authorize]
    public async Task<ActionResult<Game>> GetGameById(int id)
    {
        var game = await gameRepository.GetGameByIdAsync(id);
        if (game == null)
            return NotFound();

        return Ok(game);
    }

    [HttpPost]
    [Authorize(Roles = Roles.EditorsAndEnsembleLibrarian)]
    public async Task<ActionResult<Game>> AddGame([FromBody] Game game)
    {
        var now = DateTime.UtcNow;
        game.CreatedAt = now;
        game.UpdatedAt = now;
        game.CreatedBy = User.Identity?.Name;
        var createdGame = await gameRepository.AddGameAsync(game);
        await auditService.LogAsync(AuditEventType.GameCreate, User.Identity?.Name, null, null,
            $"gameId: {createdGame.Id} ({createdGame.Name})");
        return CreatedAtAction(nameof(GetGameById), new { id = createdGame.Id }, createdGame);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = Roles.EditorsAndEnsembleLibrarian)]
    public async Task<ActionResult<Game>> UpdateGame(int id, [FromBody] Game game)
    {
        game.UpdatedAt = DateTime.UtcNow;
        var updated = await gameRepository.UpdateGameAsync(id, game);
        if (updated == null)
            return NotFound();

        return Ok(updated);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = Roles.EditorsAndEnsembleLibrarian)]
    public async Task<IActionResult> DeleteGame(int id)
    {
        var game = await gameRepository.GetGameByIdAsync(id);
        if (game == null)
            return NotFound();

        var success = await gameRepository.DeleteGameAsync(id);
        if (!success)
            return NotFound();

        await auditService.LogAsync(AuditEventType.GameDelete, User.Identity?.Name, null, null,
            $"gameId: {id} ({game.Name})");
        return NoContent();
    }
}
