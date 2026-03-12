using FluentAssertions;
using KnowledgeHub.Infrastructure.Services;

namespace KnowledgeHub.Infrastructure.Tests.Services;

public class LocalFileStorageServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly LocalFileStorageService _service;

    public LocalFileStorageServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"knowledgehub-test-{Guid.NewGuid()}");
        _service = new LocalFileStorageService(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public void Constructor_CreatesDirectory()
    {
        Directory.Exists(_tempDir).Should().BeTrue();
    }

    [Fact]
    public async Task SaveFileAsync_StoresFileAndReturnsPath()
    {
        var content = "Hello, World!"u8.ToArray();
        using var stream = new MemoryStream(content);

        var storagePath = await _service.SaveFileAsync(stream, "test.txt", "text/plain");

        storagePath.Should().NotBeNullOrEmpty();
        storagePath.Should().EndWith(".txt");

        var filePath = Path.Combine(_tempDir, storagePath);
        File.Exists(filePath).Should().BeTrue();
    }

    [Fact]
    public async Task SaveFileAsync_PreservesFileContent()
    {
        var content = "Test file content for verification"u8.ToArray();
        using var stream = new MemoryStream(content);

        var storagePath = await _service.SaveFileAsync(stream, "data.txt", "text/plain");

        var savedContent = await File.ReadAllBytesAsync(Path.Combine(_tempDir, storagePath));
        savedContent.Should().BeEquivalentTo(content);
    }

    [Fact]
    public async Task SaveFileAsync_GeneratesUniqueName()
    {
        var content = "content"u8.ToArray();

        using var stream1 = new MemoryStream(content);
        var path1 = await _service.SaveFileAsync(stream1, "file.txt", "text/plain");

        using var stream2 = new MemoryStream(content);
        var path2 = await _service.SaveFileAsync(stream2, "file.txt", "text/plain");

        path1.Should().NotBe(path2);
    }

    [Fact]
    public async Task GetFileAsync_ReturnsFileStream()
    {
        var content = "Readable content"u8.ToArray();
        using var inputStream = new MemoryStream(content);
        var storagePath = await _service.SaveFileAsync(inputStream, "read.txt", "text/plain");

        using var resultStream = await _service.GetFileAsync(storagePath);

        var resultContent = new MemoryStream();
        await resultStream.CopyToAsync(resultContent);
        resultContent.ToArray().Should().BeEquivalentTo(content);
    }

    [Fact]
    public async Task GetFileAsync_WhenFileNotFound_ThrowsFileNotFoundException()
    {
        var act = () => _service.GetFileAsync("nonexistent-file.txt");

        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public async Task DeleteFileAsync_RemovesFile()
    {
        var content = "To be deleted"u8.ToArray();
        using var stream = new MemoryStream(content);
        var storagePath = await _service.SaveFileAsync(stream, "delete-me.txt", "text/plain");

        await _service.DeleteFileAsync(storagePath);

        var filePath = Path.Combine(_tempDir, storagePath);
        File.Exists(filePath).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteFileAsync_WhenFileNotFound_DoesNotThrow()
    {
        var act = () => _service.DeleteFileAsync("nonexistent-file.txt");

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task FileExistsAsync_WhenFileExists_ReturnsTrue()
    {
        var content = "Exists check"u8.ToArray();
        using var stream = new MemoryStream(content);
        var storagePath = await _service.SaveFileAsync(stream, "exists.txt", "text/plain");

        var result = await _service.FileExistsAsync(storagePath);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task FileExistsAsync_WhenFileNotFound_ReturnsFalse()
    {
        var result = await _service.FileExistsAsync("nonexistent-file.txt");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task SaveAndGetFile_RoundTrip_PreservesData()
    {
        var originalContent = new byte[4096];
        Random.Shared.NextBytes(originalContent);
        using var inputStream = new MemoryStream(originalContent);

        var storagePath = await _service.SaveFileAsync(inputStream, "binary.dat", "application/octet-stream");

        using var outputStream = await _service.GetFileAsync(storagePath);
        var retrievedContent = new MemoryStream();
        await outputStream.CopyToAsync(retrievedContent);

        retrievedContent.ToArray().Should().BeEquivalentTo(originalContent);
    }
}
