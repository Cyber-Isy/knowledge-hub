using System.Security.Claims;
using FluentAssertions;
using KnowledgeHub.API.Configuration;
using KnowledgeHub.API.Controllers;
using KnowledgeHub.API.DTOs;
using KnowledgeHub.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace KnowledgeHub.API.Tests.Controllers;

public class AuthControllerTests
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IOptions<JwtSettings> _jwtSettings;
    private readonly AuthController _controller;

    public AuthControllerTests()
    {
        var userStore = Substitute.For<IUserStore<ApplicationUser>>();
        _userManager = Substitute.For<UserManager<ApplicationUser>>(
            userStore, null!, null!, null!, null!, null!, null!, null!, null!);

        var contextAccessor = Substitute.For<IHttpContextAccessor>();
        var claimsFactory = Substitute.For<IUserClaimsPrincipalFactory<ApplicationUser>>();
        _signInManager = Substitute.For<SignInManager<ApplicationUser>>(
            _userManager, contextAccessor, claimsFactory, null!, null!, null!, null!);

        _jwtSettings = Options.Create(new JwtSettings
        {
            Secret = "test-secret-key-that-is-at-least-32-characters-long",
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            ExpirationInMinutes = 60
        });

        _controller = new AuthController(_userManager, _signInManager, _jwtSettings);
    }

    [Fact]
    public async Task Register_WithValidRequest_ReturnsOkWithToken()
    {
        var request = new RegisterRequest
        {
            Email = "test@example.com",
            Password = "Password123",
            ConfirmPassword = "Password123",
            DisplayName = "Test User"
        };

        _userManager.CreateAsync(Arg.Any<ApplicationUser>(), request.Password)
            .Returns(IdentityResult.Success);
        _userManager.GetRolesAsync(Arg.Any<ApplicationUser>())
            .Returns(new List<string>());

        var result = await _controller.Register(request);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var authResponse = okResult.Value.Should().BeOfType<AuthResponse>().Subject;
        authResponse.Email.Should().Be(request.Email);
        authResponse.DisplayName.Should().Be(request.DisplayName);
        authResponse.Token.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ReturnsBadRequest()
    {
        var request = new RegisterRequest
        {
            Email = "duplicate@example.com",
            Password = "Password123",
            ConfirmPassword = "Password123"
        };

        var identityErrors = new[]
        {
            new IdentityError { Code = "DuplicateEmail", Description = "Email is already taken." }
        };
        _userManager.CreateAsync(Arg.Any<ApplicationUser>(), request.Password)
            .Returns(IdentityResult.Failed(identityErrors));

        var result = await _controller.Register(request);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsOkWithToken()
    {
        var request = new LoginRequest
        {
            Email = "test@example.com",
            Password = "Password123"
        };

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = request.Email,
            UserName = request.Email,
            DisplayName = "Test User"
        };

        _userManager.FindByEmailAsync(request.Email).Returns(user);
        _signInManager.CheckPasswordSignInAsync(user, request.Password, false)
            .Returns(Microsoft.AspNetCore.Identity.SignInResult.Success);
        _userManager.GetRolesAsync(user).Returns(new List<string>());

        var result = await _controller.Login(request);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var authResponse = okResult.Value.Should().BeOfType<AuthResponse>().Subject;
        authResponse.Email.Should().Be(request.Email);
        authResponse.Token.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        var request = new LoginRequest
        {
            Email = "test@example.com",
            Password = "WrongPassword"
        };

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = request.Email,
            UserName = request.Email
        };

        _userManager.FindByEmailAsync(request.Email).Returns(user);
        _signInManager.CheckPasswordSignInAsync(user, request.Password, false)
            .Returns(Microsoft.AspNetCore.Identity.SignInResult.Failed);

        var result = await _controller.Login(request);

        result.Result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task Login_WithNonexistentEmail_ReturnsUnauthorized()
    {
        var request = new LoginRequest
        {
            Email = "nonexistent@example.com",
            Password = "Password123"
        };

        _userManager.FindByEmailAsync(request.Email).Returns((ApplicationUser?)null);

        var result = await _controller.Login(request);

        result.Result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task GetCurrentUser_WhenAuthenticated_ReturnsUserInfo()
    {
        var userId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = userId,
            Email = "test@example.com",
            UserName = "test@example.com",
            DisplayName = "Test User",
            CreatedAt = DateTime.UtcNow
        };

        _userManager.FindByIdAsync(userId.ToString()).Returns(user);
        _userManager.GetRolesAsync(user).Returns(new List<string> { "User" });

        SetUserContext(userId);

        var result = await _controller.GetCurrentUser();

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var userInfo = okResult.Value.Should().BeOfType<UserInfoResponse>().Subject;
        userInfo.Id.Should().Be(userId);
        userInfo.Email.Should().Be("test@example.com");
        userInfo.DisplayName.Should().Be("Test User");
        userInfo.Roles.Should().Contain("User");
    }

    [Fact]
    public async Task GetCurrentUser_WithNoUserId_ReturnsUnauthorized()
    {
        var claims = new ClaimsPrincipal(new ClaimsIdentity());
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claims }
        };

        var result = await _controller.GetCurrentUser();

        result.Result.Should().BeOfType<UnauthorizedResult>();
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
