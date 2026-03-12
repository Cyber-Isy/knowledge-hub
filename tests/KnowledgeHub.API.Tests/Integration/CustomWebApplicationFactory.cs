using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Threading.Channels;
using KnowledgeHub.API.Configuration;
using KnowledgeHub.Core.Configuration;
using KnowledgeHub.Core.Entities;
using KnowledgeHub.Core.Interfaces.Repositories;
using KnowledgeHub.Core.Interfaces.Services;
using KnowledgeHub.Infrastructure.Data;
using KnowledgeHub.Infrastructure.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using NSubstitute;
using Serilog;

namespace KnowledgeHub.API.Tests.Integration;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private const string TestSecret = "integration-test-secret-key-minimum-32-characters-long";
    private const string TestIssuer = "TestIssuer";
    private const string TestAudience = "TestAudience";

    public CustomWebApplicationFactory()
    {
        // Reset Serilog bootstrap logger before any host creation
        // This prevents "The logger is already frozen" errors in parallel test runs
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Warning()
            .CreateLogger();
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        // Reset Serilog again right before host creation
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Warning()
            .CreateLogger();

        return base.CreateHost(builder);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Remove all EF Core and database provider registrations to avoid dual provider conflicts
            var efDescriptors = services.Where(d =>
                d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>) ||
                d.ServiceType == typeof(ApplicationDbContext) ||
                d.ServiceType.FullName?.Contains("EntityFrameworkCore") == true ||
                d.ImplementationType?.FullName?.Contains("EntityFrameworkCore.Sqlite") == true)
                .ToList();
            foreach (var descriptor in efDescriptors)
                services.Remove(descriptor);

            // Add InMemory database with a unique name per factory (name must be
            // computed outside the lambda so all scopes share the same database)
            var dbName = $"TestDb-{Guid.NewGuid()}";
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase(dbName));

            // Replace external services with stubs
            ReplaceService<IEmbeddingService>(services, Substitute.For<IEmbeddingService>());
            ReplaceService<IVectorSearchService>(services, Substitute.For<IVectorSearchService>());

            // Replace IChatService with a stub that delegates CRUD to the real repository
            RemoveServicesByType(services, typeof(IChatService));
            services.AddScoped<IChatService>(sp =>
            {
                var conversationRepo = sp.GetRequiredService<IRepository<Conversation>>();
                var chatServiceStub = Substitute.For<IChatService>();

                chatServiceStub.CreateConversationAsync(Arg.Any<Guid>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
                    .Returns(callInfo =>
                    {
                        var userId = callInfo.ArgAt<Guid>(0);
                        var title = callInfo.ArgAt<string?>(1) ?? "New Conversation";
                        var conversation = new Conversation { Title = title, UserId = userId };
                        return conversationRepo.AddAsync(conversation, callInfo.ArgAt<CancellationToken>(2));
                    });

                chatServiceStub.GetConversationAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                    .Returns(async callInfo =>
                    {
                        var conversationId = callInfo.ArgAt<Guid>(0);
                        var userId = callInfo.ArgAt<Guid>(1);
                        var conversation = await conversationRepo.GetByIdAsync(conversationId, callInfo.ArgAt<CancellationToken>(2));
                        return conversation?.UserId == userId ? conversation : null;
                    });

                chatServiceStub.GetConversationsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                    .Returns(async callInfo =>
                    {
                        var userId = callInfo.ArgAt<Guid>(0);
                        var conversations = await conversationRepo.FindAsync(
                            c => c.UserId == userId && !c.IsArchived,
                            callInfo.ArgAt<CancellationToken>(1));
                        return conversations;
                    });

                return chatServiceStub;
            });

            // Replace DocumentProcessingBackgroundService to avoid background processing
            RemoveServicesByType(services, typeof(DocumentProcessingBackgroundService));
            var channel = Channel.CreateUnbounded<Guid>();
            services.AddSingleton(channel);
            var bgService = Substitute.For<DocumentProcessingBackgroundService>(
                channel,
                Substitute.For<IServiceScopeFactory>(),
                Substitute.For<Microsoft.Extensions.Logging.ILogger<DocumentProcessingBackgroundService>>());
            services.AddSingleton(bgService);

            // Replace IFileStorageService with local temp storage
            RemoveServicesByType(services, typeof(IFileStorageService));
            var tempDir = Path.Combine(Path.GetTempPath(), $"knowledgehub-inttest-{Guid.NewGuid()}");
            services.AddSingleton<IFileStorageService>(new LocalFileStorageService(tempDir));

            // Remove hosted services to prevent startup issues
            RemoveServicesByType(services, typeof(Microsoft.Extensions.Hosting.IHostedService));

            // Reconfigure JWT Bearer to use test key and accept tokens without kid header.
            // Force use of JwtSecurityTokenHandler for claim type mapping compatibility.
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestSecret));
                options.UseSecurityTokenValidators = true;
