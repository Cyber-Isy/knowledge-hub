using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using KnowledgeHub.Core.Interfaces.Services;

namespace KnowledgeHub.Infrastructure.Services;

public class AzureBlobStorageService : IFileStorageService
{
    private readonly BlobContainerClient _containerClient;

    public AzureBlobStorageService(BlobServiceClient blobServiceClient, string containerName)
    {
        _containerClient = blobServiceClient.GetBlobContainerClient(containerName);
        _containerClient.CreateIfNotExists();
    }

    public async Task<string> SaveFileAsync(Stream fileStream, string fileName, string contentType, CancellationToken ct = default)
    {
        var blobName = $"{Guid.NewGuid()}{Path.GetExtension(fileName)}";
        var blobClient = _containerClient.GetBlobClient(blobName);

        var options = new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = contentType }
        };

        await blobClient.UploadAsync(fileStream, options, ct);

        return blobName;
    }

    public async Task<Stream> GetFileAsync(string storagePath, CancellationToken ct = default)
    {
        var blobClient = _containerClient.GetBlobClient(storagePath);

        if (!await blobClient.ExistsAsync(ct))
            throw new FileNotFoundException("Blob not found.", storagePath);

        var response = await blobClient.DownloadStreamingAsync(cancellationToken: ct);
        return response.Value.Content;
    }

    public async Task DeleteFileAsync(string storagePath, CancellationToken ct = default)
    {
        var blobClient = _containerClient.GetBlobClient(storagePath);
        await blobClient.DeleteIfExistsAsync(cancellationToken: ct);
    }

    public async Task<bool> FileExistsAsync(string storagePath, CancellationToken ct = default)
    {
        var blobClient = _containerClient.GetBlobClient(storagePath);
        var response = await blobClient.ExistsAsync(ct);
        return response.Value;
    }
}
