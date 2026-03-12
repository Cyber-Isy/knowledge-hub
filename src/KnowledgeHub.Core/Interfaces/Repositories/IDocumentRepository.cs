using KnowledgeHub.Core.Entities;

namespace KnowledgeHub.Core.Interfaces.Repositories;

public interface IDocumentRepository : IRepository<Document>
{
    Task<IReadOnlyList<Document>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<Document?> GetWithChunksAsync(Guid id, CancellationToken ct = default);
}
