using System.Security.Claims;
using Asp.Versioning;
using KnowledgeHub.API.Configuration;
using KnowledgeHub.API.DTOs;
using KnowledgeHub.API.Validators;
using KnowledgeHub.Core.Entities;
using KnowledgeHub.Core.Interfaces.Repositories;
using KnowledgeHub.Core.Interfaces.Services;
using KnowledgeHub.Core.Models;
using KnowledgeHub.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace KnowledgeHub.API.Controllers;

/// <summary>
/// Manages document upload, retrieval, download, and deletion.
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("1.0")]
[Authorize]
public class DocumentsController : ControllerBase
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IFileStorageService _fileStorageService;
    private readonly DocumentProcessingBackgroundService _backgroundService;

    public DocumentsController(
        IDocumentRepository documentRepository,
        IFileStorageService fileStorageService,
        DocumentProcessingBackgroundService backgroundService)
    {
        _documentRepository = documentRepository;
        _fileStorageService = fileStorageService;
        _backgroundService = backgroundService;
    }

    /// <summary>
    /// Uploads a document for processing into the knowledge base.
    /// </summary>
    /// <param name="file">The file to upload (PDF, DOCX, TXT, or MD).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created document metadata.</returns>
    /// <response code="201">Document uploaded successfully and queued for processing.</response>
    /// <response code="400">File is empty, exceeds size limit, or has an unsupported format.</response>
    /// <response code="401">The request is not authenticated.</response>
    /// <response code="429">Upload rate limit exceeded.</response>
    [HttpPost("upload")]
    [EnableRateLimiting("upload")]
    [ProducesResponseType(typeof(DocumentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<DocumentDto>> Upload(IFormFile file, CancellationToken ct)
    {
        if (file.Length == 0)
            return BadRequest(new { Message = "File is empty." });

        if (file.Length > FileUploadSettings.MaxFileSizeBytes)
            return BadRequest(new { Message = $"File exceeds the maximum size of {FileUploadSettings.MaxFileSizeBytes / (1024 * 1024)} MB." });

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!FileUploadSettings.AllowedExtensions.Contains(extension))
            return BadRequest(new { Message = $"File type '{extension}' is not allowed. Allowed: {string.Join(", ", FileUploadSettings.AllowedExtensions)}" });

        // Validate file content by magic bytes
        await using var validationStream = file.OpenReadStream();
        if (!FileSignatureValidator.IsValidFileSignature(validationStream, extension))
            return BadRequest(new { Message = $"File content does not match the expected format for '{extension}'." });

        var userId = GetUserId();

        await using var stream = file.OpenReadStream();
        var storagePath = await _fileStorageService.SaveFileAsync(stream, file.FileName, file.ContentType, ct);

        var document = new Document
        {
            FileName = file.FileName,
            ContentType = file.ContentType,
            FileSize = file.Length,
            StoragePath = storagePath,
            UserId = userId
        };

        await _documentRepository.AddAsync(document, ct);

        await _backgroundService.EnqueueAsync(document.Id, ct);

        return CreatedAtAction(nameof(GetById), new { id = document.Id }, ToDto(document));
    }

    /// <summary>
    /// Returns document statistics for the current user.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Aggregate statistics including document counts by status and recent uploads.</returns>
    /// <response code="200">Returns the document statistics.</response>
    /// <response code="401">The request is not authenticated.</response>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(DocumentStatsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<DocumentStatsDto>> GetStats(CancellationToken ct)
    {
        var userId = GetUserId();
        var documents = await _documentRepository.GetByUserIdAsync(userId, ct);

        var stats = new DocumentStatsDto
        {
            TotalDocuments = documents.Count,
            TotalStorageBytes = documents.Sum(d => d.FileSize),
            DocumentsByStatus = documents
                .GroupBy(d => d.Status.ToString())
                .ToDictionary(g => g.Key, g => g.Count()),
            RecentUploads = documents
                .OrderByDescending(d => d.CreatedAt)
                .Take(5)
                .Select(ToDto)
                .ToList()
        };

        return Ok(stats);
    }

    /// <summary>
    /// Returns a paginated list of documents for the current user.
    /// </summary>
    /// <param name="page">Page number (1-based). Defaults to 1.</param>
    /// <param name="pageSize">Number of items per page. Defaults to 20.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A paginated list of documents.</returns>
    /// <response code="200">Returns the paginated document list.</response>
    /// <response code="401">The request is not authenticated.</response>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<DocumentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PagedResult<DocumentDto>>> GetAll(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var userId = GetUserId();
        var pagination = new PaginationParams { Page = page, PageSize = pageSize };
        var pagedDocuments = await _documentRepository.GetByUserIdPagedAsync(userId, pagination, ct);

        var result = new PagedResult<DocumentDto>
        {
            Items = pagedDocuments.Items.Select(ToDto).ToList(),
            TotalCount = pagedDocuments.TotalCount,
            Page = pagedDocuments.Page,
            PageSize = pagedDocuments.PageSize
        };

        return Ok(result);
    }

    /// <summary>
    /// Returns a single document by its unique identifier.
    /// </summary>
    /// <param name="id">The document ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The document metadata.</returns>
    /// <response code="200">Returns the document.</response>
    /// <response code="401">The request is not authenticated.</response>
    /// <response code="404">Document not found or does not belong to the current user.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(DocumentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DocumentDto>> GetById(Guid id, CancellationToken ct)
    {
        var document = await _documentRepository.GetByIdAsync(id, ct);
        if (document is null || document.UserId != GetUserId())
            return NotFound();

        return Ok(ToDto(document));
    }

    /// <summary>
    /// Downloads the original file for a document.
    /// </summary>
    /// <param name="id">The document ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The file content as a binary stream.</returns>
    /// <response code="200">Returns the file content.</response>
    /// <response code="401">The request is not authenticated.</response>
    /// <response code="404">Document not found or file is unavailable.</response>
    [HttpGet("{id:guid}/download")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct)
    {
        var document = await _documentRepository.GetByIdAsync(id, ct);
        if (document is null || document.UserId != GetUserId())
            return NotFound();

        if (document.StoragePath is null)
            return NotFound(new { Message = "File not available." });

        var stream = await _fileStorageService.GetFileAsync(document.StoragePath, ct);
        return File(stream, document.ContentType, document.FileName);
    }

    /// <summary>
    /// Deletes a document and its associated file from storage.
    /// </summary>
    /// <param name="id">The document ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>No content on success.</returns>
    /// <response code="204">Document deleted successfully.</response>
    /// <response code="401">The request is not authenticated.</response>
    /// <response code="404">Document not found or does not belong to the current user.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var document = await _documentRepository.GetByIdAsync(id, ct);
        if (document is null || document.UserId != GetUserId())
            return NotFound();

        if (document.StoragePath is not null)
            await _fileStorageService.DeleteFileAsync(document.StoragePath, ct);

        await _documentRepository.DeleteAsync(document, ct);
        return NoContent();
    }

    private Guid GetUserId()
        => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private static DocumentDto ToDto(Document document) => new()
    {
        Id = document.Id,
        FileName = document.FileName,
        ContentType = document.ContentType,
        FileSize = document.FileSize,
        Status = document.Status,
        ErrorMessage = document.ErrorMessage,
        CreatedAt = document.CreatedAt,
        UpdatedAt = document.UpdatedAt
    };
}
