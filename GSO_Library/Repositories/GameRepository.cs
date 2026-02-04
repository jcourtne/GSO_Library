using GSO_Library.Data;
using GSO_Library.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace GSO_Library.Repositories;

public class GameRepository
{
    private readonly GSOLibraryContext _context;
    private readonly IMemoryCache _cache;

    public GameRepository(GSOLibraryContext context, IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<IEnumerable<Game>> GetAllGamesAsync()
    {
        return await _context.Games
            .Include(g => g.Series)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<Game?> GetGameByIdAsync(int id)
    {
        return await _context.Games
            .Include(g => g.Series)
            .Include(g => g.Arrangements)
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == id);
    }

    public async Task<PaginatedResult<Game>> GetAllGamesAsync(int page, int pageSize)
    {
        var query = _context.Games
            .Include(g => g.Series)
            .AsNoTracking();
        var totalCount = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return new PaginatedResult<Game> { Items = items, Page = page, PageSize = pageSize, TotalCount = totalCount };
    }

    public async Task<Game> AddGameAsync(Game game)
    {
        _context.Games.Add(game);
        await _context.SaveChangesAsync();
        InvalidateArrangementCache();
        return game;
    }

    public async Task<Game?> UpdateGameAsync(int id, Game game)
    {
        var existing = await _context.Games.FindAsync(id);
        if (existing == null)
            return null;

        existing.Name = game.Name;
        existing.Description = game.Description;
        existing.SeriesId = game.SeriesId;
        await _context.SaveChangesAsync();
        InvalidateArrangementCache();
        return existing;
    }

    public async Task<bool> DeleteGameAsync(int id)
    {
        var game = await _context.Games.FindAsync(id);
        if (game == null)
            return false;

        _context.Games.Remove(game);
        await _context.SaveChangesAsync();
        InvalidateArrangementCache();
        return true;
    }

    private void InvalidateArrangementCache()
    {
        _cache.Remove(ArrangementRepository.ArrangementsCacheKey);
    }
}
