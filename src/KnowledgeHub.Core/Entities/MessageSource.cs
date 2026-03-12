namespace KnowledgeHub.Core.Entities;

public class MessageSource : BaseEntity
{
    public double RelevanceScore { get; set; }

    public Guid MessageId { get; set; }
    public Message Message { get; set; } = null!;

    public Guid DocumentChunkId { get; set; }
    public DocumentChunk DocumentChunk { get; set; } = null!;
}
