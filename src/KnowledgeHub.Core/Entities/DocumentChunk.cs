namespace KnowledgeHub.Core.Entities;

public class DocumentChunk : BaseEntity
{
    public required string Content { get; set; }
    public string? EmbeddingId { get; set; }
    public int ChunkIndex { get; set; }
    public int TokenCount { get; set; }

    public Guid DocumentId { get; set; }
    public Document Document { get; set; } = null!;

    public ICollection<MessageSource> MessageSources { get; set; } = [];
}
