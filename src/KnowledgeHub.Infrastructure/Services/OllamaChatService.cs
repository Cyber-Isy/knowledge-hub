using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using KnowledgeHub.Core.Configuration;
using KnowledgeHub.Core.Entities;
using KnowledgeHub.Core.Enums;
using KnowledgeHub.Core.Interfaces.Repositories;
using KnowledgeHub.Core.Interfaces.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KnowledgeHub.Infrastructure.Services;

public class OllamaChatService : IChatService
{
    private readonly HttpClient _httpClient;
    private readonly string _model;
    private readonly IEmbeddingService _embeddingService;
    private readonly IVectorSearchService _vectorSearchService;
    private readonly IRepository<Conversation> _conversationRepository;
    private readonly IRepository<Message> _messageRepository;
    private readonly IRepository<MessageSource> _messageSourceRepository;
    private readonly ChatSettings _settings;
    private readonly ILogger<OllamaChatService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public OllamaChatService(
        HttpClient httpClient,
        string model,
        IEmbeddingService embeddingService,
        IVectorSearchService vectorSearchService,
        IRepository<Conversation> conversationRepository,
        IRepository<Message> messageRepository,
        IRepository<MessageSource> messageSourceRepository,
        IOptions<ChatSettings> settings,
        ILogger<OllamaChatService> logger)
    {
        _httpClient = httpClient;
        _model = model;
        _embeddingService = embeddingService;
        _vectorSearchService = vectorSearchService;
        _conversationRepository = conversationRepository;
        _messageRepository = messageRepository;
        _messageSourceRepository = messageSourceRepository;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<(Message Response, IReadOnlyList<MessageSource> Sources)> SendMessageAsync(
        Guid conversationId, Guid userId, string content, CancellationToken ct = default)
    {
        var conversation = await _conversationRepository.GetByIdAsync(conversationId, ct);
        if (conversation is null || conversation.UserId != userId)
            throw new InvalidOperationException("Conversation not found.");

        // Save user message
        var userMessage = new Message
        {
            Content = content,
            Role = MessageRole.User,
            ConversationId = conversationId
        };
        await _messageRepository.AddAsync(userMessage, ct);

        // Embed user query and search for relevant chunks
        var embeddingStopwatch = Stopwatch.StartNew();
        var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(content, ct);
        embeddingStopwatch.Stop();
        _logger.LogInformation("Embedding generation completed in {ElapsedMs}ms", embeddingStopwatch.ElapsedMilliseconds);

        var searchStopwatch = Stopwatch.StartNew();
        var searchResults = await _vectorSearchService.SearchAsync(queryEmbedding, _settings.MaxContextChunks, ct);
        searchStopwatch.Stop();
        _logger.LogInformation("Vector search completed in {ElapsedMs}ms, found {Count} results",
            searchStopwatch.ElapsedMilliseconds, searchResults.Count);

        // Build prompt with context
        var systemPrompt = BuildSystemPrompt(searchResults);

        // Call Ollama
        var completionStopwatch = Stopwatch.StartNew();
        var request = new OllamaChatRequest
        {
            Model = _model,
            Stream = false,
            Messages =
            [
                new OllamaChatMessage { Role = "system", Content = systemPrompt },
                new OllamaChatMessage { Role = "user", Content = content }
            ]
        };

        var response = await _httpClient.PostAsJsonAsync("api/chat", request, JsonOptions, ct);
        response.EnsureSuccessStatusCode();

        var chatResponse = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(JsonOptions, ct);
        completionStopwatch.Stop();

        var responseContent = chatResponse?.Message?.Content ?? "Sorry, I could not generate a response.";
        _logger.LogInformation("Chat completion finished in {ElapsedMs}ms via Ollama ({Model})",
            completionStopwatch.ElapsedMilliseconds, _model);

        // Save assistant response
        var assistantMessage = new Message
        {
            Content = responseContent,
            Role = MessageRole.Assistant,
            ConversationId = conversationId,
            TokensUsed = chatResponse?.EvalCount ?? 0
        };
        await _messageRepository.AddAsync(assistantMessage, ct);

        // Save source references
        var sources = new List<MessageSource>();
        foreach (var result in searchResults)
        {
            var source = new MessageSource
            {
                MessageId = assistantMessage.Id,
                DocumentChunkId = result.DocumentChunkId,
                RelevanceScore = result.Score
            };
            await _messageSourceRepository.AddAsync(source, ct);
            sources.Add(source);
        }

        // Update conversation title on first message
        if (conversation.Title == "New Conversation")
        {
            conversation.Title = content.Length > 50 ? content[..50] + "..." : content;
            await _conversationRepository.UpdateAsync(conversation, ct);
        }

        return (assistantMessage, sources);
    }

    public async Task<Conversation> CreateConversationAsync(Guid userId, string? title = null, CancellationToken ct = default)
    {
        var conversation = new Conversation
        {
            UserId = userId,
            Title = title ?? "New Conversation"
        };

        return await _conversationRepository.AddAsync(conversation, ct);
    }

    public async Task<Conversation?> GetConversationAsync(Guid conversationId, Guid userId, CancellationToken ct = default)
    {
        var conversation = await _conversationRepository.GetByIdAsync(conversationId, ct);
        if (conversation is null || conversation.UserId != userId)
            return null;

        return conversation;
    }

    public async Task<IReadOnlyList<Conversation>> GetConversationsAsync(Guid userId, CancellationToken ct = default)
    {
        return await _conversationRepository.FindAsync(c => c.UserId == userId && !c.IsArchived, ct);
    }

    private string BuildSystemPrompt(IReadOnlyList<VectorSearchResult> searchResults)
    {
        var sb = new StringBuilder();
        sb.AppendLine(_settings.SystemPrompt);
        sb.AppendLine();

        if (searchResults.Count > 0)
        {
            sb.AppendLine("Context from the knowledge base:");
            sb.AppendLine("---");

            foreach (var result in searchResults)
            {
                sb.AppendLine($"[Source: {result.FileName}]");
                sb.AppendLine(result.Content);
                sb.AppendLine("---");
            }
        }
        else
        {
            sb.AppendLine("No relevant documents were found in the knowledge base for this query.");
        }

        return sb.ToString();
    }

    // Ollama API models

    private sealed class OllamaChatRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("messages")]
        public List<OllamaChatMessage> Messages { get; set; } = [];

        [JsonPropertyName("stream")]
        public bool Stream { get; set; }
    }

    private sealed class OllamaChatMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
    }

    private sealed class OllamaChatResponse
    {
        [JsonPropertyName("message")]
        public OllamaChatMessage? Message { get; set; }

        [JsonPropertyName("eval_count")]
        public int? EvalCount { get; set; }
    }
}
