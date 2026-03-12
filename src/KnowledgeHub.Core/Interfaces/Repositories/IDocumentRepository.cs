using KnowledgeHub.Core.Entities;
using KnowledgeHub.Core.Models;

namespace KnowledgeHub.Core.Interfaces.Repositories;

public interface IDocumentRepository : IRepository<Document>
{
    Task<IReadOnlyList<Document>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<PagedResult<Document>> GetByUserIdPagedAsync(Guid userId, PaginationParams pagination, CancellationToken ct = default);
    Task<Document?> GetWithChunksAsync(Guid id, CancellationToken ct = default);
}
