using GSO_Library.Data;
using GSO_Library.Models;
using GSO_Library.Repositories;
using GSO_Library.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GSO_Library.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ArrangementsController : ControllerBase
{
    private readonly ArrangementRepository _arrangementRepository;
    private readonly IFileStorageService _fileStorageService;
    private readonly GSOLibraryContext _context;

    public ArrangementsController(
        ArrangementRepository arrangementRepository,
        IFileStorageService fileStorageService,
        GSOLibraryContext context)
    {
        _arrangementRepository = arrangementRepository;
        _fileStorageService = fileStorageService;
        _context = context;
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<ActionResult<Arrangement>> AddArrangement([FromBody] Arrangement arrangement)
    {
        var createdArrangement = await _arrangementRepository.AddArrangementAsync(arrangement);
        return CreatedAtAction(nameof(GetArrangementById), new { id = createdArrangement.Id }, createdArrangement);
    }

    [HttpGet("{id}")]
    [Authorize]
    public async Task<ActionResult<Arrangement>> GetArrangementById(int id)
    {
        var arrangement = await _arrangementRepository.GetArrangementByIdAsync(id);
        if (arrangement == null)
            return NotFound();

        return Ok(arrangement);
    }

    [HttpGet]
    [Authorize]
    public async Task<ActionResult<PaginatedResult<Arrangement>>> GetAllArrangements(
        [FromQuery] int? gameId, [FromQuery] int? seriesId, [FromQuery] int? instrumentId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        PaginatedResult<Arrangement> result;

        if (gameId.HasValue)
        {
            result = await _arrangementRepository.GetArrangementsByGameIdAsync(gameId.Value, page, pageSize);
        }
        else if (seriesId.HasValue)
        {
            result = await _arrangementRepository.GetArrangementsBySeriesIdAsync(seriesId.Value, page, pageSize);
        }
        else if (instrumentId.HasValue)
        {
            result = await _arrangementRepository.GetArrangementsByInstrumentIdAsync(instrumentId.Value, page, pageSize);
        }
        else
        {
            result = await _arrangementRepository.GetAllArrangementsAsync(page, pageSize);
        }

        return Ok(result);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<ActionResult<Arrangement>> UpdateArrangement(int id, [FromBody] Arrangement arrangement)
    {
        var updated = await _arrangementRepository.UpdateArrangementAsync(id, arrangement);
        if (updated == null)
            return NotFound();

        return Ok(updated);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<IActionResult> DeleteArrangement(int id)
    {
        var files = await _arrangementRepository.DeleteArrangementAsync(id);
        if (files == null)
            return NotFound();

        foreach (var file in files)
        {
            await _fileStorageService.DeleteFileAsync(id, file.StoredFileName);
        }

        return NoContent();
    }

    [HttpPost("{arrangementId}/performances")]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<ActionResult<Performance>> AddPerformance(int arrangementId, [FromBody] Performance performance)
    {
        var createdPerformance = await _arrangementRepository.AddPerformanceAsync(arrangementId, performance);
        if (createdPerformance == null)
            return NotFound("Arrangement not found");

        return CreatedAtAction(nameof(AddPerformance), new { arrangementId, performanceId = createdPerformance.Id }, createdPerformance);
    }

    [HttpDelete("{arrangementId}/performances/{performanceId}")]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<IActionResult> RemovePerformance(int arrangementId, int performanceId)
    {
        var success = await _arrangementRepository.RemovePerformanceAsync(arrangementId, performanceId);
        if (!success)
            return NotFound("Performance not found or does not belong to this arrangement");

        return NoContent();
    }

    [HttpPost("{id}/files")]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<ActionResult<ArrangementFile>> UploadFile(int id, IFormFile file)
    {
        var arrangement = await _context.Arrangements.FindAsync(id);
        if (arrangement == null)
            return NotFound("Arrangement not found");

        var storedFileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";

        using var stream = file.OpenReadStream();
        await _fileStorageService.SaveFileAsync(id, storedFileName, stream);

        var arrangementFile = new ArrangementFile
        {
            FileName = file.FileName,
            StoredFileName = storedFileName,
            ContentType = file.ContentType,
            FileSize = file.Length,
            UploadedAt = DateTime.UtcNow,
            ArrangementId = id
        };

        _context.ArrangementFiles.Add(arrangementFile);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(DownloadFile), new { id, fileId = arrangementFile.Id }, arrangementFile);
    }

    [HttpGet("{id}/files")]
    [Authorize]
    public async Task<ActionResult<IEnumerable<ArrangementFile>>> ListFiles(int id)
    {
        var arrangement = await _context.Arrangements.FindAsync(id);
        if (arrangement == null)
            return NotFound("Arrangement not found");

        var files = await _context.ArrangementFiles
            .Where(f => f.ArrangementId == id)
            .ToListAsync();

        return Ok(files);
    }

    [HttpGet("{id}/files/{fileId}")]
    [Authorize]
    public async Task<IActionResult> DownloadFile(int id, int fileId)
    {
        var arrangementFile = await _context.ArrangementFiles
            .FirstOrDefaultAsync(f => f.Id == fileId && f.ArrangementId == id);

        if (arrangementFile == null)
            return NotFound("File not found");

        var stream = await _fileStorageService.GetFileAsync(id, arrangementFile.StoredFileName);
        return File(stream, arrangementFile.ContentType, arrangementFile.FileName);
    }

    [HttpDelete("{id}/files/{fileId}")]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<IActionResult> DeleteFile(int id, int fileId)
    {
        var arrangementFile = await _context.ArrangementFiles
            .FirstOrDefaultAsync(f => f.Id == fileId && f.ArrangementId == id);

        if (arrangementFile == null)
            return NotFound("File not found");

        await _fileStorageService.DeleteFileAsync(id, arrangementFile.StoredFileName);

        _context.ArrangementFiles.Remove(arrangementFile);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
