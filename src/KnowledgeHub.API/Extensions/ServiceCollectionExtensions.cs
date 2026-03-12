using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Threading.Channels;
using System.Threading.RateLimiting;
using Asp.Versioning;
using Azure.Storage.Blobs;
using FluentValidation;
using FluentValidation.AspNetCore;
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
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

namespace KnowledgeHub.API.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment environment)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

        services.AddDbContext<ApplicationDbContext>(options =>
        {
            if (environment.IsDevelopment())
            {
                options.UseSqlite(connectionString);
            }
            else
            {
                options.UseSqlServer(connectionString, sqlOptions =>
                {
                    sqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(30),
                        errorNumbersToAdd: null);
                });
            }
        });

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
                    ClockSkew = TimeSpan.Zero,
                    RoleClaimType = ClaimTypes.Role
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

            // Include XML comments in Swagger if available
            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath))
            {
                options.IncludeXmlComments(xmlPath);
            }
        });

        return services;
    }

    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment environment)
    {
        // Repositories
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IDocumentRepository, DocumentRepository>();

        // Data seeding
        services.AddScoped<IDataSeeder, DataSeeder>();

        // File storage: local disk in Development/Testing, Azure Blob Storage in Production
        var blobConnectionString = configuration["AzureBlobStorage:ConnectionString"];
        if (environment.IsDevelopment() || string.IsNullOrEmpty(blobConnectionString))
        {
            services.AddSingleton<IFileStorageService>(new LocalFileStorageService("uploads"));
        }
        else
        {
            var containerName = configuration["AzureBlobStorage:ContainerName"] ?? "documents";
            services.AddSingleton(new BlobServiceClient(blobConnectionString));
            services.AddSingleton<IFileStorageService>(sp =>
                new AzureBlobStorageService(
                    sp.GetRequiredService<BlobServiceClient>(),
                    containerName));
        }

        // Text extraction
        services.AddSingleton<IDocumentTextExtractor, PdfTextExtractor>();
        services.AddSingleton<IDocumentTextExtractor, DocxTextExtractor>();
        services.AddSingleton<IDocumentTextExtractor, PlainTextExtractor>();
        services.AddSingleton<DocumentTextExtractorFactory>();

        // Text chunking
        services.AddSingleton<ITextChunker, RecursiveTextChunker>();

        // AI provider: Ollama (development) or Azure OpenAI (production)
        var useOllama = configuration.GetValue<bool>("Ollama:Enabled");

        if (useOllama)
        {
            // Ollama — local AI (free, no API keys)
            var ollamaSettings = configuration.GetSection(OllamaSettings.SectionName).Get<OllamaSettings>()
                ?? new OllamaSettings();

            services.AddHttpClient("Ollama", client =>
            {
                client.BaseAddress = new Uri(ollamaSettings.BaseUrl);
                client.Timeout = TimeSpan.FromMinutes(5); // LLM responses can be slow
            });

            services.AddSingleton<IEmbeddingService>(sp =>
                new OllamaEmbeddingService(
                    sp.GetRequiredService<IHttpClientFactory>().CreateClient("Ollama"),
                    ollamaSettings.EmbeddingModel,
                    sp.GetRequiredService<ILogger<OllamaEmbeddingService>>()));

            // In-memory vector search (development only)
            services.AddSingleton<IVectorSearchService>(sp =>
                new InMemoryVectorSearchService(
                    sp.GetRequiredService<ILogger<InMemoryVectorSearchService>>()));
        }
        else
        {
            // Azure OpenAI — production AI
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
        }

        // Document processing pipeline
        services.AddScoped<IDocumentProcessingService, DocumentProcessingService>();
        services.AddSingleton(Channel.CreateUnbounded<Guid>());
        services.AddSingleton<DocumentProcessingBackgroundService>();
        services.AddHostedService(sp => sp.GetRequiredService<DocumentProcessingBackgroundService>());

        // Chat
        services.Configure<ChatSettings>(configuration.GetSection(ChatSettings.SectionName));

        if (useOllama)
        {
            var ollamaSettings = configuration.GetSection(OllamaSettings.SectionName).Get<OllamaSettings>()
                ?? new OllamaSettings();

            services.AddScoped<IChatService>(sp =>
                new OllamaChatService(
                    sp.GetRequiredService<IHttpClientFactory>().CreateClient("Ollama"),
                    ollamaSettings.ChatModel,
                    sp.GetRequiredService<IEmbeddingService>(),
                    sp.GetRequiredService<IVectorSearchService>(),
                    sp.GetRequiredService<IRepository<Conversation>>(),
                    sp.GetRequiredService<IRepository<Message>>(),
                    sp.GetRequiredService<IRepository<MessageSource>>(),
                    sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ChatSettings>>(),
                    sp.GetRequiredService<ILogger<OllamaChatService>>()));
        }
        else
        {
            var azureOpenAI = configuration.GetSection("AzureOpenAI");
            var endpoint = azureOpenAI["Endpoint"] ?? string.Empty;
            var apiKey = azureOpenAI["ApiKey"] ?? string.Empty;
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
        }

        // SignalR
        services.AddSignalR();

        return services;
    }

    public static IServiceCollection AddCorsPolicies(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddCors(options =>
        {
            options.AddPolicy("AllowAngular", policy =>
            {
                var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                    ?? ["http://localhost:4200"];

                policy.WithOrigins(origins)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });
        });

        return services;
    }

    public static IServiceCollection AddFluentValidationServices(this IServiceCollection services)
    {
        services.AddFluentValidationAutoValidation();
        services.AddValidatorsFromAssemblyContaining<Program>();

        return services;
    }

    public static IServiceCollection AddRateLimitingPolicies(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.OnRejected = async (context, cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)retryAfter.TotalSeconds).ToString();
                }

                context.HttpContext.Response.ContentType = "application/json";
                await context.HttpContext.Response.WriteAsJsonAsync(new
                {
                    Title = "Too Many Requests",
                    Status = 429,
                    Detail = "Rate limit exceeded. Please try again later."
                }, cancellationToken);
            };

            // Auth endpoints: 10 requests/minute, partitioned by IP
            options.AddPolicy("auth", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    }));

            // Upload endpoints: 20 requests/hour, partitioned by user claim
            options.AddPolicy("upload", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "anonymous",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 20,
                        Window = TimeSpan.FromHours(1),
                        QueueLimit = 0
                    }));

            // Chat endpoints: 60 requests/hour, partitioned by user claim
            options.AddPolicy("chat", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "anonymous",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 60,
                        Window = TimeSpan.FromHours(1),
                        QueueLimit = 0
                    }));
        });

        return services;
    }

    public static IServiceCollection AddApiVersioningServices(this IServiceCollection services)
    {
        services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new ApiVersion(1, 0);
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.ReportApiVersions = true;
            options.ApiVersionReader = new UrlSegmentApiVersionReader();
        }).AddApiExplorer(options =>
        {
            options.GroupNameFormat = "'v'VVV";
            options.SubstituteApiVersionInUrl = true;
        });

        return services;
    }
}