#pragma warning disable CS0618 // SecurityTokenValidators is obsolete but needed for claim mapping
                options.SecurityTokenValidators.Clear();
                options.SecurityTokenValidators.Add(new JwtSecurityTokenHandler { MapInboundClaims = true });
#pragma warning restore CS0618
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = TestIssuer,
                    ValidAudience = TestAudience,
                    IssuerSigningKey = key,
                    ClockSkew = TimeSpan.Zero,
                    RoleClaimType = ClaimTypes.Role
                };
            });

            // Disable rate limiting for tests by removing all limiter option configurations
            // and registering a no-op global limiter
            RemoveServicesByType(services, typeof(IConfigureOptions<RateLimiterOptions>));
            RemoveServicesByType(services, typeof(IPostConfigureOptions<RateLimiterOptions>));
            services.Configure<RateLimiterOptions>(options =>
            {
                options.RejectionStatusCode = 429;
                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(
                    _ => RateLimitPartition.GetNoLimiter("test"));

                options.AddFixedWindowLimiter("auth", o => { o.PermitLimit = 100000; o.Window = TimeSpan.FromMinutes(1); });
                options.AddFixedWindowLimiter("upload", o => { o.PermitLimit = 100000; o.Window = TimeSpan.FromMinutes(1); });
                options.AddFixedWindowLimiter("chat", o => { o.PermitLimit = 100000; o.Window = TimeSpan.FromMinutes(1); });
            });
        });

        builder.ConfigureAppConfiguration((context, config) =>
        {
            var testConfig = new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = TestSecret,
                ["Jwt:Issuer"] = TestIssuer,
                ["Jwt:Audience"] = TestAudience,
                ["Jwt:ExpirationInMinutes"] = "60",
                ["AzureOpenAI:Endpoint"] = "https://test.openai.azure.com/",
                ["AzureOpenAI:ApiKey"] = "test-api-key",
                ["AzureOpenAI:EmbeddingDeploymentName"] = "test-embedding",
                ["AzureOpenAI:ChatDeploymentName"] = "test-chat",
                ["AzureAISearch:Endpoint"] = "https://test.search.windows.net",
                ["AzureAISearch:ApiKey"] = "test-search-key",
                ["AzureAISearch:IndexName"] = "test-index"
            };

            config.AddInMemoryCollection(testConfig);
        });
    }

    public HttpClient CreateAuthenticatedClient(Guid? userId = null)
    {
        var client = CreateClient();
        var token = GenerateTestToken(userId ?? Guid.NewGuid());
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public async Task<(HttpClient Client, string Token, Guid UserId)> RegisterAndLoginAsync()
    {
        var client = CreateClient();

        var registerRequest = new
        {
            Email = $"test-{Guid.NewGuid()}@example.com",
            Password = "TestPassword1A",
            ConfirmPassword = "TestPassword1A",
            DisplayName = "Test User"
        };

        var response = await client.PostAsJsonAsync("/api/v1/auth/register", registerRequest);
        response.EnsureSuccessStatusCode();

        var authResponse = await response.Content.ReadFromJsonAsync<AuthResponseData>();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authResponse!.Token);

        // Decode token to get user ID
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(authResponse.Token);
        var userIdClaim = jwt.Claims.First(c => c.Type == ClaimTypes.NameIdentifier);
        var userId = Guid.Parse(userIdClaim.Value);

        return (client, authResponse.Token, userId);
    }

    private string GenerateTestToken(Guid userId)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Email, $"test-{userId}@example.com"),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestSecret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: TestIssuer,
            audience: TestAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static void ReplaceService<T>(IServiceCollection services, T implementation) where T : class
    {
        RemoveServicesByType(services, typeof(T));
        services.AddSingleton(implementation);
    }

    private static void RemoveServicesByType(IServiceCollection services, Type serviceType)
    {
        var descriptors = services.Where(d => d.ServiceType == serviceType).ToList();
        foreach (var descriptor in descriptors)
            services.Remove(descriptor);
    }

    private record AuthResponseData(string Token, string Email, string? DisplayName, DateTime ExpiresAt);
}
