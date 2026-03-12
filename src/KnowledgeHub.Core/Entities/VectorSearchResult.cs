namespace KnowledgeHub.Core.Entities;

public record VectorSearchResult(
    Guid DocumentChunkId,
    string Content,
    double Score,
    Guid DocumentId,
    string FileName);
