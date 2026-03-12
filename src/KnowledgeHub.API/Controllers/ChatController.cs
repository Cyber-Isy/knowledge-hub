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

/// <summary>
/// Manages chat conversations and messages with RAG-powered responses.
/// </summary>
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

    /// <summary>
    /// Creates a new chat conversation.
    /// </summary>
    /// <param name="request">Optional request body containing a title for the conversation.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The newly created conversation.</returns>
    /// <response code="201">Conversation created successfully.</response>
    /// <response code="401">The request is not authenticated.</response>
    [HttpPost("conversations")]
    [ProducesResponseType(typeof(ConversationDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ConversationDto>> CreateConversation(
        [FromBody] CreateConversationRequest? request, CancellationToken ct)
    {
        var userId = GetUserId();
        var conversation = await _chatService.CreateConversationAsync(userId, request?.Title, ct);
        return CreatedAtAction(nameof(GetConversation), new { id = conversation.Id }, ToDto(conversation));
    }

    /// <summary>
    /// Returns a paginated list of conversations for the current user.
    /// </summary>
    /// <param name="page">Page number (1-based). Defaults to 1.</param>
    /// <param name="pageSize">Number of items per page. Defaults to 20.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A paginated list of non-archived conversations.</returns>
    /// <response code="200">Returns the conversation list.</response>
    /// <response code="401">The request is not authenticated.</response>
    [HttpGet("conversations")]
    [ProducesResponseType(typeof(PagedResult<ConversationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
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

    /// <summary>
    /// Returns a single conversation by its unique identifier.
    /// </summary>
    /// <param name="id">The conversation ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The conversation details.</returns>
    /// <response code="200">Returns the conversation.</response>
    /// <response code="401">The request is not authenticated.</response>
    /// <response code="404">Conversation not found or does not belong to the current user.</response>
    [HttpGet("conversations/{id:guid}")]
    [ProducesResponseType(typeof(ConversationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConversationDto>> GetConversation(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        var conversation = await _chatService.GetConversationAsync(id, userId, ct);
        if (conversation is null)
            return NotFound();

        return Ok(ToDto(conversation));
    }

    /// <summary>
    /// Returns a paginated list of messages for a conversation.
    /// </summary>
    /// <param name="id">The conversation ID.</param>
    /// <param name="page">Page number (1-based). Defaults to 1.</param>
    /// <param name="pageSize">Number of items per page. Defaults to 50.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A paginated list of messages with source citations.</returns>
    /// <response code="200">Returns the message list.</response>
    /// <response code="401">The request is not authenticated.</response>
    /// <response code="404">Conversation not found or does not belong to the current user.</response>
    [HttpGet("conversations/{id:guid}/messages")]
    [ProducesResponseType(typeof(PagedResult<MessageDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
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
            Items = pagedMessages.Items.OrderBy(m => m.CreatedAt).Select(ToMessageDto).ToList(),
            TotalCount = pagedMessages.TotalCount,
            Page = pagedMessages.Page,
            PageSize = pagedMessages.PageSize
        };

        return Ok(result);
    }

    /// <summary>
    /// Sends a message in a conversation and returns the RAG-powered response.
    /// </summary>
    /// <param name="conversationId">The conversation ID.</param>
    /// <param name="request">The message content to send.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The assistant's response with source citations.</returns>
    /// <response code="200">Returns the assistant response and source documents.</response>
    /// <response code="401">The request is not authenticated.</response>
    /// <response code="404">Conversation not found or does not belong to the current user.</response>
    /// <response code="429">Chat rate limit exceeded.</response>
    [HttpPost("conversations/{conversationId:guid}/messages")]
    [ProducesResponseType(typeof(ChatResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
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
