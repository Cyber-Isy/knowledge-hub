namespace KnowledgeHub.Core.Interfaces.Services;

public interface IDocumentProcessingService
{
    Task ProcessDocumentAsync(Guid documentId, CancellationToken ct = default);
}
