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

    private IQueryable<Arrangement> ArrangementsWithIncludes()
    {
        return _context.Arrangements
            .Include(a => a.Games)
                .ThenInclude(g => g.Series)
            .Include(a => a.Instruments)
            .Include(a => a.Performances)
            .Include(a => a.Files);
    }

    public async Task<Arrangement?> GetArrangementByIdAsync(int id)
    {
        return await ArrangementsWithIncludes()
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task<IEnumerable<Arrangement>> GetAllArrangementsAsync()
    {
        return await ArrangementsWithIncludes().ToListAsync();
    }

    public async Task<PaginatedResult<Arrangement>> GetAllArrangementsAsync(int page, int pageSize)
    {
        var query = ArrangementsWithIncludes();
        var totalCount = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return new PaginatedResult<Arrangement> { Items = items, Page = page, PageSize = pageSize, TotalCount = totalCount };
    }

    public async Task<Arrangement> AddArrangementAsync(Arrangement arrangement)
    {
        _context.Arrangements.Add(arrangement);
        await _context.SaveChangesAsync();
        return arrangement;
    }

    public async Task<Arrangement?> UpdateArrangementAsync(int id, Arrangement arrangement)
    {
        var existing = await _context.Arrangements
            .Include(a => a.Games)
            .Include(a => a.Instruments)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (existing == null)
            return null;

        existing.Name = arrangement.Name;
        existing.Description = arrangement.Description;
        existing.Arranger = arrangement.Arranger;
        existing.Composer = arrangement.Composer;
        existing.Key = arrangement.Key;
        existing.DurationSeconds = arrangement.DurationSeconds;
        existing.Difficulty = arrangement.Difficulty;
        existing.Year = arrangement.Year;

        existing.Games.Clear();
        foreach (var game in arrangement.Games)
        {
            _context.Games.Attach(game);
            existing.Games.Add(game);
        }

        existing.Instruments.Clear();
        foreach (var instrument in arrangement.Instruments)
        {
            _context.Instruments.Attach(instrument);
            existing.Instruments.Add(instrument);
        }

        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task<List<ArrangementFile>?> DeleteArrangementAsync(int id)
    {
        var arrangement = await _context.Arrangements
            .Include(a => a.Files)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (arrangement == null)
            return null;

        var files = arrangement.Files.ToList();
        _context.Arrangements.Remove(arrangement);
        await _context.SaveChangesAsync();
        return files;
    }

    public async Task<IEnumerable<Arrangement>> GetArrangementsByGameIdAsync(int gameId)
    {
        return await ArrangementsWithIncludes()
            .Where(a => a.Games.Any(g => g.Id == gameId))
            .ToListAsync();
    }

    public async Task<PaginatedResult<Arrangement>> GetArrangementsByGameIdAsync(int gameId, int page, int pageSize)
    {
        var query = ArrangementsWithIncludes().Where(a => a.Games.Any(g => g.Id == gameId));
        var totalCount = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return new PaginatedResult<Arrangement> { Items = items, Page = page, PageSize = pageSize, TotalCount = totalCount };
    }

    public async Task<IEnumerable<Arrangement>> GetArrangementsBySeriesIdAsync(int seriesId)
    {
        return await ArrangementsWithIncludes()
            .Where(a => a.Games.Any(g => g.SeriesId == seriesId))
            .ToListAsync();
    }

    public async Task<PaginatedResult<Arrangement>> GetArrangementsBySeriesIdAsync(int seriesId, int page, int pageSize)
    {
        var query = ArrangementsWithIncludes().Where(a => a.Games.Any(g => g.SeriesId == seriesId));
        var totalCount = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return new PaginatedResult<Arrangement> { Items = items, Page = page, PageSize = pageSize, TotalCount = totalCount };
    }

    public async Task<IEnumerable<Arrangement>> GetArrangementsByInstrumentIdAsync(int instrumentId)
    {
        return await ArrangementsWithIncludes()
            .Where(a => a.Instruments.Any(i => i.Id == instrumentId))
            .ToListAsync();
    }

    public async Task<PaginatedResult<Arrangement>> GetArrangementsByInstrumentIdAsync(int instrumentId, int page, int pageSize)
    {
        var query = ArrangementsWithIncludes().Where(a => a.Instruments.Any(i => i.Id == instrumentId));
        var totalCount = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return new PaginatedResult<Arrangement> { Items = items, Page = page, PageSize = pageSize, TotalCount = totalCount };
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
