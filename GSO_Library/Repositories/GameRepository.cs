using GSO_Library.Data;
using GSO_Library.Models;

namespace GSO_Library.Repositories;

public class GameRepository
{
    private readonly GSOLibraryContext _context;

    public GameRepository(GSOLibraryContext context)
    {
        _context = context;
    }

    public async Task<Game> AddGameAsync(Game game)
    {
        _context.Games.Add(game);
        await _context.SaveChangesAsync();
        return game;
    }

    public async Task DeleteGameAsync(int id)
    {
        var game = await _context.Games.FindAsync(id);
        if (game != null)
        {
            _context.Games.Remove(game);
            await _context.SaveChangesAsync();
        }
    }
}
