using GSO_Library.Data;
using GSO_Library.Models;

namespace GSO_Library.Repositories;

public class PerformanceRepository
{
    private readonly GSOLibraryContext _context;

    public PerformanceRepository(GSOLibraryContext context)
    {
        _context = context;
    }

    public async Task<Performance> AddPerformanceAsync(Performance performance)
    {
        _context.Performances.Add(performance);
        await _context.SaveChangesAsync();
        return performance;
    }

    public async Task DeletePerformanceAsync(int id)
    {
        var performance = await _context.Performances.FindAsync(id);
        if (performance != null)
        {
            _context.Performances.Remove(performance);
            await _context.SaveChangesAsync();
        }
    }
}
