using GSO_Library.Dtos;
using GSO_Library.Models;
using GSO_Library.Repositories;
using GSO_Library.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GSO_Library.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ArrangementsController : ControllerBase
{
    private readonly ArrangementRepository _arrangementRepository;
    private readonly ArrangementFileRepository _fileRepository;
    private readonly IFileStorageService _fileStorageService;

    public ArrangementsController(
        ArrangementRepository arrangementRepository,
        ArrangementFileRepository fileRepository,
        IFileStorageService fileStorageService)
    {
        _arrangementRepository = arrangementRepository;
        _fileRepository = fileRepository;
        _fileStorageService = fileStorageService;
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<ActionResult<Arrangement>> AddArrangement([FromBody] ArrangementRequest request)
    {
        var createdArrangement = await _arrangementRepository.AddArrangementAsync(request);
        var arrangement = await _arrangementRepository.GetArrangementByIdAsync(createdArrangement.Id);
        return CreatedAtAction(nameof(GetArrangementById), new { id = createdArrangement.Id }, arrangement);
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
        [FromQuery] int? gameId, [FromQuery] int? seriesId, [FromQuery] int? instrumentId, [FromQuery] int? performanceId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var result = await _arrangementRepository.GetArrangementsAsync(page, pageSize, gameId, seriesId, instrumentId, performanceId);
        return Ok(result);
    }

    [HttpPut("{id}/details")]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<ActionResult<Arrangement>> UpdateArrangementDetails(int id, [FromBody] ArrangementRequest request)
    {
        var updated = await _arrangementRepository.UpdateArrangementAsync(id, request);
        if (updated == null)
            return NotFound();

        return Ok(updated);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<IActionResult> DeleteArrangement(int id)
    {
        // Get arrangement with files first (before DB deletion)
        var arrangement = await _arrangementRepository.GetArrangementByIdAsync(id);
        if (arrangement == null)
            return NotFound();

        // Delete files from disk first
        foreach (var file in arrangement.Files)
        {
            await _fileStorageService.DeleteFileAsync(id, file.StoredFileName);
        }

        // Then delete from DB
        var deleted = await _arrangementRepository.DeleteArrangementAsync(id);
        if (deleted == null)
            return NotFound();

        return NoContent();
    }

    [HttpPost("{arrangementId}/games/{gameId}")]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<IActionResult> AddGame(int arrangementId, int gameId)
    {
        var result = await _arrangementRepository.AddGameAsync(arrangementId, gameId);
        if (result == null)
            return NotFound();
        if (!result.Value)
            return BadRequest();

        return NoContent();
    }

    [HttpDelete("{arrangementId}/games/{gameId}")]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<IActionResult> RemoveGame(int arrangementId, int gameId)
    {
        var result = await _arrangementRepository.RemoveGameAsync(arrangementId, gameId);
        if (result == null)
            return NotFound();
        if (!result.Value)
            return NotFound();

        return NoContent();
    }

    [HttpPost("{arrangementId}/instruments/{instrumentId}")]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<IActionResult> AddInstrument(int arrangementId, int instrumentId)
    {
        var result = await _arrangementRepository.AddInstrumentAsync(arrangementId, instrumentId);
        if (result == null)
            return NotFound();
        if (!result.Value)
            return BadRequest();

        return NoContent();
    }

    [HttpDelete("{arrangementId}/instruments/{instrumentId}")]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<IActionResult> RemoveInstrument(int arrangementId, int instrumentId)
    {
        var result = await _arrangementRepository.RemoveInstrumentAsync(arrangementId, instrumentId);
        if (result == null)
            return NotFound();
        if (!result.Value)
            return NotFound();

        return NoContent();
    }

    [HttpPost("{arrangementId}/performances/{performanceId}")]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<IActionResult> AddPerformance(int arrangementId, int performanceId)
    {
        var result = await _arrangementRepository.AddPerformanceAsync(arrangementId, performanceId);
        if (result == null)
            return NotFound();
        if (!result.Value)
            return BadRequest();

        return NoContent();
    }

    [HttpDelete("{arrangementId}/performances/{performanceId}")]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<IActionResult> RemovePerformance(int arrangementId, int performanceId)
    {
        var result = await _arrangementRepository.RemovePerformanceAsync(arrangementId, performanceId);
        if (result == null)
            return NotFound();
        if (!result.Value)
            return NotFound();

        return NoContent();
    }

    [HttpPost("{id}/files")]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<ActionResult<ArrangementFile>> UploadFile(int id, IFormFile file)
    {
        if (!await _fileRepository.ArrangementExistsAsync(id))
            return NotFound();

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

        await _fileRepository.AddFileAsync(arrangementFile);
        _arrangementRepository.InvalidateCache();

        return CreatedAtAction(nameof(DownloadFile), new { id, fileId = arrangementFile.Id }, arrangementFile);
    }

    [HttpGet("{id}/files")]
    [Authorize]
    public async Task<ActionResult<IEnumerable<ArrangementFile>>> ListFiles(int id)
    {
        if (!await _fileRepository.ArrangementExistsAsync(id))
            return NotFound();

        var files = await _fileRepository.GetFilesByArrangementIdAsync(id);
        return Ok(files);
    }

    [HttpGet("{id}/files/{fileId}")]
    [Authorize]
    public async Task<IActionResult> DownloadFile(int id, int fileId)
    {
        var arrangementFile = await _fileRepository.GetFileAsync(id, fileId);
        if (arrangementFile == null)
            return NotFound();

        try
        {
            var stream = await _fileStorageService.GetFileAsync(id, arrangementFile.StoredFileName);
            return File(stream, arrangementFile.ContentType, arrangementFile.FileName);
        }
        catch (FileNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpDelete("{id}/files/{fileId}")]
    [Authorize(Roles = "Admin,Editor")]
    public async Task<IActionResult> DeleteFile(int id, int fileId)
    {
        var arrangementFile = await _fileRepository.GetFileAsync(id, fileId);
        if (arrangementFile == null)
            return NotFound();

        await _fileStorageService.DeleteFileAsync(id, arrangementFile.StoredFileName);
        await _fileRepository.DeleteFileAsync(id, fileId);
        _arrangementRepository.InvalidateCache();

        return NoContent();
    }
}
