using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Uchat.Database.Entities;
using Uchat.Database.Repositories.Interfaces;

namespace Uchat.Server.Services.Auth;

/// <summary>
/// Service responsible for creating and validating user security tokens
/// (email confirmation, password reset) and sending corresponding emails.
/// Uses existing UserSecurityToken entity that belongs to User.
/// </summary>
public class UserSecurityService
{
    private readonly IUserRepository _userRepository;
    private readonly IUserSecurityTokenRepository _tokenRepository;
    private readonly Uchat.Server.Services.Email.IEmailSender _emailSender;
    private readonly Uchat.Server.Services.Email.EmailSettings _emailSettings;

    public UserSecurityService(
        IUserRepository userRepository,
        IUserSecurityTokenRepository tokenRepository,
        Uchat.Server.Services.Email.IEmailSender emailSender,
        IOptions<Uchat.Server.Services.Email.EmailSettings> emailOptions)
    {
        _userRepository = userRepository;
        _tokenRepository = tokenRepository;
        _emailSender = emailSender;
        _emailSettings = emailOptions.Value;
    }

    // Generate a cryptographically secure token string (used for password reset links)
    private static string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    // Generate a cryptographically secure numeric code (6 digits)
    private static string GenerateNumericCode(int digits = 6)
    {
        var bytes = RandomNumberGenerator.GetBytes(4);
        var value = BitConverter.ToUInt32(bytes, 0) % (uint)Math.Pow(10, digits);
        return value.ToString(new string('0', digits));
    }

    /// <summary>
    /// Send an email confirmation code (numeric) that expires in 15 minutes.
    /// If a frontend URL is provided, include a link as well.
    /// </summary>
    public async Task SendEmailConfirmationAsync(int userId, string? baseFrontendUrl = null)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null) throw new InvalidOperationException("User not found");

        var code = GenerateNumericCode(6);

        var dbToken = new UserSecurityToken
        {
            UserId = user.Id,
            User = user,
            Type = TokenType.EmailConfirmation,
            Token = code,
            ExpiresAt = DateTime.UtcNow.AddMinutes(15),
            IsUsed = false
        };

        await _tokenRepository.CreateAsync(dbToken);

        string body;
        if (!string.IsNullOrWhiteSpace(baseFrontendUrl))
        {
            var url = baseFrontendUrl.TrimEnd('/') + $"/confirm-email?token={Uri.EscapeDataString(code)}";
            body = $"<p>Hello {user.DisplayName},</p>" +
                   $"<p>Your confirmation code is: <strong>{code}</strong> (expires in 15 minutes)</p>" +
                   $"<p>Or confirm by clicking: <a href=\"{url}\">Confirm Email</a></p>" +
                   $"<p>If you did not sign up, ignore this message.</p>";
        }
        else
        {
            body = $"<p>Hello {user.DisplayName},</p>" +
                   $"<p>Your confirmation code is: <strong>{code}</strong> (expires in 15 minutes)</p>" +
                   $"<p>If you did not sign up, ignore this message.</p>";
        }

        await _emailSender.SendEmailAsync(user.Email, "Confirm your Uchat account", body);
    }

    public async Task SendPasswordResetAsync(string email, string baseFrontendUrl)
    {
        var user = await _userRepository.GetByEmailAsync(email);
        if (user == null) return; // don't reveal existence

        var token = GenerateToken();

        var dbToken = new UserSecurityToken
        {
            UserId = user.Id,
            User = user,
            Type = TokenType.PasswordReset,
            Token = token,
            ExpiresAt = DateTime.UtcNow.AddHours(2),
            IsUsed = false
        };

        await _tokenRepository.CreateAsync(dbToken);

        var url = baseFrontendUrl?.TrimEnd('/') + $"/reset-password?token={Uri.EscapeDataString(token)}";

        var body = $"<p>Hello {user.DisplayName},</p>" +
                   $"<p>Click the link below to reset your password (expires in 2 hours):</p>" +
                   $"<p><a href=\"{url}\">Reset Password</a></p>" +
                   $"<p>If you didn't request a reset, ignore this email.</p>";

        await _emailSender.SendEmailAsync(user.Email, "Uchat Password Reset", body);
    }

    public async Task<User?> ValidateTokenAsync(string token, TokenType type)
    {
        var dbToken = await _tokenRepository.GetByTokenAsync(token, type);
        if (dbToken == null) return null;
        if (dbToken.IsUsed) return null;
        if (dbToken.ExpiresAt < DateTime.UtcNow) return null;

        // Mark as used
        await _tokenRepository.MarkAsUsedAsync(dbToken.Id);
        return dbToken.User;
    }
}
