namespace KnowledgeHub.Core.Entities;

public class Conversation : BaseEntity
{
    public string Title { get; set; } = "New Conversation";
    public Guid UserId { get; set; }
    public bool IsArchived { get; set; }

    public ICollection<Message> Messages { get; set; } = [];
}
