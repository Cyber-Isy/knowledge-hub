using System.Security.Claims;
using FluentAssertions;
using KnowledgeHub.API.Configuration;
using KnowledgeHub.API.Controllers;
using KnowledgeHub.API.DTOs;
using KnowledgeHub.Core.Entities;
using KnowledgeHub.Core.Enums;
using KnowledgeHub.Core.Interfaces.Repositories;
using KnowledgeHub.Core.Interfaces.Services;
using KnowledgeHub.Core.Models;
using KnowledgeHub.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;

namespace KnowledgeHub.API.Tests.Controllers;

public class DocumentsControllerTests
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IFileStorageService _fileStorageService;
    private readonly DocumentProcessingBackgroundService _backgroundService;
    private readonly DocumentsController _controller;
    private readonly Guid _userId = Guid.NewGuid();

    public DocumentsControllerTests()
    {
        _documentRepository = Substitute.For<IDocumentRepository>();
        _fileStorageService = Substitute.For<IFileStorageService>();
        _backgroundService = Substitute.For<DocumentProcessingBackgroundService>(
            System.Threading.Channels.Channel.CreateUnbounded<Guid>(),
            Substitute.For<Microsoft.Extensions.DependencyInjection.IServiceScopeFactory>(),
            Substitute.For<Microsoft.Extensions.Logging.ILogger<DocumentProcessingBackgroundService>>());

        _controller = new DocumentsController(_documentRepository, _fileStorageService, _backgroundService);
        SetUserContext(_userId);
    }

    [Fact]
    public async Task Upload_WithValidFile_ReturnsCreated()
    {
        var file = CreateFormFileWithSignature("test.txt", "text/plain", 1024, "Hello, this is a valid text file content.");
        _fileStorageService.SaveFileAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("stored-file.txt");
        _documentRepository.AddAsync(Arg.Any<Document>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<Document>());

        var result = await _controller.Upload(file, CancellationToken.None);

        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        var dto = createdResult.Value.Should().BeOfType<DocumentDto>().Subject;
        dto.FileName.Should().Be("test.txt");
        dto.ContentType.Should().Be("text/plain");
        dto.FileSize.Should().Be(1024);
    }

    [Fact]
    public async Task Upload_WithEmptyFile_ReturnsBadRequest()
    {
        var file = CreateFormFile("empty.pdf", "application/pdf", 0);

        var result = await _controller.Upload(file, CancellationToken.None);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Upload_WithFileTooLarge_ReturnsBadRequest()
    {
        var file = CreateFormFile("large.pdf", "application/pdf", FileUploadSettings.MaxFileSizeBytes + 1);

        var result = await _controller.Upload(file, CancellationToken.None);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Upload_WithDisallowedFileType_ReturnsBadRequest()
    {
        var file = CreateFormFile("script.exe", "application/octet-stream", 1024);

        var result = await _controller.Upload(file, CancellationToken.None);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetAll_ReturnsUserDocuments()
    {
        var documents = new List<Document>
        {
            new() { FileName = "doc1.pdf", ContentType = "application/pdf", FileSize = 100, UserId = _userId },
            new() { FileName = "doc2.txt", ContentType = "text/plain", FileSize = 200, UserId = _userId }
        };
        _documentRepository.GetByUserIdPagedAsync(_userId, Arg.Any<PaginationParams>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<Document>
            {
                Items = documents,
                TotalCount = 2,
                Page = 1,
                PageSize = 20
            });

        var result = await _controller.GetAll(1, 20, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var pagedResult = okResult.Value.Should().BeOfType<PagedResult<DocumentDto>>().Subject;
        pagedResult.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetById_WithOwnDocument_ReturnsDocument()
    {
        var docId = Guid.NewGuid();
        var document = new Document
        {
            Id = docId,
            FileName = "test.pdf",
            ContentType = "application/pdf",
            FileSize = 1024,
            UserId = _userId,
            Status = DocumentStatus.Ready
        };
        _documentRepository.GetByIdAsync(docId, Arg.Any<CancellationToken>())
            .Returns(document);

        var result = await _controller.GetById(docId, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = okResult.Value.Should().BeOfType<DocumentDto>().Subject;
        dto.Id.Should().Be(docId);
        dto.FileName.Should().Be("test.pdf");
    }

    [Fact]
    public async Task GetById_WithOtherUsersDocument_ReturnsNotFound()
    {
        var docId = Guid.NewGuid();
        var document = new Document
        {
            Id = docId,
            FileName = "other.pdf",
            ContentType = "application/pdf",
            FileSize = 1024,
            UserId = Guid.NewGuid() // different user
        };
        _documentRepository.GetByIdAsync(docId, Arg.Any<CancellationToken>())
            .Returns(document);

        var result = await _controller.GetById(docId, CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetById_WithNonexistentId_ReturnsNotFound()
    {
        var docId = Guid.NewGuid();
        _documentRepository.GetByIdAsync(docId, Arg.Any<CancellationToken>())
            .Returns((Document?)null);

        var result = await _controller.GetById(docId, CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Delete_WithOwnDocument_ReturnsNoContent()
    {
        var docId = Guid.NewGuid();
        var document = new Document
        {
            Id = docId,
            FileName = "test.pdf",
            ContentType = "application/pdf",
            FileSize = 1024,
            UserId = _userId,
            StoragePath = "stored-file.pdf"
        };
        _documentRepository.GetByIdAsync(docId, Arg.Any<CancellationToken>())
            .Returns(document);

        var result = await _controller.Delete(docId, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        await _fileStorageService.Received(1).DeleteFileAsync("stored-file.pdf", Arg.Any<CancellationToken>());
        await _documentRepository.Received(1).DeleteAsync(document, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_WithOtherUsersDocument_ReturnsNotFound()
    {
        var docId = Guid.NewGuid();
        var document = new Document
        {
            Id = docId,
            FileName = "other.pdf",
            ContentType = "application/pdf",
            FileSize = 1024,
            UserId = Guid.NewGuid()
        };
        _documentRepository.GetByIdAsync(docId, Arg.Any<CancellationToken>())
            .Returns(document);

        var result = await _controller.Delete(docId, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
        await _documentRepository.DidNotReceive().DeleteAsync(Arg.Any<Document>(), Arg.Any<CancellationToken>());
    }

    private void SetUserContext(Guid userId)
    {
        var claims = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString())
        }, "TestAuth"));

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claims }
        };
    }

    private static IFormFile CreateFormFile(string fileName, string contentType, long size)
    {
        var file = Substitute.For<IFormFile>();
        file.FileName.Returns(fileName);
        file.ContentType.Returns(contentType);
        file.Length.Returns(size);
        file.OpenReadStream().Returns(new MemoryStream(new byte[Math.Min(size, 1)]));
        return file;
    }

    private static IFormFile CreateFormFileWithSignature(string fileName, string contentType, long reportedSize, string textContent)
    {
        var contentBytes = System.Text.Encoding.UTF8.GetBytes(textContent);
        var file = Substitute.For<IFormFile>();
        file.FileName.Returns(fileName);
        file.ContentType.Returns(contentType);
        file.Length.Returns(reportedSize);
        // Return a new stream each time OpenReadStream is called (controller calls it twice)
        file.OpenReadStream().Returns(_ => new MemoryStream(contentBytes), _ => new MemoryStream(contentBytes));
        return file;
    }
}
