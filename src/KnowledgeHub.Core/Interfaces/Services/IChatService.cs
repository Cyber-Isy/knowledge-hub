using KnowledgeHub.Core.Entities;

namespace KnowledgeHub.Core.Interfaces.Services;

public interface IChatService
{
    Task<(Message Response, IReadOnlyList<MessageSource> Sources)> SendMessageAsync(
        Guid conversationId, Guid userId, string content, CancellationToken ct = default);

    Task<Conversation> CreateConversationAsync(Guid userId, string? title = null, CancellationToken ct = default);
    Task<Conversation?> GetConversationAsync(Guid conversationId, Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<Conversation>> GetConversationsAsync(Guid userId, CancellationToken ct = default);
}
