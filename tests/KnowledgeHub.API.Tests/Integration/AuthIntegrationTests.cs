using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

namespace KnowledgeHub.API.Tests.Integration;

[Collection("Integration")]
public class AuthIntegrationTests
{
    private readonly CustomWebApplicationFactory _factory;

    public AuthIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Register_Login_AccessProtectedEndpoint_Succeeds()
    {
        var (client, token, userId) = await _factory.RegisterAndLoginAsync();

        var meResponse = await client.GetAsync("/api/v1/auth/me");

        meResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await meResponse.Content.ReadFromJsonAsync<UserInfoData>();
        content!.Email.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task AccessProtectedEndpoint_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();
        var email = $"duplicate-{Guid.NewGuid()}@example.com";

        var registerRequest = new
        {
            Email = email,
            Password = "TestPassword1A",
            ConfirmPassword = "TestPassword1A",
            DisplayName = "User One"
        };

        var firstResponse = await client.PostAsJsonAsync("/api/v1/auth/register", registerRequest);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var secondResponse = await client.PostAsJsonAsync("/api/v1/auth/register", registerRequest);
        secondResponse.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsToken()
    {
        var client = _factory.CreateClient();
        var email = $"login-{Guid.NewGuid()}@example.com";
        var password = "TestPassword1A";

        await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            Email = email,
            Password = password,
            ConfirmPassword = password,
            DisplayName = "Login User"
        });

        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            Email = email,
            Password = password
        });

        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var authData = await loginResponse.Content.ReadFromJsonAsync<AuthData>();
        authData!.Token.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();
        var email = $"wrongpw-{Guid.NewGuid()}@example.com";

        await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            Email = email,
            Password = "TestPassword1A",
            ConfirmPassword = "TestPassword1A",
            DisplayName = "Test"
        });

        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            Email = email,
            Password = "WrongPassword1A"
        });

        loginResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private record UserInfoData(Guid Id, string Email, string? DisplayName, DateTime CreatedAt);
    private record AuthData(string Token, string Email, string? DisplayName, DateTime ExpiresAt);
}
