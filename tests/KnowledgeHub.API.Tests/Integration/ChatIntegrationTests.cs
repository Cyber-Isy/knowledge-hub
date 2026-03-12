using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

namespace KnowledgeHub.API.Tests.Integration;

[Collection("Integration")]
public class ChatIntegrationTests
{
    private readonly CustomWebApplicationFactory _factory;

    public ChatIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateConversation_AppearsInList()
    {
        var (client, _, _) = await _factory.RegisterAndLoginAsync();

        var createResponse = await client.PostAsJsonAsync("/api/v1/chat/conversations", new
        {
            Title = "Test Conversation"
        });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await createResponse.Content.ReadFromJsonAsync<ConversationData>();
        created!.Title.Should().Be("Test Conversation");

        // Verify it appears in the conversations list
        var listResponse = await client.GetAsync("/api/v1/chat/conversations");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var pagedResult = await listResponse.Content.ReadFromJsonAsync<PagedConversationResult>();
        pagedResult!.Items.Should().Contain(c => c.Id == created.Id);
    }

    [Fact]
    public async Task CreateConversation_WithoutAuth_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/chat/conversations", new
        {
            Title = "Unauthorized"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetConversation_ById_ReturnsConversation()
    {
        var (client, _, _) = await _factory.RegisterAndLoginAsync();

        var createResponse = await client.PostAsJsonAsync("/api/v1/chat/conversations", new
        {
            Title = "Fetch Test"
        });
        var created = await createResponse.Content.ReadFromJsonAsync<ConversationData>();

        var getResponse = await client.GetAsync($"/api/v1/chat/conversations/{created!.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var conversation = await getResponse.Content.ReadFromJsonAsync<ConversationData>();
        conversation!.Title.Should().Be("Fetch Test");
    }

    private record ConversationData(Guid Id, string Title, bool IsArchived, DateTime CreatedAt, DateTime UpdatedAt);
    private record PagedConversationResult(List<ConversationData> Items, int TotalCount, int Page, int PageSize);
}
