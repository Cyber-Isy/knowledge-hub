using System.Runtime.CompilerServices;
using System.Security.Claims;
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
    private readonly ChatClient _chatClient;
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(
        IEmbeddingService embeddingService,
        IVectorSearchService vectorSearchService,
        IRepository<Conversation> conversationRepository,
        IRepository<Message> messageRepository,
        IRepository<MessageSource> messageSourceRepository,
        IOptions<ChatSettings> settings,
        IConfiguration configuration,
        ILogger<ChatHub> logger)
    {
        _embeddingService = embeddingService;
        _vectorSearchService = vectorSearchService;
        _conversationRepository = conversationRepository;
        _messageRepository = messageRepository;
        _messageSourceRepository = messageSourceRepository;
        _settings = settings.Value;
        _logger = logger;

        var azureOpenAI = configuration.GetSection("AzureOpenAI");
        var endpoint = azureOpenAI["Endpoint"] ?? string.Empty;
        var apiKey = azureOpenAI["ApiKey"] ?? string.Empty;
        var chatDeployment = azureOpenAI["ChatDeploymentName"] ?? "gpt-4o";

        var azureClient = new AzureOpenAIClient(
            new Uri(endpoint),
            new AzureKeyCredential(apiKey));
        _chatClient = azureClient.GetChatClient(chatDeployment);
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

        // Build chat messages
        var chatMessages = BuildChatMessages(content, searchResults);

        // Stream the response
        var fullResponse = new System.Text.StringBuilder();

        await foreach (var update in _chatClient.CompleteChatStreamingAsync(chatMessages, cancellationToken: cancellationToken))
        {
            foreach (var part in update.ContentUpdate)
            {
                fullResponse.Append(part.Text);
                yield return part.Text;
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

    private List<ChatMessage> BuildChatMessages(string userQuery, IReadOnlyList<VectorSearchResult> searchResults)
    {
        var messages = new List<ChatMessage>();

        var contextBuilder = new System.Text.StringBuilder();
        contextBuilder.AppendLine(_settings.SystemPrompt);
        contextBuilder.AppendLine();

        if (searchResults.Count > 0)
        {
            contextBuilder.AppendLine("Context from the knowledge base:");
            contextBuilder.AppendLine("---");

            foreach (var result in searchResults)
            {
                contextBuilder.AppendLine($"[Source: {result.FileName}]");
                contextBuilder.AppendLine(result.Content);
                contextBuilder.AppendLine("---");
            }
        }
        else
        {
            contextBuilder.AppendLine("No relevant documents were found in the knowledge base for this query.");
        }

        messages.Add(new SystemChatMessage(contextBuilder.ToString()));
        messages.Add(new UserChatMessage(userQuery));

        return messages;
    }
}
