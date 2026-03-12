using KnowledgeHub.Core.Interfaces.Services;

namespace KnowledgeHub.Infrastructure.Services;

public class LocalFileStorageService : IFileStorageService
{
    private readonly string _basePath;

    public LocalFileStorageService(string basePath = "uploads")
    {
        _basePath = Path.GetFullPath(basePath);
        Directory.CreateDirectory(_basePath);
    }

    public async Task<string> SaveFileAsync(Stream fileStream, string fileName, string contentType, CancellationToken ct = default)
    {
        var uniqueName = $"{Guid.NewGuid()}{Path.GetExtension(fileName)}";
        var filePath = Path.Combine(_basePath, uniqueName);

        await using var outputStream = File.Create(filePath);
        await fileStream.CopyToAsync(outputStream, ct);

        return uniqueName;
    }

    public Task<Stream> GetFileAsync(string storagePath, CancellationToken ct = default)
    {
        var filePath = Path.Combine(_basePath, storagePath);
        if (!File.Exists(filePath))
            throw new FileNotFoundException("File not found.", storagePath);

        return Task.FromResult<Stream>(File.OpenRead(filePath));
    }

    public Task DeleteFileAsync(string storagePath, CancellationToken ct = default)
    {
        var filePath = Path.Combine(_basePath, storagePath);
        if (File.Exists(filePath))
            File.Delete(filePath);

        return Task.CompletedTask;
    }

    public Task<bool> FileExistsAsync(string storagePath, CancellationToken ct = default)
    {
        var filePath = Path.Combine(_basePath, storagePath);
        return Task.FromResult(File.Exists(filePath));
    }
}
