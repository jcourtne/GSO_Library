using GSO_Library.Models;
using GSO_Library.Repositories;
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

    [HttpPost]
    public async Task<ActionResult<Game>> AddGame([FromBody] Game game)
    {
        var createdGame = await _gameRepository.AddGameAsync(game);
        return CreatedAtAction(nameof(AddGame), new { id = createdGame.Id }, createdGame);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteGame(int id)
    {
        await _gameRepository.DeleteGameAsync(id);
        return NoContent();
    }
}
