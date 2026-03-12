using KnowledgeHub.Core.Entities;
using KnowledgeHub.Core.Interfaces.Repositories;
using KnowledgeHub.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace KnowledgeHub.Infrastructure.Repositories;

public class DocumentRepository : Repository<Document>, IDocumentRepository
{
    public DocumentRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IReadOnlyList<Document>> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
        => await DbSet.AsNoTracking()
            .Where(d => d.UserId == userId)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(ct);

    public async Task<Document?> GetWithChunksAsync(Guid id, CancellationToken ct = default)
        => await DbSet
            .Include(d => d.Chunks)
            .FirstOrDefaultAsync(d => d.Id == id, ct);
}
