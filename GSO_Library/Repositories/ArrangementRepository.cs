using GSO_Library.Data;
using GSO_Library.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace GSO_Library.Repositories;

public class ArrangementRepository
{
    private const string CacheKey = "ArrangementsWithIncludes";
    private readonly GSOLibraryContext _context;
    private readonly IMemoryCache _cache;

    public ArrangementRepository(GSOLibraryContext context, IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    private async Task<List<Arrangement>> GetCachedArrangementsAsync()
    {
        if (!_cache.TryGetValue(CacheKey, out List<Arrangement>? arrangements))
        {
            arrangements = await _context.Arrangements
                .Include(a => a.Games)
                    .ThenInclude(g => g.Series)
                .Include(a => a.Instruments)
                .Include(a => a.Performances)
                .Include(a => a.Files)
                .AsNoTracking()
                .ToListAsync();

            _cache.Set(CacheKey, arrangements);
        }

        return arrangements!;
    }

    public void InvalidateCache()
    {
        _cache.Remove(CacheKey);
    }

    public async Task<Arrangement?> GetArrangementByIdAsync(int id)
    {
        var arrangements = await GetCachedArrangementsAsync();
        return arrangements.FirstOrDefault(a => a.Id == id);
    }

    public async Task<PaginatedResult<Arrangement>> GetArrangementsAsync(
        int page, int pageSize, int? gameId = null, int? seriesId = null, int? instrumentId = null)
    {
        var arrangements = await GetCachedArrangementsAsync();
        IEnumerable<Arrangement> filtered = arrangements;

        if (gameId.HasValue)
            filtered = filtered.Where(a => a.Games.Any(g => g.Id == gameId.Value));

        if (seriesId.HasValue)
            filtered = filtered.Where(a => a.Games.Any(g => g.SeriesId == seriesId.Value));

        if (instrumentId.HasValue)
            filtered = filtered.Where(a => a.Instruments.Any(i => i.Id == instrumentId.Value));

        var totalCount = filtered.Count();
        var items = filtered.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return new PaginatedResult<Arrangement> { Items = items, Page = page, PageSize = pageSize, TotalCount = totalCount };
    }

    public async Task<Arrangement> AddArrangementAsync(Arrangement arrangement)
    {
        _context.Arrangements.Add(arrangement);
        await _context.SaveChangesAsync();
        InvalidateCache();
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
        InvalidateCache();
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
        InvalidateCache();
        return files;
    }

    public async Task<bool?> AddGameAsync(int arrangementId, int gameId)
    {
        var arrangement = await _context.Arrangements
            .Include(a => a.Games)
            .FirstOrDefaultAsync(a => a.Id == arrangementId);
        if (arrangement == null)
            return null;

        if (arrangement.Games.Any(g => g.Id == gameId))
            return false;

        var game = await _context.Games.FindAsync(gameId);
        if (game == null)
            return false;

        arrangement.Games.Add(game);
        await _context.SaveChangesAsync();
        InvalidateCache();
        return true;
    }

    public async Task<bool?> RemoveGameAsync(int arrangementId, int gameId)
    {
        var arrangement = await _context.Arrangements
            .Include(a => a.Games)
            .FirstOrDefaultAsync(a => a.Id == arrangementId);
        if (arrangement == null)
            return null;

        var game = arrangement.Games.FirstOrDefault(g => g.Id == gameId);
        if (game == null)
            return false;

        arrangement.Games.Remove(game);
        await _context.SaveChangesAsync();
        InvalidateCache();
        return true;
    }

    public async Task<bool?> AddInstrumentAsync(int arrangementId, int instrumentId)
    {
        var arrangement = await _context.Arrangements
            .Include(a => a.Instruments)
            .FirstOrDefaultAsync(a => a.Id == arrangementId);
        if (arrangement == null)
            return null;

        if (arrangement.Instruments.Any(i => i.Id == instrumentId))
            return false;

        var instrument = await _context.Instruments.FindAsync(instrumentId);
        if (instrument == null)
            return false;

        arrangement.Instruments.Add(instrument);
        await _context.SaveChangesAsync();
        InvalidateCache();
        return true;
    }

    public async Task<bool?> RemoveInstrumentAsync(int arrangementId, int instrumentId)
    {
        var arrangement = await _context.Arrangements
            .Include(a => a.Instruments)
            .FirstOrDefaultAsync(a => a.Id == arrangementId);
        if (arrangement == null)
            return null;

        var instrument = arrangement.Instruments.FirstOrDefault(i => i.Id == instrumentId);
        if (instrument == null)
            return false;

        arrangement.Instruments.Remove(instrument);
        await _context.SaveChangesAsync();
        InvalidateCache();
        return true;
    }

    public async Task<Performance?> AddPerformanceAsync(int arrangementId, Performance performance)
    {
        var arrangement = await _context.Arrangements.FindAsync(arrangementId);
        if (arrangement == null)
            return null;

        performance.ArrangementId = arrangementId;
        _context.Performances.Add(performance);
        await _context.SaveChangesAsync();
        InvalidateCache();
        return performance;
    }

    public async Task<bool> RemovePerformanceAsync(int arrangementId, int performanceId)
    {
        var performance = await _context.Performances.FindAsync(performanceId);
        if (performance == null || performance.ArrangementId != arrangementId)
            return false;

        _context.Performances.Remove(performance);
        await _context.SaveChangesAsync();
        InvalidateCache();
        return true;
    }
}
