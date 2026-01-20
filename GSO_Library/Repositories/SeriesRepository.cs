using GSO_Library.Data;
using GSO_Library.Models;

namespace GSO_Library.Repositories;

public class SeriesRepository
{
    private readonly GSOLibraryContext _context;

    public SeriesRepository(GSOLibraryContext context)
    {
        _context = context;
    }

    public async Task<Series> AddSeriesAsync(Series series)
    {
        _context.Series.Add(series);
        await _context.SaveChangesAsync();
        return series;
    }

    public async Task DeleteSeriesAsync(int id)
    {
        var series = await _context.Series.FindAsync(id);
        if (series != null)
        {
            _context.Series.Remove(series);
            await _context.SaveChangesAsync();
        }
    }
}
