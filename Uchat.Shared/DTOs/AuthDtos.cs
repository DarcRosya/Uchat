using System;
using System.ComponentModel.DataAnnotations;

namespace Uchat.Shared.DTOs;

// ========================================================================
// REQUEST DTOs
// ========================================================================

public class RegisterDto
{
    [Required]
    [MinLength(3)]
    [MaxLength(50)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MinLength(6)]
    public string Password { get; set; } = string.Empty;
}

public class LoginDto
{
    [Required]
    public string Identifier { get; set; } = string.Empty; // Username или Email

    [Required]
    public string Password { get; set; } = string.Empty;
}

public class RefreshTokenDto
{
    [Required]
    public string RefreshToken { get; set; } = string.Empty;
}

// ========================================================================
// RESPONSE DTOs
// ========================================================================

public class AuthResponseDto
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? Email { get; set; }
    public DateTime ExpiresAt { get; set; }
}

public class ErrorResponse
{
    public string Error { get; set; } = string.Empty;
}
