using System;
using System.ComponentModel.DataAnnotations;

namespace Uchat.Shared.DTOs;

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
    public string Identifier { get; set; } = string.Empty; // Username 

    [Required]
    public string Password { get; set; } = string.Empty;
}

public class RefreshTokenDto
{
    [Required]
    public string RefreshToken { get; set; } = string.Empty;
}

public class ConfirmEmailDto
{
    public required string Email { get; set; }
    public required string Code { get; set; }
}

public class AuthResponseDto
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? Email { get; set; }
    public DateTime ExpiresAt { get; set; }
}

public class RegisterResultDto
{
    public bool RequiresConfirmation { get; set; }
    public string Message { get; set; } = string.Empty;
    public int? PendingId { get; set; }
}

public class ErrorResponse
{
    public string? Error { get; set; }
    public int StatusCode { get; set; }
}


public class ForgotPasswordDto
{
    public string Email { get; set; } = string.Empty;
}

public class VerifyResetCodeDto
{
    public string Email { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}

public class ResetPasswordDto
{
    public string Email { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}