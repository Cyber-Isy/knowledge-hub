using KnowledgeHub.Core.Enums;

namespace KnowledgeHub.API.DTOs;

public record DocumentDto
{
    public Guid Id { get; init; }
    public required string FileName { get; init; }
    public required string ContentType { get; init; }
    public long FileSize { get; init; }
    public DocumentStatus Status { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public record BatchUploadResultDto
{
    public List<DocumentDto> Succeeded { get; init; } = [];
    public List<BatchUploadErrorDto> Failed { get; init; } = [];
}

public record BatchUploadErrorDto
{
    public required string FileName { get; init; }
    public required string Error { get; init; }
}

public record DocumentStatsDto
{
    public int TotalDocuments { get; init; }
    public long TotalStorageBytes { get; init; }
    public Dictionary<string, int> DocumentsByStatus { get; init; } = new();
    public List<DocumentDto> RecentUploads { get; init; } = [];
}
