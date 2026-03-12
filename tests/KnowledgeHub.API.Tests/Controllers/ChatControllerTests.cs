using System.Security.Claims;
using FluentAssertions;
using KnowledgeHub.API.Controllers;
using KnowledgeHub.API.DTOs;
using KnowledgeHub.Core.Entities;
using KnowledgeHub.Core.Enums;
using KnowledgeHub.Core.Interfaces.Repositories;
using KnowledgeHub.Core.Interfaces.Services;
using KnowledgeHub.Core.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;

namespace KnowledgeHub.API.Tests.Controllers;

public class ChatControllerTests
{
    private readonly IChatService _chatService;
    private readonly IRepository<Message> _messageRepository;
    private readonly IRepository<Conversation> _conversationRepository;
    private readonly ChatController _controller;
    private readonly Guid _userId = Guid.NewGuid();

    public ChatControllerTests()
    {
        _chatService = Substitute.For<IChatService>();
        _messageRepository = Substitute.For<IRepository<Message>>();
        _conversationRepository = Substitute.For<IRepository<Conversation>>();
        _controller = new ChatController(_chatService, _messageRepository, _conversationRepository);
        SetUserContext(_userId);
    }

    [Fact]
    public async Task GetConversations_ReturnsUserConversations()
    {
        var conversations = new List<Conversation>
        {
            new() { Title = "Conversation 1", UserId = _userId },
            new() { Title = "Conversation 2", UserId = _userId }
        };
        _conversationRepository.GetPagedAsync(
                Arg.Any<PaginationParams>(),
                Arg.Any<System.Linq.Expressions.Expression<Func<Conversation, bool>>>(),
                Arg.Any<CancellationToken>())
            .Returns(new PagedResult<Conversation>
            {
                Items = conversations,
                TotalCount = 2,
                Page = 1,
                PageSize = 20
            });

        var result = await _controller.GetConversations(1, 20, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var pagedResult = okResult.Value.Should().BeOfType<PagedResult<ConversationDto>>().Subject;
        pagedResult.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task CreateConversation_WithTitle_ReturnsCreated()
    {
        var request = new CreateConversationRequest { Title = "My Conversation" };
        var conversation = new Conversation
        {
            Id = Guid.NewGuid(),
            Title = "My Conversation",
            UserId = _userId
        };

        _chatService.CreateConversationAsync(_userId, "My Conversation", Arg.Any<CancellationToken>())
            .Returns(conversation);

        var result = await _controller.CreateConversation(request, CancellationToken.None);

        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        var dto = createdResult.Value.Should().BeOfType<ConversationDto>().Subject;
        dto.Title.Should().Be("My Conversation");
    }

    [Fact]
    public async Task CreateConversation_WithoutTitle_ReturnsCreated()
    {
        var conversation = new Conversation
        {
            Id = Guid.NewGuid(),
            Title = "New Conversation",
            UserId = _userId
        };

        _chatService.CreateConversationAsync(_userId, null, Arg.Any<CancellationToken>())
            .Returns(conversation);

        var result = await _controller.CreateConversation(null, CancellationToken.None);

        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        var dto = createdResult.Value.Should().BeOfType<ConversationDto>().Subject;
        dto.Title.Should().Be("New Conversation");
    }

    [Fact]
    public async Task GetConversation_WhenExists_ReturnsConversation()
    {
        var conversationId = Guid.NewGuid();
        var conversation = new Conversation
        {
            Id = conversationId,
            Title = "Test Conversation",
            UserId = _userId
        };

        _chatService.GetConversationAsync(conversationId, _userId, Arg.Any<CancellationToken>())
            .Returns(conversation);

        var result = await _controller.GetConversation(conversationId, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = okResult.Value.Should().BeOfType<ConversationDto>().Subject;
        dto.Id.Should().Be(conversationId);
    }

    [Fact]
    public async Task GetConversation_WhenNotFound_ReturnsNotFound()
    {
        var conversationId = Guid.NewGuid();
        _chatService.GetConversationAsync(conversationId, _userId, Arg.Any<CancellationToken>())
            .Returns((Conversation?)null);

        var result = await _controller.GetConversation(conversationId, CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task SendMessage_WithValidRequest_ReturnsOk()
    {
        var conversationId = Guid.NewGuid();
        var request = new SendMessageRequest { Content = "Hello, how are you?" };
        var responseMessage = new Message
        {
            Id = Guid.NewGuid(),
            Content = "I'm doing well, thank you!",
            Role = MessageRole.Assistant,
            ConversationId = conversationId,
            Sources = new List<MessageSource>()
        };
        var sources = new List<MessageSource>();

        _chatService.SendMessageAsync(conversationId, _userId, request.Content, Arg.Any<CancellationToken>())
            .Returns((responseMessage, (IReadOnlyList<MessageSource>)sources));

        var result = await _controller.SendMessage(conversationId, request, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = okResult.Value.Should().BeOfType<ChatResponseDto>().Subject;
        dto.Message.Content.Should().Be("I'm doing well, thank you!");
        dto.Message.Role.Should().Be(MessageRole.Assistant);
    }

    [Fact]
    public async Task SendMessage_WithSources_ReturnsSources()
    {
        var conversationId = Guid.NewGuid();
        var chunkId = Guid.NewGuid();
        var request = new SendMessageRequest { Content = "What is RAG?" };
        var responseMessage = new Message
        {
            Id = Guid.NewGuid(),
            Content = "RAG stands for Retrieval-Augmented Generation.",
            Role = MessageRole.Assistant,
            ConversationId = conversationId,
            Sources = new List<MessageSource>()
        };
        var sources = new List<MessageSource>
        {
            new() { DocumentChunkId = chunkId, RelevanceScore = 0.95 }
        };

        _chatService.SendMessageAsync(conversationId, _userId, request.Content, Arg.Any<CancellationToken>())
            .Returns((responseMessage, (IReadOnlyList<MessageSource>)sources));

        var result = await _controller.SendMessage(conversationId, request, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = okResult.Value.Should().BeOfType<ChatResponseDto>().Subject;
        dto.Sources.Should().HaveCount(1);
        dto.Sources[0].DocumentChunkId.Should().Be(chunkId);
        dto.Sources[0].RelevanceScore.Should().Be(0.95);
    }

    [Fact]
    public async Task SendMessage_WhenConversationNotFound_ReturnsNotFound()
    {
        var conversationId = Guid.NewGuid();
        var request = new SendMessageRequest { Content = "Hello" };

        _chatService.SendMessageAsync(conversationId, _userId, request.Content, Arg.Any<CancellationToken>())
            .Returns<(Message, IReadOnlyList<MessageSource>)>(_ => throw new InvalidOperationException("Conversation not found"));

        var result = await _controller.SendMessage(conversationId, request, CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetMessages_WhenConversationExists_ReturnsMessages()
    {
        var conversationId = Guid.NewGuid();
        var conversation = new Conversation
        {
            Id = conversationId,
            Title = "Test",
            UserId = _userId
        };
        var messages = new List<Message>
        {
            new() { Content = "Hello", Role = MessageRole.User, ConversationId = conversationId, Sources = new List<MessageSource>() },
            new() { Content = "Hi there!", Role = MessageRole.Assistant, ConversationId = conversationId, Sources = new List<MessageSource>() }
        };

        _chatService.GetConversationAsync(conversationId, _userId, Arg.Any<CancellationToken>())
            .Returns(conversation);
        _messageRepository.GetPagedAsync(
                Arg.Any<PaginationParams>(),
                Arg.Any<System.Linq.Expressions.Expression<Func<Message, bool>>>(),
                Arg.Any<CancellationToken>())
            .Returns(new PagedResult<Message>
            {
                Items = messages,
                TotalCount = 2,
                Page = 1,
                PageSize = 50
            });

        var result = await _controller.GetMessages(conversationId, 1, 50, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var pagedResult = okResult.Value.Should().BeOfType<PagedResult<MessageDto>>().Subject;
        pagedResult.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetMessages_WhenConversationNotFound_ReturnsNotFound()
    {
        var conversationId = Guid.NewGuid();
        _chatService.GetConversationAsync(conversationId, _userId, Arg.Any<CancellationToken>())
            .Returns((Conversation?)null);

        var result = await _controller.GetMessages(conversationId, 1, 50, CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    private void SetUserContext(Guid userId)
    {
        var claims = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString())
        }, "TestAuth"));

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claims }
        };
    }
}
