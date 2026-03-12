using System.Text;
using System.Threading.Channels;
using KnowledgeHub.API.Configuration;
using KnowledgeHub.Core.Configuration;
using KnowledgeHub.Core.Entities;
using KnowledgeHub.Core.Interfaces.Repositories;
using KnowledgeHub.Core.Interfaces.Services;
using KnowledgeHub.Infrastructure.Data;
using KnowledgeHub.Infrastructure.Repositories;
using KnowledgeHub.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

namespace KnowledgeHub.API.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlite(configuration.GetConnectionString("DefaultConnection")));

        return services;
    }

    public static IServiceCollection AddIdentityServices(this IServiceCollection services)
    {
        services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
            {
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireNonAlphanumeric = false;
                options.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        return services;
    }

    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtSettings = configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
            ?? throw new InvalidOperationException("JWT settings are not configured.");

        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));

        services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtSettings.Issuer,
                    ValidAudience = jwtSettings.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret)),
                    ClockSkew = TimeSpan.Zero
                };

                // Allow SignalR to receive JWT from query string
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;
                        if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                        {
                            context.Token = accessToken;
                        }
                        return Task.CompletedTask;
                    }
                };
            });

        return services;
    }

    public static IServiceCollection AddSwaggerDocumentation(this IServiceCollection services)
    {
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "KnowledgeHub API",
                Version = "v1",
                Description = "AI-powered RAG Chat Assistant with Knowledge Base"
            });

            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Enter your JWT token"
            });

            options.AddSecurityRequirement(doc =>
            {
                var schemeRef = new OpenApiSecuritySchemeReference("Bearer", doc);
                return new OpenApiSecurityRequirement
                {
                    { schemeRef, new List<string>() }
                };
            });
        });

        return services;
    }

    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Repositories
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IDocumentRepository, DocumentRepository>();
        services.AddSingleton<IFileStorageService>(new LocalFileStorageService("uploads"));

        // Text extraction
        services.AddSingleton<IDocumentTextExtractor, PdfTextExtractor>();
        services.AddSingleton<IDocumentTextExtractor, DocxTextExtractor>();
        services.AddSingleton<IDocumentTextExtractor, PlainTextExtractor>();
        services.AddSingleton<DocumentTextExtractorFactory>();

        // Text chunking
        services.AddSingleton<ITextChunker, RecursiveTextChunker>();

        // Azure OpenAI Embeddings
        var azureOpenAI = configuration.GetSection("AzureOpenAI");
        var endpoint = azureOpenAI["Endpoint"] ?? string.Empty;
        var apiKey = azureOpenAI["ApiKey"] ?? string.Empty;
        var embeddingDeployment = azureOpenAI["EmbeddingDeploymentName"] ?? "text-embedding-3-small";

        services.AddSingleton<IEmbeddingService>(sp =>
            new AzureOpenAIEmbeddingService(
                endpoint, apiKey, embeddingDeployment,
                sp.GetRequiredService<ILogger<AzureOpenAIEmbeddingService>>()));

        // Azure AI Search (vector store)
        var azureSearch = configuration.GetSection("AzureAISearch");
        var searchEndpoint = azureSearch["Endpoint"] ?? string.Empty;
        var searchApiKey = azureSearch["ApiKey"] ?? string.Empty;
        var indexName = azureSearch["IndexName"] ?? "knowledgehub-chunks";

        services.AddSingleton<IVectorSearchService>(sp =>
            new AzureAISearchService(
                searchEndpoint, searchApiKey, indexName,
                sp.GetRequiredService<ILogger<AzureAISearchService>>()));

        // Document processing pipeline
        services.AddScoped<IDocumentProcessingService, DocumentProcessingService>();
        services.AddSingleton(Channel.CreateUnbounded<Guid>());
        services.AddSingleton<DocumentProcessingBackgroundService>();
        services.AddHostedService(sp => sp.GetRequiredService<DocumentProcessingBackgroundService>());

        // Chat
        services.Configure<ChatSettings>(configuration.GetSection(ChatSettings.SectionName));
        var chatDeployment = azureOpenAI["ChatDeploymentName"] ?? "gpt-4o";
        services.AddScoped<IChatService>(sp =>
            new ChatService(
                endpoint, apiKey, chatDeployment,
                sp.GetRequiredService<IEmbeddingService>(),
                sp.GetRequiredService<IVectorSearchService>(),
                sp.GetRequiredService<IRepository<Conversation>>(),
                sp.GetRequiredService<IRepository<Message>>(),
                sp.GetRequiredService<IRepository<MessageSource>>(),
                sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ChatSettings>>(),
                sp.GetRequiredService<ILogger<ChatService>>()));

        // SignalR
        services.AddSignalR();

        return services;
    }

    public static IServiceCollection AddCorsPolicies(this IServiceCollection services)
    {
        services.AddCors(options =>
        {
            options.AddPolicy("AllowAngular", policy =>
            {
                policy.WithOrigins("http://localhost:4200")
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });
        });

        return services;
    }
}
