using GSO_Library.Data;
using GSO_Library.Dtos;
using GSO_Library.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace GSO_Library.Repositories;

public class ArrangementRepository
{
    // Cache key exposed for other repositories to invalidate when their entities change
    public const string ArrangementsCacheKey = "ArrangementsWithIncludes";

    private static readonly SemaphoreSlim _cacheLock = new(1, 1);
    private readonly GSOLibraryContext _context;
    private readonly IMemoryCache _cache;

    public ArrangementRepository(GSOLibraryContext context, IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    // Note: Cached entries must be treated as read-only. Do not mutate returned objects.
    private async Task<List<Arrangement>> GetCachedArrangementsAsync()
    {
        // Fast path: check cache without lock
        if (_cache.TryGetValue(ArrangementsCacheKey, out List<Arrangement>? arrangements))
        {
            return arrangements!;
        }

        // Slow path: acquire lock and double-check
        await _cacheLock.WaitAsync();
        try
        {
            // Double-check after acquiring lock
            if (_cache.TryGetValue(ArrangementsCacheKey, out arrangements))
            {
                return arrangements!;
            }

            arrangements = await _context.Arrangements
                .Include(a => a.Games)
                    .ThenInclude(g => g.Series)
                .Include(a => a.Instruments)
                .Include(a => a.Performances)
                .Include(a => a.Files)
                .AsNoTracking()
                .ToListAsync();

            _cache.Set(ArrangementsCacheKey, arrangements, new MemoryCacheEntryOptions
            {
                SlidingExpiration = TimeSpan.FromMinutes(30)
            });

            return arrangements;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    public void InvalidateCache()
    {
        _cache.Remove(ArrangementsCacheKey);
    }

    public async Task<Arrangement?> GetArrangementByIdAsync(int id)
    {
        var arrangements = await GetCachedArrangementsAsync();
        return arrangements.FirstOrDefault(a => a.Id == id);
    }

    public async Task<PaginatedResult<Arrangement>> GetArrangementsAsync(
        int page, int pageSize, int? gameId = null, int? seriesId = null, int? instrumentId = null, int? performanceId = null)
    {
        var arrangements = await GetCachedArrangementsAsync();
        IEnumerable<Arrangement> filtered = arrangements;

        if (gameId.HasValue)
            filtered = filtered.Where(a => a.Games.Any(g => g.Id == gameId.Value));

        if (seriesId.HasValue)
            filtered = filtered.Where(a => a.Games.Any(g => g.SeriesId == seriesId.Value));

        if (instrumentId.HasValue)
            filtered = filtered.Where(a => a.Instruments.Any(i => i.Id == instrumentId.Value));

        if (performanceId.HasValue)
            filtered = filtered.Where(a => a.Performances.Any(p => p.Id == performanceId.Value));

        var totalCount = filtered.Count();
        var items = filtered.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return new PaginatedResult<Arrangement> { Items = items, Page = page, PageSize = pageSize, TotalCount = totalCount };
    }

    public async Task<Arrangement> AddArrangementAsync(ArrangementRequest request)
    {
        var arrangement = new Arrangement
        {
            Name = request.Name,
            Description = request.Description,
            Arranger = request.Arranger,
            Composer = request.Composer,
            Key = request.Key,
            DurationSeconds = request.DurationSeconds,
            Year = request.Year
        };

        _context.Arrangements.Add(arrangement);
        await _context.SaveChangesAsync();
        InvalidateCache();
        return arrangement;
    }

    public async Task<Arrangement?> UpdateArrangementAsync(int id, ArrangementRequest request)
    {
        var existing = await _context.Arrangements.FindAsync(id);
        if (existing == null)
            return null;

        existing.Name = request.Name;
        existing.Description = request.Description;
        existing.Arranger = request.Arranger;
        existing.Composer = request.Composer;
        existing.Key = request.Key;
        existing.DurationSeconds = request.DurationSeconds;
        existing.Year = request.Year;

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

    public async Task<bool?> AddPerformanceAsync(int arrangementId, int performanceId)
    {
        var arrangement = await _context.Arrangements
            .Include(a => a.Performances)
            .FirstOrDefaultAsync(a => a.Id == arrangementId);
        if (arrangement == null)
            return null;

        if (arrangement.Performances.Any(p => p.Id == performanceId))
            return false;

        var performance = await _context.Performances.FindAsync(performanceId);
        if (performance == null)
            return false;

        arrangement.Performances.Add(performance);
        await _context.SaveChangesAsync();
        InvalidateCache();
        return true;
    }

    public async Task<bool?> RemovePerformanceAsync(int arrangementId, int performanceId)
    {
        var arrangement = await _context.Arrangements
            .Include(a => a.Performances)
            .FirstOrDefaultAsync(a => a.Id == arrangementId);
        if (arrangement == null)
            return null;

        var performance = arrangement.Performances.FirstOrDefault(p => p.Id == performanceId);
        if (performance == null)
            return false;

        arrangement.Performances.Remove(performance);
        await _context.SaveChangesAsync();
        InvalidateCache();
        return true;
    }
}
