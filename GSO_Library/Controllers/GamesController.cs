using GSO_Library.Models;
using GSO_Library.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GSO_Library.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GamesController : ControllerBase
{
    private readonly GameRepository _gameRepository;

    public GamesController(GameRepository gameRepository)
    {
        _gameRepository = gameRepository;
    }

    [HttpGet]
    [Authorize]
    public async Task<ActionResult<PaginatedResult<Game>>> GetAllGames(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? sortBy = null, [FromQuery] string? sortDirection = null,
        [FromQuery] string? search = null)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var result = await _gameRepository.GetAllGamesAsync(page, pageSize, sortBy, sortDirection, search);
        return Ok(result);
    }

    [HttpGet("{id}")]
    [Authorize]
    public async Task<ActionResult<Game>> GetGameById(int id)
    {
        var game = await _gameRepository.GetGameByIdAsync(id);
        if (game == null)
            return NotFound();

        return Ok(game);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<ActionResult<Game>> AddGame([FromBody] Game game)
    {
        var now = DateTime.UtcNow;
        game.CreatedAt = now;
        game.UpdatedAt = now;
        game.CreatedBy = User.Identity?.Name;
        var createdGame = await _gameRepository.AddGameAsync(game);
        return CreatedAtAction(nameof(GetGameById), new { id = createdGame.Id }, createdGame);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<ActionResult<Game>> UpdateGame(int id, [FromBody] Game game)
    {
        game.UpdatedAt = DateTime.UtcNow;
        var updated = await _gameRepository.UpdateGameAsync(id, game);
        if (updated == null)
            return NotFound();

        return Ok(updated);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<IActionResult> DeleteGame(int id)
    {
        var success = await _gameRepository.DeleteGameAsync(id);
        if (!success)
            return NotFound();

        return NoContent();
    }
}
