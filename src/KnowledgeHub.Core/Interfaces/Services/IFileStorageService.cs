namespace KnowledgeHub.Core.Interfaces.Services;

public interface IFileStorageService
{
    Task<string> SaveFileAsync(Stream fileStream, string fileName, string contentType, CancellationToken ct = default);
    Task<Stream> GetFileAsync(string storagePath, CancellationToken ct = default);
    Task DeleteFileAsync(string storagePath, CancellationToken ct = default);
    Task<bool> FileExistsAsync(string storagePath, CancellationToken ct = default);
}
