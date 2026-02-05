using GSO_Library.Data;
using GSO_Library.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace GSO_Library.Repositories;

public class PerformanceRepository
{
    private readonly GSOLibraryContext _context;
    private readonly IMemoryCache _cache;

    public PerformanceRepository(GSOLibraryContext context, IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<IEnumerable<Performance>> GetAllPerformancesAsync()
    {
        return await _context.Performances
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<PaginatedResult<Performance>> GetAllPerformancesAsync(int page, int pageSize)
    {
        var query = _context.Performances.AsNoTracking();
        var totalCount = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return new PaginatedResult<Performance> { Items = items, Page = page, PageSize = pageSize, TotalCount = totalCount };
    }

    public async Task<Performance?> GetPerformanceByIdAsync(int id)
    {
        return await _context.Performances
            .Include(p => p.Arrangements)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<Performance> AddPerformanceAsync(Performance performance)
    {
        _context.Performances.Add(performance);
        await _context.SaveChangesAsync();
        InvalidateArrangementCache();
        return performance;
    }

    public async Task<Performance?> UpdatePerformanceAsync(int id, Performance performance)
    {
        var existing = await _context.Performances.FindAsync(id);
        if (existing == null)
            return null;

        existing.Name = performance.Name;
        existing.Link = performance.Link;
        existing.PerformanceDate = performance.PerformanceDate;
        existing.Notes = performance.Notes;
        await _context.SaveChangesAsync();
        InvalidateArrangementCache();
        return existing;
    }

    public async Task<bool> DeletePerformanceAsync(int id)
    {
        var performance = await _context.Performances.FindAsync(id);
        if (performance == null)
            return false;

        _context.Performances.Remove(performance);
        await _context.SaveChangesAsync();
        InvalidateArrangementCache();
        return true;
    }

    private void InvalidateArrangementCache()
    {
        _cache.Remove(ArrangementRepository.ArrangementsCacheKey);
    }
}
