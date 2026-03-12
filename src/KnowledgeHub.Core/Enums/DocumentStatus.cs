namespace KnowledgeHub.Core.Enums;

public enum DocumentStatus
{
    Uploaded,
    Processing,
    Chunking,
    Embedding,
    Indexing,
    Ready,
    Failed
}
