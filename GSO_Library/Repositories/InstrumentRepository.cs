using GSO_Library.Data;
using GSO_Library.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace GSO_Library.Repositories;

public class InstrumentRepository
{
    private readonly GSOLibraryContext _context;
    private readonly IMemoryCache _cache;

    public InstrumentRepository(GSOLibraryContext context, IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<IEnumerable<Instrument>> GetAllInstrumentsAsync()
    {
        return await _context.Instruments
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<PaginatedResult<Instrument>> GetAllInstrumentsAsync(int page, int pageSize)
    {
        var query = _context.Instruments.AsNoTracking();
        var totalCount = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return new PaginatedResult<Instrument> { Items = items, Page = page, PageSize = pageSize, TotalCount = totalCount };
    }

    public async Task<Instrument?> GetInstrumentByIdAsync(int id)
    {
        return await _context.Instruments
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == id);
    }

    public async Task<Instrument> AddInstrumentAsync(Instrument instrument)
    {
        _context.Instruments.Add(instrument);
        await _context.SaveChangesAsync();
        InvalidateArrangementCache();
        return instrument;
    }

    public async Task<Instrument?> UpdateInstrumentAsync(int id, Instrument instrument)
    {
        var existing = await _context.Instruments.FindAsync(id);
        if (existing == null)
            return null;

        existing.Name = instrument.Name;
        await _context.SaveChangesAsync();
        InvalidateArrangementCache();
        return existing;
    }

    public async Task<bool> DeleteInstrumentAsync(int id)
    {
        var instrument = await _context.Instruments.FindAsync(id);
        if (instrument == null)
            return false;

        _context.Instruments.Remove(instrument);
        await _context.SaveChangesAsync();
        InvalidateArrangementCache();
        return true;
    }

    private void InvalidateArrangementCache()
    {
        _cache.Remove(ArrangementRepository.ArrangementsCacheKey);
    }
}
