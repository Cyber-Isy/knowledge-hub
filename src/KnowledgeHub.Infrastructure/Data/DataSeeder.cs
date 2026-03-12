using KnowledgeHub.Core.Entities;
using KnowledgeHub.Core.Enums;
using KnowledgeHub.Core.Interfaces.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace KnowledgeHub.Infrastructure.Data;

public class DataSeeder : IDataSeeder
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _context;

    public DataSeeder(UserManager<ApplicationUser> userManager, ApplicationDbContext context)
    {
        _userManager = userManager;
        _context = context;
    }

    public async Task SeedAsync()
    {
        var demoUser = await SeedDemoUserAsync();
        var adminUser = await SeedAdminUserAsync();

        await SeedDocumentsAsync(demoUser.Id);
        await SeedConversationsAsync(demoUser.Id);
    }

    private async Task<ApplicationUser> SeedDemoUserAsync()
    {
        const string email = "demo@knowledgehub.ch";
        var existing = await _userManager.FindByEmailAsync(email);
        if (existing is not null)
            return existing;

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            DisplayName = "Demo User",
            EmailConfirmed = true
        };

        await _userManager.CreateAsync(user, "Demo1234!");
        await _userManager.AddToRoleAsync(user, "User");

        return user;
    }

    private async Task<ApplicationUser> SeedAdminUserAsync()
    {
        const string email = "admin@knowledgehub.ch";
        var existing = await _userManager.FindByEmailAsync(email);
        if (existing is not null)
            return existing;

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            DisplayName = "Admin",
            EmailConfirmed = true
        };

        await _userManager.CreateAsync(user, "Admin1234!");
        await _userManager.AddToRoleAsync(user, "Admin");

        return user;
    }

    private async Task SeedDocumentsAsync(Guid userId)
    {
        if (await _context.Documents.AnyAsync(d => d.UserId == userId))
            return;

        var documents = new List<Document>
        {
            new()
            {
                FileName = "company-handbook.pdf",
                ContentType = "application/pdf",
                FileSize = 2_457_600,
                StoragePath = "seed/company-handbook.pdf",
                Status = DocumentStatus.Ready,
                UserId = userId
            },
            new()
            {
                FileName = "technical-architecture.docx",
                ContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                FileSize = 1_048_576,
                StoragePath = "seed/technical-architecture.docx",
                Status = DocumentStatus.Ready,
                UserId = userId
            },
            new()
            {
                FileName = "api-guidelines.md",
                ContentType = "text/markdown",
                FileSize = 524_288,
                StoragePath = "seed/api-guidelines.md",
                Status = DocumentStatus.Ready,
                UserId = userId
            }
        };

        _context.Documents.AddRange(documents);
        await _context.SaveChangesAsync();
    }

    private async Task SeedConversationsAsync(Guid userId)
    {
        if (await _context.Conversations.AnyAsync(c => c.UserId == userId))
            return;

        // Conversation 1
        var conversation1 = new Conversation
        {
            Title = "Company policies overview",
            UserId = userId
        };
        _context.Conversations.Add(conversation1);
        await _context.SaveChangesAsync();

        var messages1 = new List<Message>
        {
            new()
            {
                Content = "What are the main sections in our company handbook?",
                Role = MessageRole.User,
                ConversationId = conversation1.Id,
                TokensUsed = 12
            },
            new()
            {
                Content = "Based on the company handbook, the main sections cover: 1) Code of Conduct, 2) Remote Work Policy, 3) Benefits and Compensation, and 4) Professional Development. Each section outlines guidelines and procedures for employees.",
                Role = MessageRole.Assistant,
                ConversationId = conversation1.Id,
                TokensUsed = 85
            },
            new()
            {
                Content = "Can you explain the remote work policy in more detail?",
                Role = MessageRole.User,
                ConversationId = conversation1.Id,
                TokensUsed = 11
            },
            new()
            {
                Content = "The remote work policy allows employees to work from home up to 3 days per week. Key requirements include: maintaining core hours (10:00-15:00), using the company VPN for secure access, and attending mandatory in-person meetings on designated office days. Managers must approve remote work schedules in advance.",
                Role = MessageRole.Assistant,
                ConversationId = conversation1.Id,
                TokensUsed = 120
            }
        };
        _context.Messages.AddRange(messages1);

        // Conversation 2
        var conversation2 = new Conversation
        {
            Title = "API design best practices",
            UserId = userId
        };
        _context.Conversations.Add(conversation2);
        await _context.SaveChangesAsync();

        var messages2 = new List<Message>
        {
            new()
            {
                Content = "What REST API versioning strategy does our documentation recommend?",
                Role = MessageRole.User,
                ConversationId = conversation2.Id,
                TokensUsed = 14
            },
            new()
            {
                Content = "According to the API guidelines, the recommended versioning strategy is URL path versioning (e.g., /api/v1/resources). This approach was chosen for its clarity and ease of testing. The guidelines also recommend maintaining at most two active versions simultaneously and providing a 6-month deprecation notice before retiring an older version.",
                Role = MessageRole.Assistant,
                ConversationId = conversation2.Id,
                TokensUsed = 95
            },
            new()
            {
                Content = "What about error handling conventions?",
                Role = MessageRole.User,
                ConversationId = conversation2.Id,
                TokensUsed = 8
            }
        };
        _context.Messages.AddRange(messages2);
        await _context.SaveChangesAsync();
    }
}
