using System.Security.Claims;
using Asp.Versioning;
using KnowledgeHub.API.DTOs;
using KnowledgeHub.Core.Entities;
using KnowledgeHub.Core.Interfaces.Repositories;
using KnowledgeHub.Core.Interfaces.Services;
using KnowledgeHub.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace KnowledgeHub.API.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("1.0")]
[Authorize]
[EnableRateLimiting("chat")]
public class ChatController : ControllerBase
{
    private readonly IChatService _chatService;
    private readonly IRepository<Message> _messageRepository;
    private readonly IRepository<Conversation> _conversationRepository;

    public ChatController(
        IChatService chatService,
        IRepository<Message> messageRepository,
        IRepository<Conversation> conversationRepository)
    {
        _chatService = chatService;
        _messageRepository = messageRepository;
        _conversationRepository = conversationRepository;
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
    public async Task<ActionResult<PagedResult<ConversationDto>>> GetConversations(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var userId = GetUserId();
        var pagination = new PaginationParams { Page = page, PageSize = pageSize };

        var pagedConversations = await _conversationRepository.GetPagedAsync(
            pagination, c => c.UserId == userId && !c.IsArchived, ct);

        var result = new PagedResult<ConversationDto>
        {
            Items = pagedConversations.Items.Select(ToDto).ToList(),
            TotalCount = pagedConversations.TotalCount,
            Page = pagedConversations.Page,
            PageSize = pagedConversations.PageSize
        };

        return Ok(result);
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
    public async Task<ActionResult<PagedResult<MessageDto>>> GetMessages(
        Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        var userId = GetUserId();
        var conversation = await _chatService.GetConversationAsync(id, userId, ct);
        if (conversation is null)
            return NotFound();

        var pagination = new PaginationParams { Page = page, PageSize = pageSize };
        var pagedMessages = await _messageRepository.GetPagedAsync(
            pagination, m => m.ConversationId == id, ct);

        var result = new PagedResult<MessageDto>
        {
            Items = pagedMessages.Items.Select(ToMessageDto).ToList(),
            TotalCount = pagedMessages.TotalCount,
            Page = pagedMessages.Page,
            PageSize = pagedMessages.PageSize
        };

        return Ok(result);
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
