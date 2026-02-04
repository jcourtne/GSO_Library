using GSO_Library.Data;
using GSO_Library.Models;
using Microsoft.EntityFrameworkCore;

namespace GSO_Library.Repositories;

public class SeriesRepository
{
    private readonly GSOLibraryContext _context;

    public SeriesRepository(GSOLibraryContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Series>> GetAllSeriesAsync()
    {
        return await _context.Series
            .Include(s => s.Games)
            .ToListAsync();
    }

    public async Task<Series?> GetSeriesByIdAsync(int id)
    {
        return await _context.Series
            .Include(s => s.Games)
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<PaginatedResult<Series>> GetAllSeriesAsync(int page, int pageSize)
    {
        var query = _context.Series.Include(s => s.Games);
        var totalCount = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return new PaginatedResult<Series> { Items = items, Page = page, PageSize = pageSize, TotalCount = totalCount };
    }

    public async Task<Series> AddSeriesAsync(Series series)
    {
        _context.Series.Add(series);
        await _context.SaveChangesAsync();
        return series;
    }

    public async Task<Series?> UpdateSeriesAsync(int id, Series series)
    {
        var existing = await _context.Series.FindAsync(id);
        if (existing == null)
            return null;

        existing.Name = series.Name;
        existing.Description = series.Description;
        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task<bool> DeleteSeriesAsync(int id)
    {
        var series = await _context.Series.FindAsync(id);
        if (series == null)
            return false;

        _context.Series.Remove(series);
        await _context.SaveChangesAsync();
        return true;
    }
}
