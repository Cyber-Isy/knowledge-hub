using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure;
using Azure.AI.OpenAI;
using KnowledgeHub.Core.Configuration;
using KnowledgeHub.Core.Entities;
using KnowledgeHub.Core.Enums;
using KnowledgeHub.Core.Interfaces.Repositories;
using KnowledgeHub.Core.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using OpenAI.Chat;

namespace KnowledgeHub.API.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly IEmbeddingService _embeddingService;
    private readonly IVectorSearchService _vectorSearchService;
    private readonly IRepository<Conversation> _conversationRepository;
    private readonly IRepository<Message> _messageRepository;
    private readonly IRepository<MessageSource> _messageSourceRepository;
    private readonly ChatSettings _settings;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory? _httpClientFactory;
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(
        IEmbeddingService embeddingService,
        IVectorSearchService vectorSearchService,
        IRepository<Conversation> conversationRepository,
        IRepository<Message> messageRepository,
        IRepository<MessageSource> messageSourceRepository,
        IOptions<ChatSettings> settings,
        IConfiguration configuration,
        ILogger<ChatHub> logger,
        IHttpClientFactory? httpClientFactory = null)
    {
        _embeddingService = embeddingService;
        _vectorSearchService = vectorSearchService;
        _conversationRepository = conversationRepository;
        _messageRepository = messageRepository;
        _messageSourceRepository = messageSourceRepository;
        _settings = settings.Value;
        _configuration = configuration;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    public async IAsyncEnumerable<string> StreamMessage(
        Guid conversationId,
        string content,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        var conversation = await _conversationRepository.GetByIdAsync(conversationId, cancellationToken);
        if (conversation is null || conversation.UserId != userId)
        {
            yield break;
        }

        // Save user message
        var userMessage = new Message
        {
            Content = content,
            Role = MessageRole.User,
            ConversationId = conversationId
        };
        await _messageRepository.AddAsync(userMessage, cancellationToken);

        // Embed query and search
        var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(content, cancellationToken);
        var searchResults = await _vectorSearchService.SearchAsync(
            queryEmbedding, _settings.MaxContextChunks, cancellationToken);

        // Send source information to client
        await Clients.Caller.SendAsync("ReceiveSources", searchResults.Select(s => new
        {
            s.DocumentChunkId,
            s.FileName,
            s.Score
        }), cancellationToken);

        // Build system prompt with context
        var systemPrompt = BuildSystemPrompt(searchResults);

        // Stream from the appropriate provider
        var fullResponse = new StringBuilder();
        var useOllama = _configuration.GetValue<bool>("Ollama:Enabled");

        if (useOllama)
        {
            await foreach (var token in StreamFromOllamaAsync(systemPrompt, content, cancellationToken))
            {
                fullResponse.Append(token);
                yield return token;
            }
        }
        else
        {
            await foreach (var token in StreamFromAzureAsync(systemPrompt, content, cancellationToken))
            {
                fullResponse.Append(token);
                yield return token;
            }
        }

        // Save assistant message
        var assistantMessage = new Message
        {
            Content = fullResponse.ToString(),
            Role = MessageRole.Assistant,
            ConversationId = conversationId
        };
        await _messageRepository.AddAsync(assistantMessage, cancellationToken);

        // Save source references
        foreach (var result in searchResults)
        {
            var source = new MessageSource
            {
                MessageId = assistantMessage.Id,
                DocumentChunkId = result.DocumentChunkId,
                RelevanceScore = result.Score
            };
            await _messageSourceRepository.AddAsync(source, cancellationToken);
        }

        // Update conversation title on first message
        if (conversation.Title == "New Conversation")
        {
            conversation.Title = content.Length > 50 ? content[..50] + "..." : content;
            await _conversationRepository.UpdateAsync(conversation, cancellationToken);
        }

        // Notify client that streaming is complete
        await Clients.Caller.SendAsync("StreamCompleted", assistantMessage.Id, cancellationToken);

        _logger.LogInformation(
            "Streamed response for conversation {ConversationId}", conversationId);
    }

    private async IAsyncEnumerable<string> StreamFromOllamaAsync(
        string systemPrompt,
        string userContent,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var ollamaModel = _configuration["Ollama:ChatModel"] ?? "llama3.1:8b";
        var ollamaBaseUrl = _configuration["Ollama:BaseUrl"] ?? "http://localhost:11434";

        var httpClient = _httpClientFactory?.CreateClient("Ollama")
            ?? new HttpClient { BaseAddress = new Uri(ollamaBaseUrl), Timeout = TimeSpan.FromMinutes(5) };

        var request = new
        {
            model = ollamaModel,
            stream = true,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userContent }
            }
        };

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "api/chat")
        {
            Content = JsonContent.Create(request)
        };

        var response = await httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrEmpty(line)) continue;

            var chunk = JsonSerializer.Deserialize<OllamaStreamChunk>(line);
            if (chunk?.Done == true) break;
            if (chunk?.Message?.Content is not null)
            {
                yield return chunk.Message.Content;
            }
        }
    }

    private async IAsyncEnumerable<string> StreamFromAzureAsync(
        string systemPrompt,
        string userContent,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var azureOpenAI = _configuration.GetSection("AzureOpenAI");
        var endpoint = azureOpenAI["Endpoint"] ?? string.Empty;
        var apiKey = azureOpenAI["ApiKey"] ?? string.Empty;
        var chatDeployment = azureOpenAI["ChatDeploymentName"] ?? "gpt-4o";

        var azureClient = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
        var chatClient = azureClient.GetChatClient(chatDeployment);

        var chatMessages = new List<ChatMessage>
        {
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(userContent)
        };

        await foreach (var update in chatClient.CompleteChatStreamingAsync(chatMessages, cancellationToken: cancellationToken))
        {
            foreach (var part in update.ContentUpdate)
            {
                yield return part.Text;
            }
        }
    }

    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        _logger.LogInformation("User {UserId} connected to ChatHub", userId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId();
        _logger.LogInformation("User {UserId} disconnected from ChatHub", userId);
        await base.OnDisconnectedAsync(exception);
    }

    private Guid GetUserId()
        => Guid.Parse(Context.User!.FindFirstValue(ClaimTypes.NameIdentifier)!);

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

    // Ollama streaming response models
    private sealed class OllamaStreamChunk
    {
        [JsonPropertyName("message")]
        public OllamaStreamMessage? Message { get; set; }

        [JsonPropertyName("done")]
        public bool Done { get; set; }
    }

    private sealed class OllamaStreamMessage
    {
        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }
}
