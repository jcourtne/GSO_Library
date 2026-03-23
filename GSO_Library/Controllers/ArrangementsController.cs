using GSO_Library.Configuration;
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
    private static readonly HashSet<string> PlaybackExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mid", ".midi", ".mp3", ".wav", ".flac", ".ogg"
    };

    private static readonly Dictionary<string, string> ContentTypeByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"]    = "application/pdf",
        [".zip"]    = "application/zip",
        [".xml"]    = "application/xml",
        [".mxl"]    = "application/vnd.recordare.musicxml",
        [".mid"]    = "audio/midi",
        [".midi"]   = "audio/midi",
        [".mp3"]    = "audio/mpeg",
        [".wav"]    = "audio/wav",
        [".flac"]   = "audio/flac",
        [".ogg"]    = "audio/ogg",
        [".mscz"]   = "application/octet-stream",
        [".dorico"] = "application/octet-stream",
        [".sib"]    = "application/octet-stream",
    };

    private readonly ArrangementRepository _arrangementRepository;
    private readonly ArrangementFileRepository _fileRepository;
    private readonly IFileStorageService _fileStorageService;
    private readonly FileUploadSettings _fileUploadSettings;
    private readonly IAuditService _auditService;

    public ArrangementsController(
        ArrangementRepository arrangementRepository,
        ArrangementFileRepository fileRepository,
        IFileStorageService fileStorageService,
        FileUploadSettings fileUploadSettings,
        IAuditService auditService)
    {
        _arrangementRepository = arrangementRepository;
        _fileRepository = fileRepository;
        _fileStorageService = fileStorageService;
        _fileUploadSettings = fileUploadSettings;
        _auditService = auditService;
    }

    private bool IsSubmitterOnly() =>
        User.IsInRole("Submitter") && !User.IsInRole("Admin") && !User.IsInRole("Librarian");

    private static bool IsOwner(Arrangement arrangement, string? username) =>
        string.Equals(arrangement.CreatedBy, username, StringComparison.OrdinalIgnoreCase);

    [HttpPost]
    [Authorize(Roles = "Admin,Librarian,Submitter")]
    public async Task<ActionResult<Arrangement>> AddArrangement([FromBody] ArrangementRequest request)
    {
        var createdArrangement = await _arrangementRepository.AddArrangementAsync(request, User.Identity?.Name);
        var arrangement = await _arrangementRepository.GetArrangementByIdAsync(createdArrangement.Id);
        await _auditService.LogAsync(Models.AuditEventType.ArrangementCreate, User.Identity?.Name, null, null,
            $"arrangementId: {createdArrangement.Id}");
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
        [FromQuery] int[]? gameIds, [FromQuery] int[]? seriesIds, [FromQuery] int[]? instrumentIds, [FromQuery] int? performanceId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? sortBy = null, [FromQuery] string? sortDirection = null,
        [FromQuery] string? search = null, [FromQuery] string[]? composers = null, [FromQuery] string[]? arrangers = null)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var result = await _arrangementRepository.GetArrangementsAsync(page, pageSize, gameIds, seriesIds, instrumentIds, performanceId, sortBy, sortDirection, search, composers, arrangers);
        return Ok(result);
    }

    [HttpGet("filter-options")]
    [Authorize]
    public async Task<ActionResult> GetFilterOptions()
    {
        var options = await _arrangementRepository.GetFilterOptionsAsync();
        return Ok(options);
    }

    [HttpPut("{id}/details")]
    [Authorize(Roles = "Admin,Librarian,Submitter")]
    public async Task<ActionResult<Arrangement>> UpdateArrangementDetails(int id, [FromBody] ArrangementRequest request)
    {
        if (IsSubmitterOnly())
        {
            var existing = await _arrangementRepository.GetArrangementByIdAsync(id);
            if (existing == null) return NotFound();
            if (!IsOwner(existing, User.Identity?.Name)) return Forbid();
        }

        var updated = await _arrangementRepository.UpdateArrangementAsync(id, request);
        if (updated == null)
            return NotFound();

        return Ok(updated);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin,Librarian,Submitter")]
    public async Task<IActionResult> DeleteArrangement(int id)
    {
        // Get arrangement with files first (before DB deletion)
        var arrangement = await _arrangementRepository.GetArrangementByIdAsync(id);
        if (arrangement == null)
            return NotFound();

        if (IsSubmitterOnly() && !IsOwner(arrangement, User.Identity?.Name))
            return Forbid();

        // Delete files from disk first
        foreach (var file in arrangement.Files)
        {
            await _fileStorageService.DeleteFileAsync($"arrangements/{id}", file.StoredFileName);
        }

        // Then delete from DB
        var deleted = await _arrangementRepository.DeleteArrangementAsync(id);
        if (deleted == null)
            return NotFound();

        await _auditService.LogAsync(Models.AuditEventType.ArrangementDelete, User.Identity?.Name, null, null,
            $"arrangementId: {id}");
        return NoContent();
    }

    [HttpPost("{arrangementId}/games/{gameId}")]
    [Authorize(Roles = "Admin,Librarian,Submitter")]
    public async Task<IActionResult> AddGame(int arrangementId, int gameId)
    {
        if (IsSubmitterOnly())
        {
            var arrangement = await _arrangementRepository.GetArrangementByIdAsync(arrangementId);
            if (arrangement == null) return NotFound();
            if (!IsOwner(arrangement, User.Identity?.Name)) return Forbid();
        }

        var result = await _arrangementRepository.AddGameAsync(arrangementId, gameId);
        if (result == null)
            return NotFound();
        if (!result.Value)
            return BadRequest();

        return NoContent();
    }

    [HttpDelete("{arrangementId}/games/{gameId}")]
    [Authorize(Roles = "Admin,Librarian,Submitter")]
    public async Task<IActionResult> RemoveGame(int arrangementId, int gameId)
    {
        if (IsSubmitterOnly())
        {
            var arrangement = await _arrangementRepository.GetArrangementByIdAsync(arrangementId);
            if (arrangement == null) return NotFound();
            if (!IsOwner(arrangement, User.Identity?.Name)) return Forbid();
        }

        var result = await _arrangementRepository.RemoveGameAsync(arrangementId, gameId);
        if (result == null)
            return NotFound();
        if (!result.Value)
            return NotFound();

        return NoContent();
    }

    [HttpPost("{arrangementId}/instruments/{instrumentId}")]
    [Authorize(Roles = "Admin,Librarian,Submitter")]
    public async Task<IActionResult> AddInstrument(int arrangementId, int instrumentId)
    {
        if (IsSubmitterOnly())
        {
            var arrangement = await _arrangementRepository.GetArrangementByIdAsync(arrangementId);
            if (arrangement == null) return NotFound();
            if (!IsOwner(arrangement, User.Identity?.Name)) return Forbid();
        }

        var result = await _arrangementRepository.AddInstrumentAsync(arrangementId, instrumentId);
        if (result == null)
            return NotFound();
        if (!result.Value)
            return BadRequest();

        return NoContent();
    }

    [HttpDelete("{arrangementId}/instruments/{instrumentId}")]
    [Authorize(Roles = "Admin,Librarian,Submitter")]
    public async Task<IActionResult> RemoveInstrument(int arrangementId, int instrumentId)
    {
        if (IsSubmitterOnly())
        {
            var arrangement = await _arrangementRepository.GetArrangementByIdAsync(arrangementId);
            if (arrangement == null) return NotFound();
            if (!IsOwner(arrangement, User.Identity?.Name)) return Forbid();
        }

        var result = await _arrangementRepository.RemoveInstrumentAsync(arrangementId, instrumentId);
        if (result == null)
            return NotFound();
        if (!result.Value)
            return NotFound();

        return NoContent();
    }

    [HttpPost("{arrangementId}/performances/{performanceId}")]
    [Authorize(Roles = "Admin,Librarian,Submitter")]
    public async Task<IActionResult> AddPerformance(int arrangementId, int performanceId)
    {
        if (IsSubmitterOnly())
        {
            var arrangement = await _arrangementRepository.GetArrangementByIdAsync(arrangementId);
            if (arrangement == null) return NotFound();
            if (!IsOwner(arrangement, User.Identity?.Name)) return Forbid();
        }

        var result = await _arrangementRepository.AddPerformanceAsync(arrangementId, performanceId);
        if (result == null)
            return NotFound();
        if (!result.Value)
            return BadRequest();

        return NoContent();
    }

    [HttpDelete("{arrangementId}/performances/{performanceId}")]
    [Authorize(Roles = "Admin,Librarian,Submitter")]
    public async Task<IActionResult> RemovePerformance(int arrangementId, int performanceId)
    {
        if (IsSubmitterOnly())
        {
            var arrangement = await _arrangementRepository.GetArrangementByIdAsync(arrangementId);
            if (arrangement == null) return NotFound();
            if (!IsOwner(arrangement, User.Identity?.Name)) return Forbid();
        }

        var result = await _arrangementRepository.RemovePerformanceAsync(arrangementId, performanceId);
        if (result == null)
            return NotFound();
        if (!result.Value)
            return NotFound();

        return NoContent();
    }

    [HttpPost("{id}/files")]
    [Authorize(Roles = "Admin,Librarian,Submitter")]
    public async Task<ActionResult<ArrangementFile>> UploadFile(int id, IFormFile file)
    {
        if (IsSubmitterOnly())
        {
            var arrangement = await _arrangementRepository.GetArrangementByIdAsync(id);
            if (arrangement == null) return NotFound();
            if (!IsOwner(arrangement, User.Identity?.Name)) return Forbid();
        }

        if (!await _fileRepository.ArrangementExistsAsync(id))
            return NotFound();

        if (file.Length > _fileUploadSettings.MaxFileSizeBytes)
            return BadRequest($"File size exceeds the maximum allowed size of {_fileUploadSettings.MaxFileSizeBytes} bytes.");

        var extension = Path.GetExtension(file.FileName)?.ToLowerInvariant();
        if (_fileUploadSettings.AllowedExtensions.Length > 0 &&
            (string.IsNullOrEmpty(extension) || !_fileUploadSettings.AllowedExtensions.Contains(extension)))
            return BadRequest($"File extension '{extension}' is not allowed. Allowed extensions: {string.Join(", ", _fileUploadSettings.AllowedExtensions)}");

        var storedFileName = $"{Guid.NewGuid()}{extension}";

        using var stream = file.OpenReadStream();
        await _fileStorageService.SaveFileAsync($"arrangements/{id}", storedFileName, stream);

        var contentType = ContentTypeByExtension.TryGetValue(extension ?? "", out var mapped)
            ? mapped
            : "application/octet-stream";

        var arrangementFile = new ArrangementFile
        {
            FileName = file.FileName,
            StoredFileName = storedFileName,
            ContentType = contentType,
            FileSize = file.Length,
            UploadedAt = DateTime.UtcNow,
            ArrangementId = id,
            CreatedBy = User.Identity?.Name
        };

        await _fileRepository.AddFileAsync(arrangementFile);
        await _auditService.LogAsync(Models.AuditEventType.FileUpload, User.Identity?.Name, null, null,
            $"arrangementId: {id}, fileId: {arrangementFile.Id}, filename: {arrangementFile.FileName}");
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

        if (!User.IsInRole("Admin") && !User.IsInRole("Librarian") && !User.IsInRole("Downloader"))
        {
            var ext = Path.GetExtension(arrangementFile.FileName);
            if (!PlaybackExtensions.Contains(ext ?? ""))
            {
                if (User.IsInRole("Submitter"))
                {
                    var arrangement = await _arrangementRepository.GetArrangementByIdAsync(id);
                    if (arrangement == null || !IsOwner(arrangement, User.Identity?.Name))
                        return Forbid();
                }
                else
                {
                    return Forbid();
                }
            }
        }

        try
        {
            var stream = await _fileStorageService.GetFileAsync($"arrangements/{id}", arrangementFile.StoredFileName);
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            await _auditService.LogAsync(Models.AuditEventType.FileDownload, User.Identity?.Name, null, ip,
                $"arrangementId: {id}, fileId: {fileId}, filename: {arrangementFile.FileName}");
            return File(stream, arrangementFile.ContentType, arrangementFile.FileName);
        }
        catch (FileNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpDelete("{id}/files/{fileId}")]
    [Authorize(Roles = "Admin,Librarian,Submitter")]
    public async Task<IActionResult> DeleteFile(int id, int fileId)
    {
        if (IsSubmitterOnly())
        {
            var arrangement = await _arrangementRepository.GetArrangementByIdAsync(id);
            if (arrangement == null) return NotFound();
            if (!IsOwner(arrangement, User.Identity?.Name)) return Forbid();
        }

        var arrangementFile = await _fileRepository.GetFileAsync(id, fileId);
        if (arrangementFile == null)
            return NotFound();

        await _fileStorageService.DeleteFileAsync($"arrangements/{id}", arrangementFile.StoredFileName);
        await _fileRepository.DeleteFileAsync(id, fileId);
        await _auditService.LogAsync(Models.AuditEventType.FileDelete, User.Identity?.Name, null, null,
            $"arrangementId: {id}, fileId: {fileId}, filename: {arrangementFile.FileName}");
        _arrangementRepository.InvalidateCache();

        return NoContent();
    }
}
