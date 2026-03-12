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

    [HttpPost("upload")]
    [EnableRateLimiting("upload")]
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

    [HttpGet("stats")]
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

    [HttpGet]
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

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DocumentDto>> GetById(Guid id, CancellationToken ct)
    {
        var document = await _documentRepository.GetByIdAsync(id, ct);
        if (document is null || document.UserId != GetUserId())
            return NotFound();

        return Ok(ToDto(document));
    }

    [HttpGet("{id:guid}/download")]
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

    [HttpDelete("{id:guid}")]
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
