using System.ComponentModel.DataAnnotations;
using KnowledgeHub.Core.Enums;

namespace KnowledgeHub.API.DTOs;

public record SendMessageRequest
{
    [Required]
    [MinLength(1)]
    public required string Content { get; init; }
}

public record CreateConversationRequest
{
    public string? Title { get; init; }
}

public record ConversationDto
{
    public Guid Id { get; init; }
    public required string Title { get; init; }
    public bool IsArchived { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public record MessageDto
{
    public Guid Id { get; init; }
    public required string Content { get; init; }
    public MessageRole Role { get; init; }
    public int TokensUsed { get; init; }
    public DateTime CreatedAt { get; init; }
    public IReadOnlyList<MessageSourceDto> Sources { get; init; } = [];
}

public record MessageSourceDto
{
    public Guid DocumentChunkId { get; init; }
    public double RelevanceScore { get; init; }
    public string? FileName { get; init; }
    public string? ChunkContent { get; init; }
}

public record ChatResponseDto
{
    public required MessageDto Message { get; init; }
    public IReadOnlyList<MessageSourceDto> Sources { get; init; } = [];
}
