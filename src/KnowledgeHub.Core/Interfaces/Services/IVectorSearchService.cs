using KnowledgeHub.Core.Entities;

namespace KnowledgeHub.Core.Interfaces.Services;

public interface IVectorSearchService
{
    Task IndexChunkAsync(DocumentChunk chunk, float[] embedding, CancellationToken ct = default);
    Task<IReadOnlyList<VectorSearchResult>> SearchAsync(float[] queryEmbedding, int topK = 5, CancellationToken ct = default);
    Task DeleteDocumentChunksAsync(Guid documentId, CancellationToken ct = default);
}
