namespace KnowledgeHub.Core.Interfaces.Services;

public interface IDocumentTextExtractor
{
    Task<string> ExtractTextAsync(Stream fileStream, CancellationToken ct = default);
    bool CanHandle(string contentType);
}
