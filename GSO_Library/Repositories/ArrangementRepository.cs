using GSO_Library.Data;
using GSO_Library.Models;
using Microsoft.EntityFrameworkCore;

namespace GSO_Library.Repositories;

public class ArrangementRepository
{
    private readonly GSOLibraryContext _context;

    public ArrangementRepository(GSOLibraryContext context)
    {
        _context = context;
    }

    public async Task<Arrangement?> GetArrangementByIdAsync(int id)
    {
        return await _context.Arrangements
            .Include(a => a.Games)
                .ThenInclude(g => g.Series)
            .Include(a => a.Performances)
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task<IEnumerable<Arrangement>> GetAllArrangementsAsync()
    {
        return await _context.Arrangements
            .Include(a => a.Games)
                .ThenInclude(g => g.Series)
            .Include(a => a.Performances)
            .ToListAsync();
    }

    public async Task<Arrangement> AddArrangementAsync(Arrangement arrangement)
    {
        _context.Arrangements.Add(arrangement);
        await _context.SaveChangesAsync();
        return arrangement;
    }

    public async Task<IEnumerable<Arrangement>> GetArrangementsByGameIdAsync(int gameId)
    {
        return await _context.Arrangements
            .Where(a => a.Games.Any(g => g.Id == gameId))
            .Include(a => a.Games)
                .ThenInclude(g => g.Series)
            .Include(a => a.Performances)
            .ToListAsync();
    }

    public async Task<IEnumerable<Arrangement>> GetArrangementsBySeriesIdAsync(int seriesId)
    {
        return await _context.Arrangements
            .Where(a => a.Games.Any(g => g.SeriesId == seriesId))
            .Include(a => a.Games)
                .ThenInclude(g => g.Series)
            .Include(a => a.Performances)
            .ToListAsync();
    }

    public async Task<Performance?> AddPerformanceAsync(int arrangementId, Performance performance)
    {
        var arrangement = await _context.Arrangements.FindAsync(arrangementId);
        if (arrangement == null)
            return null;

        performance.ArrangementId = arrangementId;
        _context.Performances.Add(performance);
        await _context.SaveChangesAsync();
        return performance;
    }

    public async Task<bool> RemovePerformanceAsync(int arrangementId, int performanceId)
    {
        var performance = await _context.Performances.FindAsync(performanceId);
        if (performance == null || performance.ArrangementId != arrangementId)
            return false;

        _context.Performances.Remove(performance);
        await _context.SaveChangesAsync();
        return true;
    }
}
