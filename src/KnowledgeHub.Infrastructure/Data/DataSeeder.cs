using KnowledgeHub.Core.Entities;
using KnowledgeHub.Core.Enums;
using KnowledgeHub.Core.Interfaces.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KnowledgeHub.Infrastructure.Data;

public class DataSeeder : IDataSeeder
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _context;
    private readonly IEmbeddingService _embeddingService;
    private readonly IVectorSearchService _vectorSearchService;
    private readonly ILogger<DataSeeder> _logger;

    public DataSeeder(
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext context,
        IEmbeddingService embeddingService,
        IVectorSearchService vectorSearchService,
        ILogger<DataSeeder> logger)
    {
        _userManager = userManager;
        _context = context;
        _embeddingService = embeddingService;
        _vectorSearchService = vectorSearchService;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        var demoUser = await SeedDemoUserAsync();
        var adminUser = await SeedAdminUserAsync();

        await SeedDocumentsWithChunksAsync(demoUser.Id);
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

    private async Task SeedDocumentsWithChunksAsync(Guid userId)
    {
        if (await _context.Documents.AnyAsync(d => d.UserId == userId))
        {
            // Documents exist but chunks may not be indexed (InMemory store resets on restart).
            // Re-index existing chunks.
            await ReindexExistingChunksAsync();
            return;
        }

        var seedDocuments = GetSeedDocuments(userId);

        foreach (var (document, chunks) in seedDocuments)
        {
            _context.Documents.Add(document);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Seeding document '{FileName}' with {ChunkCount} chunks...",
                document.FileName, chunks.Count);

            for (int i = 0; i < chunks.Count; i++)
            {
                var chunk = new DocumentChunk
                {
                    Content = chunks[i],
                    ChunkIndex = i,
                    TokenCount = chunks[i].Split(' ').Length,
                    DocumentId = document.Id,
                    Document = document
                };

                _context.DocumentChunks.Add(chunk);
                await _context.SaveChangesAsync();

                // Generate embedding and index
                var embedding = await _embeddingService.GenerateEmbeddingAsync(chunks[i]);
                await _vectorSearchService.IndexChunkAsync(chunk, embedding);

                _logger.LogDebug("Indexed chunk {Index}/{Total} for '{FileName}'",
                    i + 1, chunks.Count, document.FileName);
            }

            _logger.LogInformation("Finished seeding document '{FileName}'", document.FileName);
        }
    }

    private async Task ReindexExistingChunksAsync()
    {
        var chunks = await _context.DocumentChunks
            .Include(c => c.Document)
            .ToListAsync();

        if (chunks.Count == 0)
            return;

        _logger.LogInformation("Re-indexing {Count} existing chunks into vector store...", chunks.Count);

        foreach (var chunk in chunks)
        {
            var embedding = await _embeddingService.GenerateEmbeddingAsync(chunk.Content);
            await _vectorSearchService.IndexChunkAsync(chunk, embedding);
        }

        _logger.LogInformation("Re-indexed {Count} chunks", chunks.Count);
    }

    private static List<(Document Document, List<string> Chunks)> GetSeedDocuments(Guid userId)
    {
        return
        [
            (
                new Document
                {
                    FileName = "company-handbook.pdf",
                    ContentType = "application/pdf",
                    FileSize = 2_457_600,
                    StoragePath = "seed/company-handbook.pdf",
                    Status = DocumentStatus.Ready,
                    UserId = userId
                },
                [
                    "Company Handbook - Code of Conduct: All employees are expected to maintain the highest standards of professional behavior. This includes treating colleagues with respect, maintaining confidentiality of company information, and adhering to all applicable laws and regulations. Violations may result in disciplinary action up to and including termination.",

                    "Remote Work Policy: Employees may work remotely up to 3 days per week with manager approval. Core hours are 10:00-15:00 during which all remote workers must be available. A company VPN must be used for all remote access to internal systems. Mandatory in-person meetings are scheduled on Tuesdays and Thursdays.",

                    "Benefits and Compensation: The company offers competitive salaries benchmarked annually against industry standards. Benefits include health insurance (covered at 80%), dental and vision plans, 25 days paid vacation, a retirement savings plan with 5% company match, and annual professional development budget of CHF 2,000 per employee.",

                    "Professional Development: Employees are encouraged to pursue ongoing education and certifications. The company provides CHF 2,000 annually for courses, conferences, and training materials. Employees may also take up to 5 paid learning days per year. A mentorship program pairs junior employees with senior staff for career guidance.",

                    "Onboarding Process: New employees undergo a 2-week onboarding program. Week 1 covers IT setup, security training, HR orientation, and team introductions. Week 2 includes project-specific training, codebase walkthroughs, and pairing with a buddy. All new hires receive a welcome kit with company laptop, badge, and handbook."
                ]
            ),
            (
                new Document
                {
                    FileName = "technical-architecture.docx",
                    ContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                    FileSize = 1_048_576,
                    StoragePath = "seed/technical-architecture.docx",
                    Status = DocumentStatus.Ready,
                    UserId = userId
                },
                [
                    "System Architecture Overview: The platform follows a clean architecture pattern with three layers — Core (domain entities and interfaces), Infrastructure (database access and external services), and API (controllers and configuration). The backend is built with ASP.NET Core Web API using .NET 10, with Entity Framework Core for data access.",

                    "Database Design: The system uses SQLite for local development and Azure SQL for production. Key entities include Documents (uploaded files), DocumentChunks (text segments with embeddings), Conversations, Messages, and MessageSources (citation links). All entities inherit from BaseEntity which provides Id, CreatedAt, and UpdatedAt fields.",

                    "AI and RAG Pipeline: Documents are processed through a pipeline: text extraction (PDF, DOCX, TXT), chunking with overlap, embedding generation via Azure OpenAI (text-embedding-3-small), and vector indexing. Chat queries follow the RAG pattern — the user query is embedded, similar chunks are retrieved via vector search, and a context-augmented prompt is sent to GPT-4o for response generation.",

                    "Authentication and Security: The system uses ASP.NET Core Identity with JWT bearer tokens. Users register and log in via REST endpoints. All API endpoints require authorization. Passwords are hashed with bcrypt. CORS is configured for the Angular frontend origin. Rate limiting is applied to auth and upload endpoints.",

                    "Real-time Communication: Chat responses are streamed token-by-token from the AI model to the client via SignalR WebSocket connections. The ChatHub authenticates connections using JWT tokens from query parameters. Clients receive ReceiveSources events with citation data and StreamCompleted events when generation finishes."
                ]
            ),
            (
                new Document
                {
                    FileName = "api-guidelines.md",
                    ContentType = "text/markdown",
                    FileSize = 524_288,
                    StoragePath = "seed/api-guidelines.md",
                    Status = DocumentStatus.Ready,
                    UserId = userId
                },
                [
                    "API Versioning Strategy: All API endpoints use URL path versioning (e.g., /api/v1/resources). At most two active versions should be maintained simultaneously. When deprecating a version, provide a minimum 6-month notice period. Version numbers increment only for breaking changes. Non-breaking additions use the current version.",

                    "Error Handling Conventions: All errors return RFC 7807 Problem Details format. Use standard HTTP status codes: 400 for validation errors, 401 for unauthenticated, 403 for unauthorized, 404 for not found, 409 for conflicts, 429 for rate limiting, 500 for server errors. Include a correlation ID in every error response for tracing.",

                    "Pagination Standards: List endpoints must support pagination using offset and limit query parameters. Default page size is 20, maximum is 100. Responses use a PagedResult wrapper with items, totalCount, page, and pageSize fields. Include navigation links (next, prev) in response headers.",

                    "Naming Conventions: Use PascalCase for C# class names and camelCase for JSON properties. Endpoint paths use kebab-case (e.g., /api/v1/document-chunks). Boolean parameters should be named as questions (e.g., isArchived, hasAttachment). Date fields use ISO 8601 format (UTC).",

                    "Authentication Flow: Clients obtain a JWT token via POST /api/v1/auth/login with email and password. The token is included in subsequent requests via the Authorization: Bearer header. Tokens expire after 7 days. Refresh tokens are not implemented in v1 — clients must re-authenticate after expiry."
                ]
            )
        ];
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
