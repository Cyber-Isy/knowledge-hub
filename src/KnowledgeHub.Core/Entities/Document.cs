using KnowledgeHub.Core.Enums;

namespace KnowledgeHub.Core.Entities;

public class Document : BaseEntity
{
    public required string FileName { get; set; }
    public required string ContentType { get; set; }
    public long FileSize { get; set; }
    public string? StoragePath { get; set; }
    public DocumentStatus Status { get; set; } = DocumentStatus.Uploaded;
    public string? ErrorMessage { get; set; }
    public Guid UserId { get; set; }

    public ICollection<DocumentChunk> Chunks { get; set; } = [];
}
