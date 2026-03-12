using System.Security.Claims;
using KnowledgeHub.API.DTOs;
using KnowledgeHub.Core.Entities;
using KnowledgeHub.Core.Interfaces.Repositories;
using KnowledgeHub.Core.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KnowledgeHub.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ChatController : ControllerBase
{
    private readonly IChatService _chatService;
    private readonly IRepository<Message> _messageRepository;

    public ChatController(IChatService chatService, IRepository<Message> messageRepository)
    {
        _chatService = chatService;
        _messageRepository = messageRepository;
    }

    [HttpPost("conversations")]
    public async Task<ActionResult<ConversationDto>> CreateConversation(
        [FromBody] CreateConversationRequest? request, CancellationToken ct)
    {
        var userId = GetUserId();
        var conversation = await _chatService.CreateConversationAsync(userId, request?.Title, ct);
        return CreatedAtAction(nameof(GetConversation), new { id = conversation.Id }, ToDto(conversation));
    }

    [HttpGet("conversations")]
    public async Task<ActionResult<IEnumerable<ConversationDto>>> GetConversations(CancellationToken ct)
    {
        var userId = GetUserId();
        var conversations = await _chatService.GetConversationsAsync(userId, ct);
        return Ok(conversations.Select(ToDto));
    }

    [HttpGet("conversations/{id:guid}")]
    public async Task<ActionResult<ConversationDto>> GetConversation(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        var conversation = await _chatService.GetConversationAsync(id, userId, ct);
        if (conversation is null)
            return NotFound();

        return Ok(ToDto(conversation));
    }

    [HttpGet("conversations/{id:guid}/messages")]
    public async Task<ActionResult<IEnumerable<MessageDto>>> GetMessages(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        var conversation = await _chatService.GetConversationAsync(id, userId, ct);
        if (conversation is null)
            return NotFound();

        var messages = await _messageRepository.FindAsync(m => m.ConversationId == id, ct);
        return Ok(messages.OrderBy(m => m.CreatedAt).Select(ToMessageDto));
    }

    [HttpPost("conversations/{conversationId:guid}/messages")]
    public async Task<ActionResult<ChatResponseDto>> SendMessage(
        Guid conversationId, [FromBody] SendMessageRequest request, CancellationToken ct)
    {
        var userId = GetUserId();

        try
        {
            var (response, sources) = await _chatService.SendMessageAsync(conversationId, userId, request.Content, ct);

            var responseDto = new ChatResponseDto
            {
                Message = ToMessageDto(response),
                Sources = sources.Select(s => new MessageSourceDto
                {
                    DocumentChunkId = s.DocumentChunkId,
                    RelevanceScore = s.RelevanceScore
                }).ToList()
            };

            return Ok(responseDto);
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
    }

    private Guid GetUserId()
        => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private static ConversationDto ToDto(Conversation conversation) => new()
    {
        Id = conversation.Id,
        Title = conversation.Title,
        IsArchived = conversation.IsArchived,
        CreatedAt = conversation.CreatedAt,
        UpdatedAt = conversation.UpdatedAt
    };

    private static MessageDto ToMessageDto(Message message) => new()
    {
        Id = message.Id,
        Content = message.Content,
        Role = message.Role,
        TokensUsed = message.TokensUsed,
        CreatedAt = message.CreatedAt,
        Sources = message.Sources?.Select(s => new MessageSourceDto
        {
            DocumentChunkId = s.DocumentChunkId,
            RelevanceScore = s.RelevanceScore
        }).ToList() ?? []
    };
}
