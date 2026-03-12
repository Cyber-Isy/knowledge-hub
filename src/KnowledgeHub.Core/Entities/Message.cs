using KnowledgeHub.Core.Enums;

namespace KnowledgeHub.Core.Entities;

public class Message : BaseEntity
{
    public required string Content { get; set; }
    public MessageRole Role { get; set; }
    public int TokensUsed { get; set; }

    public Guid ConversationId { get; set; }
    public Conversation Conversation { get; set; } = null!;

    public ICollection<MessageSource> Sources { get; set; } = [];
}
