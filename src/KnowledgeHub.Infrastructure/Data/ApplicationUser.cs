using Microsoft.AspNetCore.Identity;

namespace KnowledgeHub.Infrastructure.Data;

public class ApplicationUser : IdentityUser<Guid>
{
    public string? DisplayName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
