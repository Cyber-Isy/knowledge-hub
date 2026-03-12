using System.ComponentModel.DataAnnotations;

namespace KnowledgeHub.API.DTOs;

public record RegisterRequest
{
    [Required, EmailAddress]
    public required string Email { get; init; }

    [Required, MinLength(8)]
    public required string Password { get; init; }

    [Required, Compare(nameof(Password))]
    public required string ConfirmPassword { get; init; }

    [MaxLength(100)]
    public string? DisplayName { get; init; }
}

public record LoginRequest
{
    [Required, EmailAddress]
    public required string Email { get; init; }

    [Required]
    public required string Password { get; init; }
}

public record AuthResponse
{
    public required string Token { get; init; }
    public required string Email { get; init; }
    public string? DisplayName { get; init; }
    public DateTime ExpiresAt { get; init; }
}

public record UserInfoResponse
{
    public Guid Id { get; init; }
    public required string Email { get; init; }
    public string? DisplayName { get; init; }
    public DateTime CreatedAt { get; init; }
}
