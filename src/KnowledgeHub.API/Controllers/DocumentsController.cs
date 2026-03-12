using System.Security.Claims;
using KnowledgeHub.API.Configuration;
using KnowledgeHub.API.DTOs;
using KnowledgeHub.Core.Entities;
using KnowledgeHub.Core.Interfaces.Repositories;
using KnowledgeHub.Core.Interfaces.Services;
using KnowledgeHub.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KnowledgeHub.API.Controllers;

[ApiController]
[Route("api/[controller]")]
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
    public async Task<ActionResult<DocumentDto>> Upload(IFormFile file, CancellationToken ct)
    {
        if (file.Length == 0)
            return BadRequest(new { Message = "File is empty." });

        if (file.Length > FileUploadSettings.MaxFileSizeBytes)
            return BadRequest(new { Message = $"File exceeds the maximum size of {FileUploadSettings.MaxFileSizeBytes / (1024 * 1024)} MB." });

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!FileUploadSettings.AllowedExtensions.Contains(extension))
            return BadRequest(new { Message = $"File type '{extension}' is not allowed. Allowed: {string.Join(", ", FileUploadSettings.AllowedExtensions)}" });

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

    [HttpGet]
    public async Task<ActionResult<IEnumerable<DocumentDto>>> GetAll(CancellationToken ct)
    {
        var userId = GetUserId();
        var documents = await _documentRepository.GetByUserIdAsync(userId, ct);
        return Ok(documents.Select(ToDto));
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
