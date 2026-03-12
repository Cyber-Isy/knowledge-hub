using System.Text;
using Azure;
using Azure.AI.OpenAI;
using KnowledgeHub.Core.Configuration;
using KnowledgeHub.Core.Entities;
using KnowledgeHub.Core.Enums;
using KnowledgeHub.Core.Interfaces.Repositories;
using KnowledgeHub.Core.Interfaces.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;

namespace KnowledgeHub.Infrastructure.Services;

public class ChatService : IChatService
{
    private readonly ChatClient _chatClient;
    private readonly IEmbeddingService _embeddingService;
    private readonly IVectorSearchService _vectorSearchService;
    private readonly IRepository<Conversation> _conversationRepository;
    private readonly IRepository<Message> _messageRepository;
    private readonly IRepository<MessageSource> _messageSourceRepository;
    private readonly ChatSettings _settings;
    private readonly ILogger<ChatService> _logger;

    public ChatService(
        string endpoint,
        string apiKey,
        string deploymentName,
        IEmbeddingService embeddingService,
        IVectorSearchService vectorSearchService,
        IRepository<Conversation> conversationRepository,
        IRepository<Message> messageRepository,
        IRepository<MessageSource> messageSourceRepository,
        IOptions<ChatSettings> settings,
        ILogger<ChatService> logger)
    {
        var azureClient = new AzureOpenAIClient(
            new Uri(endpoint),
            new AzureKeyCredential(apiKey));

        _chatClient = azureClient.GetChatClient(deploymentName);
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
        var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(content, ct);
        var searchResults = await _vectorSearchService.SearchAsync(queryEmbedding, _settings.MaxContextChunks, ct);

        _logger.LogDebug("Found {Count} relevant chunks for query", searchResults.Count);

        // Build messages for the chat completion
        var chatMessages = BuildChatMessages(content, searchResults);

        // Call Azure OpenAI
        var completion = await _chatClient.CompleteChatAsync(chatMessages, cancellationToken: ct);
        var responseContent = completion.Value.Content[0].Text;
        var tokensUsed = completion.Value.Usage.TotalTokenCount;

        // Save assistant response
        var assistantMessage = new Message
        {
            Content = responseContent,
            Role = MessageRole.Assistant,
            ConversationId = conversationId,
            TokensUsed = tokensUsed
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

        _logger.LogInformation(
            "Chat response generated for conversation {ConversationId}, {TokensUsed} tokens used",
            conversationId, tokensUsed);

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

    private List<ChatMessage> BuildChatMessages(string userQuery, IReadOnlyList<VectorSearchResult> searchResults)
    {
        var messages = new List<ChatMessage>();

        // System prompt with context
        var contextBuilder = new StringBuilder();
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
