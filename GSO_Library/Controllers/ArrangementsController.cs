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


    private static readonly HashSet<string> RenderedScoreExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf"
    };

    private static readonly Dictionary<string, string> ContentTypeByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"]    = "application/pdf",
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
    private readonly IEnsembleAccessService _ensembleAccess;
    private readonly IFileStorageService _fileStorageService;
    private readonly FileUploadSettings _fileUploadSettings;
    private readonly IAuditService _auditService;
    private readonly ISeasonZipCacheService _zipCacheService;

    public ArrangementsController(
        ArrangementRepository arrangementRepository,
        ArrangementFileRepository fileRepository,
        IEnsembleAccessService ensembleAccess,
        IFileStorageService fileStorageService,
        FileUploadSettings fileUploadSettings,
        IAuditService auditService,
        ISeasonZipCacheService zipCacheService)
    {
        _arrangementRepository = arrangementRepository;
        _fileRepository = fileRepository;
        _ensembleAccess = ensembleAccess;
        _fileStorageService = fileStorageService;
        _fileUploadSettings = fileUploadSettings;
        _auditService = auditService;
        _zipCacheService = zipCacheService;
    }

    private static bool IsOwner(Arrangement arrangement, string? username) =>
        string.Equals(arrangement.CreatedBy, username, StringComparison.OrdinalIgnoreCase);

    // Returns null (allow) or a Forbid result. Handles Submitter, Ensemble Librarian, and dual-role
    // users correctly. Admin and Librarian always get null (full access).
    // ForbidResult (not IActionResult) so callers returning ActionResult<T> can `return deny;`.
    private async Task<ForbidResult?> EnforceWriteAccessAsync(Arrangement arrangement)
    {
        if (User.IsInRole(Roles.Admin) || User.IsInRole(Roles.Librarian))
            return null;

        bool isSubmitter = User.IsInRole(Roles.Submitter);
        bool isEnsembleLibrarian = User.IsInRole(Roles.EnsembleLibrarian);

        // Ensemble Librarians can also create arrangements (Roles.ArrangementEditors). A newly
        // created arrangement has no ensembles yet, so IsMemberOfAnyAsync would be vacuously
        // false — fall back to ownership so the creator can still restrict/edit it afterwards.
        bool ownerOk = (isSubmitter || isEnsembleLibrarian) && IsOwner(arrangement, User.Identity?.Name);
        bool ensembleOk = isEnsembleLibrarian && await _ensembleAccess.IsMemberOfAnyAsync(User, arrangement.Ensembles.Select(e => e.Id));

        return (ownerOk || ensembleOk) ? null : Forbid();
    }

    [HttpPost]
    [Authorize(Roles = Roles.ArrangementEditors)]
    public async Task<ActionResult<Arrangement>> AddArrangement([FromBody] ArrangementRequest request)
    {
        var createdArrangement = await _arrangementRepository.AddArrangementAsync(request, User.Identity?.Name);

        // New arrangements start with no ensembles (public). The creator can restrict access
        // afterwards by linking one or more ensembles.
        var arrangement = await _arrangementRepository.GetArrangementByIdAsync(createdArrangement.Id);
        await _auditService.LogAsync(Models.AuditEventType.ArrangementCreate, User.Identity?.Name, null, null,
            $"arrangementId: {createdArrangement.Id} ({arrangement?.Name})");
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
        [FromQuery] string? search = null, [FromQuery] string[]? composers = null, [FromQuery] string[]? arrangers = null,
        [FromQuery] bool instrumentMatchAll = false)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var result = await _arrangementRepository.GetArrangementsAsync(page, pageSize, gameIds, seriesIds, instrumentIds, performanceId, sortBy, sortDirection, search, composers, arrangers, instrumentMatchAll);
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
    [Authorize(Roles = Roles.ArrangementEditors)]
    public async Task<ActionResult<Arrangement>> UpdateArrangementDetails(int id, [FromBody] ArrangementRequest request)
    {
        var existing = await _arrangementRepository.GetArrangementByIdAsync(id);
        if (existing == null) return NotFound();
        var deny = await EnforceWriteAccessAsync(existing);
        if (deny != null) return deny;

        var updated = await _arrangementRepository.UpdateArrangementAsync(id, request);
        if (updated == null)
            return NotFound();

        await _zipCacheService.InvalidateForArrangementAsync(id);
        return Ok(updated);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = Roles.ArrangementEditors)]
    public async Task<IActionResult> DeleteArrangement(int id)
    {
        // Get arrangement with files first (before DB deletion)
        var arrangement = await _arrangementRepository.GetArrangementByIdAsync(id);
        if (arrangement == null)
            return NotFound();

        var deny = await EnforceWriteAccessAsync(arrangement);
        if (deny != null) return deny;

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
            $"arrangementId: {id} ({arrangement.Name})");
        await _zipCacheService.InvalidateForArrangementAsync(id);
        return NoContent();
    }

    [HttpPost("{arrangementId}/games/{gameId}")]
    [Authorize(Roles = Roles.ArrangementEditors)]
    public async Task<IActionResult> AddGame(int arrangementId, int gameId)
    {
        var arrangement = await _arrangementRepository.GetArrangementByIdAsync(arrangementId);
        if (arrangement == null) return NotFound();
        var deny = await EnforceWriteAccessAsync(arrangement);
        if (deny != null) return deny;

        var result = await _arrangementRepository.AddGameAsync(arrangementId, gameId);
        if (result == null)
            return NotFound();
        if (!result.Value)
            return BadRequest();

        return NoContent();
    }

    [HttpDelete("{arrangementId}/games/{gameId}")]
    [Authorize(Roles = Roles.ArrangementEditors)]
    public async Task<IActionResult> RemoveGame(int arrangementId, int gameId)
    {
        var arrangement = await _arrangementRepository.GetArrangementByIdAsync(arrangementId);
        if (arrangement == null) return NotFound();
        var deny = await EnforceWriteAccessAsync(arrangement);
        if (deny != null) return deny;

        var result = await _arrangementRepository.RemoveGameAsync(arrangementId, gameId);
        if (result == null)
            return NotFound();
        if (!result.Value)
            return NotFound();

        return NoContent();
    }

    [HttpPost("{arrangementId}/instruments/{instrumentId}")]
    [Authorize(Roles = Roles.ArrangementEditors)]
    public async Task<IActionResult> AddInstrument(int arrangementId, int instrumentId)
    {
        var arrangement = await _arrangementRepository.GetArrangementByIdAsync(arrangementId);
        if (arrangement == null) return NotFound();
        var deny = await EnforceWriteAccessAsync(arrangement);
        if (deny != null) return deny;

        var result = await _arrangementRepository.AddInstrumentAsync(arrangementId, instrumentId);
        if (result == null)
            return NotFound();
        if (!result.Value)
            return BadRequest();

        return NoContent();
    }

    [HttpDelete("{arrangementId}/instruments/{instrumentId}")]
    [Authorize(Roles = Roles.ArrangementEditors)]
    public async Task<IActionResult> RemoveInstrument(int arrangementId, int instrumentId)
    {
        var arrangement = await _arrangementRepository.GetArrangementByIdAsync(arrangementId);
        if (arrangement == null) return NotFound();
        var deny = await EnforceWriteAccessAsync(arrangement);
        if (deny != null) return deny;

        var result = await _arrangementRepository.RemoveInstrumentAsync(arrangementId, instrumentId);
        if (result == null)
            return NotFound();
        if (!result.Value)
            return NotFound();

        return NoContent();
    }

    [HttpPost("{arrangementId}/performances/{performanceId}")]
    [Authorize(Roles = Roles.ArrangementEditors)]
    public async Task<IActionResult> AddPerformance(int arrangementId, int performanceId)
    {
        var arrangement = await _arrangementRepository.GetArrangementByIdAsync(arrangementId);
        if (arrangement == null) return NotFound();
        var deny = await EnforceWriteAccessAsync(arrangement);
        if (deny != null) return deny;

        var result = await _arrangementRepository.AddPerformanceAsync(arrangementId, performanceId);
        if (result == null)
            return NotFound();
        if (!result.Value)
            return BadRequest();

        return NoContent();
    }

    [HttpDelete("{arrangementId}/performances/{performanceId}")]
    [Authorize(Roles = Roles.ArrangementEditors)]
    public async Task<IActionResult> RemovePerformance(int arrangementId, int performanceId)
    {
        var arrangement = await _arrangementRepository.GetArrangementByIdAsync(arrangementId);
        if (arrangement == null) return NotFound();
        var deny = await EnforceWriteAccessAsync(arrangement);
        if (deny != null) return deny;

        var result = await _arrangementRepository.RemovePerformanceAsync(arrangementId, performanceId);
        if (result == null)
            return NotFound();
        if (!result.Value)
            return NotFound();

        return NoContent();
    }

    [HttpPost("{arrangementId}/ensembles/{ensembleId}")]
    [Authorize(Roles = Roles.ArrangementEditors)]
    public async Task<IActionResult> AddEnsemble(int arrangementId, int ensembleId)
    {
        var arrangement = await _arrangementRepository.GetArrangementByIdAsync(arrangementId);
        if (arrangement == null) return NotFound();
        var deny = await EnforceWriteAccessAsync(arrangement);
        if (deny != null) return deny;

        // Non-admin/librarian users can only link an ensemble they are a member of
        if (!User.IsInRole(Roles.Admin) && !User.IsInRole(Roles.Librarian))
        {
            if (!await _ensembleAccess.IsMemberAsync(User, ensembleId))
                return Forbid();
        }

        var result = await _arrangementRepository.AddEnsembleAsync(arrangementId, ensembleId);
        if (result == null)
            return NotFound();
        if (!result.Value)
            return BadRequest();

        await _auditService.LogAsync(Models.AuditEventType.ArrangementEnsembleAdd, User.Identity?.Name, null, null,
            $"arrangementId: {arrangementId}, ensembleId: {ensembleId}");
        await _zipCacheService.InvalidateForArrangementAsync(arrangementId);
        return NoContent();
    }

    [HttpDelete("{arrangementId}/ensembles/{ensembleId}")]
    [Authorize(Roles = Roles.ArrangementEditors)]
    public async Task<IActionResult> RemoveEnsemble(int arrangementId, int ensembleId)
    {
        var arrangement = await _arrangementRepository.GetArrangementByIdAsync(arrangementId);
        if (arrangement == null) return NotFound();
        var deny = await EnforceWriteAccessAsync(arrangement);
        if (deny != null) return deny;

        // Non-admin/librarian users can only unlink an ensemble they are a member of
        if (!User.IsInRole(Roles.Admin) && !User.IsInRole(Roles.Librarian))
        {
            if (!await _ensembleAccess.IsMemberAsync(User, ensembleId))
                return Forbid();
        }

        var result = await _arrangementRepository.RemoveEnsembleAsync(arrangementId, ensembleId);
        if (result == null)
            return NotFound();
        if (!result.Value)
            return NotFound();

        await _auditService.LogAsync(Models.AuditEventType.ArrangementEnsembleRemove, User.Identity?.Name, null, null,
            $"arrangementId: {arrangementId}, ensembleId: {ensembleId}");
        await _zipCacheService.InvalidateForArrangementAsync(arrangementId);
        return NoContent();
    }

    [HttpPost("{id}/files")]
    [Authorize(Roles = Roles.ArrangementEditors)]
    public async Task<ActionResult<ArrangementFile>> UploadFile(int id, IFormFile file,
        [FromForm] string? scorePartType = null)
    {
        var arrangement = await _arrangementRepository.GetArrangementByIdAsync(id);
        if (arrangement == null) return NotFound();
        var deny = await EnforceWriteAccessAsync(arrangement);
        if (deny != null) return deny;

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

        var isPdf = RenderedScoreExtensions.Contains(extension ?? "");
        var arrangementFile = new ArrangementFile
        {
            FileName = file.FileName,
            StoredFileName = storedFileName,
            ContentType = contentType,
            FileSize = file.Length,
            UploadedAt = DateTime.UtcNow,
            ArrangementId = id,
            CreatedBy = User.Identity?.Name,
            ScorePartType = isPdf ? (scorePartType ?? ScorePartType.UnlistedPart) : null,
        };

        await _fileRepository.AddFileAsync(arrangementFile);

        if (extension is ".mp3")
        {
            try
            {
                using var durationStream = file.OpenReadStream();
                using var tagFile = TagLib.File.Create(new Mp3StreamAbstraction(file.FileName, durationStream));
                var seconds = (int)Math.Round(tagFile.Properties.Duration.TotalSeconds);
                if (seconds > 0)
                    await _arrangementRepository.SetDurationIfUnsetAsync(id, seconds);
            }
            catch { }
        }

        await _auditService.LogAsync(Models.AuditEventType.FileUpload, User.Identity?.Name, null, null,
            $"arrangementId: {id}, fileId: {arrangementFile.Id}, filename: {arrangementFile.FileName}");
        _arrangementRepository.InvalidateCache();
        await _zipCacheService.InvalidateForArrangementAsync(id);

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

        if (!User.IsInRole(Roles.Admin) && !User.IsInRole(Roles.Librarian) && !User.IsInRole(Roles.Downloader))
        {
            var ext = Path.GetExtension(arrangementFile.FileName);
            if (!PlaybackExtensions.Contains(ext ?? ""))
            {
                var arrangement = await _arrangementRepository.GetArrangementByIdAsync(id);
                if (arrangement == null) return NotFound();

                // Any one of these grants access — a user can hold multiple roles (e.g. a
                // Submitter who is also an Ensemble Librarian) and should pass if either applies.
                bool ensembleOk = (User.IsInRole(Roles.EnsembleLibrarian) || User.IsInRole(Roles.EnsembleDownloader))
                    // A public arrangement (no ensemble) is downloadable by anyone with an
                    // ensemble-download role; otherwise the user must share an ensemble with it.
                    && (arrangement.IsPublic || await _ensembleAccess.IsMemberOfAnyAsync(User, arrangement.Ensembles.Select(e => e.Id)));
                bool ownerOk = (User.IsInRole(Roles.Submitter) || User.IsInRole(Roles.EnsembleLibrarian)) && IsOwner(arrangement, User.Identity?.Name);

                if (!ensembleOk && !ownerOk) return Forbid();
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
    [Authorize(Roles = Roles.ArrangementEditors)]
    public async Task<IActionResult> DeleteFile(int id, int fileId)
    {
        var arrangement = await _arrangementRepository.GetArrangementByIdAsync(id);
        if (arrangement == null) return NotFound();
        var deny = await EnforceWriteAccessAsync(arrangement);
        if (deny != null) return deny;

        var arrangementFile = await _fileRepository.GetFileAsync(id, fileId);
        if (arrangementFile == null)
            return NotFound();

        await _fileStorageService.DeleteFileAsync($"arrangements/{id}", arrangementFile.StoredFileName);
        await _fileRepository.DeleteFileAsync(id, fileId);
        await _auditService.LogAsync(Models.AuditEventType.FileDelete, User.Identity?.Name, null, null,
            $"arrangementId: {id}, fileId: {fileId}, filename: {arrangementFile.FileName}");
        _arrangementRepository.InvalidateCache();
        await _zipCacheService.InvalidateForArrangementAsync(id);

        return NoContent();
    }

    [HttpPatch("{id}/files/{fileId}")]
    [Authorize(Roles = Roles.ArrangementEditors)]
    public async Task<IActionResult> UpdateFileMetadata(int id, int fileId, [FromBody] UpdateFileMetadataRequest request)
    {
        if (request.ScorePartType != null && !ScorePartType.All.Contains(request.ScorePartType))
            return BadRequest("Invalid scorePartType");

        var arrangement = await _arrangementRepository.GetArrangementByIdAsync(id);
        if (arrangement == null) return NotFound();
        var deny = await EnforceWriteAccessAsync(arrangement);
        if (deny != null) return deny;

        var file = await _fileRepository.GetFileAsync(id, fileId);
        if (file == null)
            return NotFound();

        var updated = await _fileRepository.UpdateFileMetadataAsync(id, fileId, request.ScorePartType, request.InstrumentIds);
        if (!updated)
            return NotFound();

        _arrangementRepository.InvalidateCache();
        await _zipCacheService.InvalidateForArrangementAsync(id);
        return NoContent();
    }

    private sealed class Mp3StreamAbstraction(string name, Stream stream) : TagLib.File.IFileAbstraction
    {
        public string Name { get; } = name;
        public Stream ReadStream { get; } = stream;
        public Stream WriteStream { get; } = stream;
        public void CloseStream(Stream s) { }
    }
}
