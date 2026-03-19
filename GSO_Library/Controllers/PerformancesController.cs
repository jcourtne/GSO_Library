using GSO_Library.Configuration;
using GSO_Library.Models;
using GSO_Library.Repositories;
using GSO_Library.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GSO_Library.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PerformancesController : ControllerBase
{
    private readonly PerformanceRepository _performanceRepository;
    private readonly PerformanceFileRepository _fileRepository;
    private readonly IFileStorageService _fileStorageService;
    private readonly FileUploadSettings _fileUploadSettings;
    private readonly IAuditService _auditService;

    public PerformancesController(
        PerformanceRepository performanceRepository,
        PerformanceFileRepository fileRepository,
        IFileStorageService fileStorageService,
        FileUploadSettings fileUploadSettings,
        IAuditService auditService)
    {
        _performanceRepository = performanceRepository;
        _fileRepository = fileRepository;
        _fileStorageService = fileStorageService;
        _fileUploadSettings = fileUploadSettings;
        _auditService = auditService;
    }

    [HttpGet]
    [Authorize]
    public async Task<ActionResult<PaginatedResult<Performance>>> GetAllPerformances(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? sortBy = null, [FromQuery] string? sortDirection = null,
        [FromQuery] string? search = null, [FromQuery] int[]? ensembleIds = null,
        [FromQuery] DateTime? dateFrom = null, [FromQuery] DateTime? dateTo = null)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var result = await _performanceRepository.GetAllPerformancesAsync(page, pageSize, sortBy, sortDirection, search, ensembleIds, dateFrom, dateTo);
        return Ok(result);
    }

    [HttpGet("{id}")]
    [Authorize]
    public async Task<ActionResult<Performance>> GetPerformanceById(int id)
    {
        var performance = await _performanceRepository.GetPerformanceByIdAsync(id);
        if (performance == null)
            return NotFound();

        return Ok(performance);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Librarian")]
    public async Task<ActionResult<Performance>> AddPerformance([FromBody] Performance performance)
    {
        var now = DateTime.UtcNow;
        performance.CreatedAt = now;
        performance.UpdatedAt = now;
        performance.CreatedBy = User.Identity?.Name;
        var created = await _performanceRepository.AddPerformanceAsync(performance);
        return CreatedAtAction(nameof(GetPerformanceById), new { id = created.Id }, created);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,Librarian")]
    public async Task<ActionResult<Performance>> UpdatePerformance(int id, [FromBody] Performance performance)
    {
        performance.UpdatedAt = DateTime.UtcNow;
        var updated = await _performanceRepository.UpdatePerformanceAsync(id, performance);
        if (updated == null)
            return NotFound();

        return Ok(updated);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin,Librarian")]
    public async Task<IActionResult> DeletePerformance(int id)
    {
        var success = await _performanceRepository.DeletePerformanceAsync(id);
        if (!success)
            return NotFound();

        return NoContent();
    }

    [HttpGet("{id}/files")]
    [Authorize]
    public async Task<ActionResult<IEnumerable<PerformanceFile>>> ListFiles(int id)
    {
        if (!await _fileRepository.PerformanceExistsAsync(id))
            return NotFound();

        var files = await _fileRepository.GetFilesByPerformanceIdAsync(id);
        return Ok(files);
    }

    [HttpPost("{id}/files")]
    [Authorize(Roles = "Admin,Librarian")]
    public async Task<ActionResult<PerformanceFile>> UploadFile(int id, IFormFile file)
    {
        if (!await _fileRepository.PerformanceExistsAsync(id))
            return NotFound();

        if (file.Length > _fileUploadSettings.MaxFileSizeBytes)
            return BadRequest($"File size exceeds the maximum allowed size of {_fileUploadSettings.MaxFileSizeBytes} bytes.");

        var extension = Path.GetExtension(file.FileName)?.ToLowerInvariant();
        if (extension != ".pdf")
            return BadRequest("Only PDF files are allowed.");

        var storedFileName = $"{Guid.NewGuid()}.pdf";

        using var stream = file.OpenReadStream();
        await _fileStorageService.SaveFileAsync($"performances/{id}", storedFileName, stream);

        var performanceFile = new PerformanceFile
        {
            FileName = file.FileName,
            StoredFileName = storedFileName,
            ContentType = "application/pdf",
            FileSize = file.Length,
            UploadedAt = DateTime.UtcNow,
            PerformanceId = id,
            CreatedBy = User.Identity?.Name
        };

        await _fileRepository.AddFileAsync(performanceFile);

        return CreatedAtAction(nameof(DownloadFile), new { id, fileId = performanceFile.Id }, performanceFile);
    }

    [HttpGet("{id}/files/{fileId}")]
    [Authorize]
    public async Task<IActionResult> DownloadFile(int id, int fileId)
    {
        var file = await _fileRepository.GetFileAsync(id, fileId);
        if (file == null)
            return NotFound();

        try
        {
            var stream = await _fileStorageService.GetFileAsync($"performances/{id}", file.StoredFileName);
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            await _auditService.LogAsync(AuditEventType.FileDownload, User.Identity?.Name, null, ip,
                $"performanceId: {id}, fileId: {fileId}, filename: {file.FileName}");
            return File(stream, file.ContentType, file.FileName);
        }
        catch (FileNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpDelete("{id}/files/{fileId}")]
    [Authorize(Roles = "Admin,Librarian")]
    public async Task<IActionResult> DeleteFile(int id, int fileId)
    {
        var file = await _fileRepository.GetFileAsync(id, fileId);
        if (file == null)
            return NotFound();

        await _fileStorageService.DeleteFileAsync($"performances/{id}", file.StoredFileName);
        await _fileRepository.DeleteFileAsync(id, fileId);

        return NoContent();
    }
}
