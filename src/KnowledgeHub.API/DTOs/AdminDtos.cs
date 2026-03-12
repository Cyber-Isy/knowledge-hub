namespace KnowledgeHub.API.DTOs;

public record AdminStatsDto
{
    public int TotalUsers { get; init; }
    public int TotalDocuments { get; init; }
    public int TotalConversations { get; init; }
    public int TotalMessages { get; init; }
    public long TotalTokensUsed { get; init; }
}

public record AdminUserDto
{
    public Guid Id { get; init; }
    public required string Email { get; init; }
    public string? DisplayName { get; init; }
    public DateTime CreatedAt { get; init; }
    public bool IsEnabled { get; init; }
    public int DocumentCount { get; init; }
    public int ConversationCount { get; init; }
    public List<string> Roles { get; init; } = [];
}
