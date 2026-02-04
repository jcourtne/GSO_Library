using GSO_Library.Data;
using GSO_Library.Models;
using Microsoft.EntityFrameworkCore;

namespace GSO_Library.Repositories;

public class InstrumentRepository
{
    private readonly GSOLibraryContext _context;

    public InstrumentRepository(GSOLibraryContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Instrument>> GetAllInstrumentsAsync()
    {
        return await _context.Instruments.ToListAsync();
    }

    public async Task<Instrument?> GetInstrumentByIdAsync(int id)
    {
        return await _context.Instruments.FindAsync(id);
    }

    public async Task<Instrument> AddInstrumentAsync(Instrument instrument)
    {
        _context.Instruments.Add(instrument);
        await _context.SaveChangesAsync();
        return instrument;
    }

    public async Task<Instrument?> UpdateInstrumentAsync(int id, Instrument instrument)
    {
        var existing = await _context.Instruments.FindAsync(id);
        if (existing == null)
            return null;

        existing.Name = instrument.Name;
        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task<bool> DeleteInstrumentAsync(int id)
    {
        var instrument = await _context.Instruments.FindAsync(id);
        if (instrument == null)
            return false;

        _context.Instruments.Remove(instrument);
        await _context.SaveChangesAsync();
        return true;
    }
}
