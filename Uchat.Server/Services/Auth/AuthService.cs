using System;
using System.IO;
using System.Security.Cryptography;
using Uchat.Database.Entities;
using Uchat.Database.Repositories.Interfaces;
using Uchat.Server.Services.Chat;
using Uchat.Shared.DTOs;


namespace Uchat.Server.Services.Auth;

public class AuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IChatRoomRepository _chatRoomRepository;
    private readonly IChatRoomService _chatRoomService;
    private readonly JwtService _jwtService;
    private readonly UserSecurityService _userSecurityService;
    private readonly Uchat.Server.Services.Email.IEmailSender _emailSender;
    private readonly IPendingRegistrationRepository _pendingRepo;

    public AuthService(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IChatRoomRepository chatRoomRepository,
        IChatRoomService chatRoomService,
        JwtService jwtService,
        UserSecurityService userSecurityService,
        Uchat.Server.Services.Email.IEmailSender emailSender,
        IPendingRegistrationRepository pendingRepo)
    {
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _chatRoomRepository = chatRoomRepository;
        _chatRoomService = chatRoomService;
        _jwtService = jwtService;
        _userSecurityService = userSecurityService;
        _emailSender = emailSender;
        _pendingRepo = pendingRepo;
    }
    public async Task<RegisterResultDto> RegisterAsync(RegisterDto dto)
    {
        var existingUser = await _userRepository.GetByUsernameAsync(dto.Username);
        if (existingUser != null)
        return new RegisterResultDto 
        {
            RequiresConfirmation = false,
            Message = "Username already taken" 
        };

        var existingEmail = await _userRepository.GetByEmailAsync(dto.Email);
        if (existingEmail != null)
        return new RegisterResultDto
        {
            RequiresConfirmation = false, 
            Message = "Email already registered" 
        };

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);
        var code = GenerateNumericCode(6);

        var pending = new PendingRegistration
        {
            Username = dto.Username,
            Email = dto.Email,
            PasswordHash = passwordHash,
            Code = code,
            CodeExpiresAt = DateTime.UtcNow.AddMinutes(15),
            CreatedAt = DateTime.UtcNow
        };

        await _pendingRepo.CreateOrUpdateAsync(pending);

        string htmlBody = await LoadEmailTemplateAsync("ConfirmEmail.html", dto.Username, code);
        await _emailSender.SendEmailAsync(dto.Email, "Confirm your Uchat account", htmlBody);

        return new RegisterResultDto
        {
            RequiresConfirmation = true,
            Message = "Confirmation code sent to email",
            PendingId = null 
        };
    }

    public async Task<AuthResponseDto> ConfirmEmailAsync(string email, string code)
    {
        var pending = await _pendingRepo.GetByEmailAsync(email);
        
        if (pending == null) 
            throw new InvalidOperationException("Registration request not found or expired.");

        if (pending.Code != code) 
            throw new InvalidOperationException("Invalid confirmation code.");

        if (!pending.CodeExpiresAt.HasValue || pending.CodeExpiresAt.Value < DateTime.UtcNow)
            throw new InvalidOperationException("Confirmation code expired.");

        if (await _userRepository.UsernameExistsAsync(pending.Username))
            throw new InvalidOperationException("Username already taken.");

        var createdUser = await _userRepository.CreateUserAsync(pending.Username, pending.PasswordHash, pending.Email);
        
        await _userRepository.SetEmailConfirmedAsync(createdUser.Id, true);

        await _pendingRepo.DeleteAsync(pending.Email);

        // INITIALIZING CHATS
        await CreatePersonalNotesChat(createdUser.Id, createdUser.Username);
        await AddUserToGlobalPublicChat(createdUser.Id, createdUser.Username);

        var accessToken = _jwtService.GenerateAccessToken(createdUser.Id, createdUser.Username, createdUser.Email);
        var (plainTextToken, tokenHash) = _jwtService.GenerateRefreshTokens();

        var refreshToken = new RefreshToken
        {
            UserId = createdUser.Id,
            TokenHash = tokenHash,
            ExpiresAt = _jwtService.GetRefreshTokenExpiry(),
            CreatedAt = DateTime.UtcNow
        };

        await _refreshTokenRepository.CreateAsync(refreshToken);

        return new AuthResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = plainTextToken,
            UserId = createdUser.Id,
            Username = createdUser.Username,
            Email = createdUser.Email,
            ExpiresAt = DateTime.UtcNow.AddMinutes(30)
        };
    }

    private static string GenerateNumericCode(int digits = 6)
    {
        var bytes = RandomNumberGenerator.GetBytes(4);
        var value = BitConverter.ToUInt32(bytes, 0) % (uint)Math.Pow(10, digits);
        return value.ToString(new string('0', digits));
    }

    public async Task<AuthResponseDto> LoginAsync(LoginDto dto)
    {
        var existingUser = await _userRepository.GetByUsernameOrEmailAsync(dto.Identifier);
        
        if (existingUser == null)
        {
            throw new InvalidOperationException("Invalid credentials");
        }

        if (!BCrypt.Net.BCrypt.Verify(dto.Password, existingUser.PasswordHash))
        {
            throw new InvalidOperationException("Invalid credentials");
        }

        var accessToken = _jwtService.GenerateAccessToken(
            existingUser.Id,
            existingUser.Username,
            existingUser.Email
        );
        var (plainTextToken, tokenHash) = _jwtService.GenerateRefreshTokens();

        var refreshToken = new RefreshToken
        {
            UserId = existingUser.Id,
            TokenHash = tokenHash,
            ExpiresAt = _jwtService.GetRefreshTokenExpiry(),
            CreatedAt = DateTime.UtcNow
        };

        await _refreshTokenRepository.CreateAsync(refreshToken);

        return new AuthResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = plainTextToken,
            UserId = existingUser.Id,
            Username = existingUser.Username,
            ExpiresAt = DateTime.UtcNow.AddMinutes(30)
        };
    }

    public async Task<AuthResponseDto?> RefreshTokenAsync(string refreshToken)
    {
        var tokenHash = _jwtService.HashRefreshToken(refreshToken);
        var storedToken = await _refreshTokenRepository.GetByTokenHashAsync(tokenHash);
        
        if (storedToken == null || storedToken.ExpiresAt < DateTime.UtcNow)
        {
            return null;
        }
        
        var user = await _userRepository.GetByIdAsync(storedToken.UserId);
        if (user == null) return null;
        
        await _refreshTokenRepository.RevokeTokenAsync(storedToken.TokenHash);
        
        var accessToken = _jwtService.GenerateAccessToken(user.Id, user.Username, user.Email);
        var (newPlainToken, newTokenHash) = _jwtService.GenerateRefreshTokens();
        
        var newRefreshToken = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = newTokenHash,
            ExpiresAt = _jwtService.GetRefreshTokenExpiry(),
            CreatedAt = DateTime.UtcNow
        };
        
        await _refreshTokenRepository.CreateAsync(newRefreshToken);
        
        return new AuthResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = newPlainToken,
            UserId = user.Id,
            Username = user.Username,
            Email = user.Email,
            ExpiresAt = DateTime.UtcNow.AddMinutes(30)
        };
    }

    public async Task LogoutAsync(string refreshToken)
    {
        var tokenHash = _jwtService.HashRefreshToken(refreshToken);
        await _refreshTokenRepository.RevokeTokenAsync(tokenHash);
    }

    public async Task ForgotPasswordAsync(string email)
    {
        var user = await _userRepository.GetByEmailAsync(email);
        
        // If the user does not exist, we pretend that everything is OK (security best practice)
        // so that hackers cannot search through the email database ( we are cool! )
        if (user == null) return; 

        var code = GenerateNumericCode(6); 
        
        user.PasswordResetCode = code;
        user.PasswordResetCodeExpiresAt = DateTime.UtcNow.AddMinutes(15);
        
        await _userRepository.UpdateAsync(user);

        string htmlBody = await LoadEmailTemplateAsync("ResetPassword.html", user.Username, code);
        await _emailSender.SendEmailAsync(email, "Reset Password Request", htmlBody);
    }

    public async Task<bool> VerifyResetCodeAsync(string email, string code)
    {
        var user = await _userRepository.GetByEmailAsync(email);
        
        if (user == null || 
            user.PasswordResetCode != code || 
            !user.PasswordResetCodeExpiresAt.HasValue ||
            user.PasswordResetCodeExpiresAt < DateTime.UtcNow)
        {
            return false;
        }
        
        return true;
    }

    public async Task ResetPasswordAsync(string email, string code, string newPassword)
    {
        var user = await _userRepository.GetByEmailAsync(email);

        if (user == null) throw new InvalidOperationException("User not found");
        
        if (user.PasswordResetCode != code || 
            user.PasswordResetCodeExpiresAt < DateTime.UtcNow)
        {
             throw new InvalidOperationException("Invalid or expired code");
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        
        user.PasswordResetCode = null;
        user.PasswordResetCodeExpiresAt = null;

        await _userRepository.UpdateAsync(user);
        
        await _refreshTokenRepository.RevokeAllUserTokensAsync(user.Id);
    }

    private async Task CreatePersonalNotesChat(int userId, string username)
    {
        try
        {
            var result = await _chatRoomService.CreateChatAsync(
                creatorId: userId,
                name: "Notes",
                type: ChatRoomType.DirectMessage,
                initialMemberIds: null // Creator is added automatically
            );

            if (!result.IsSuccess)
            {
                Console.WriteLine($"Failed to create notes chat for user {username}: {result.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            // Логируем ошибку, но не прерываем регистрацию
            Console.WriteLine($"Failed to create notes chat for user {username}: {ex.Message}");
        }
    }
    
    private async Task AddUserToGlobalPublicChat(int userId, string username)
    {
        try
        {
            var result = await _chatRoomService.JoinPublicChatByNameAsync("Global Chat", userId);

            if (!result.IsSuccess)
            {
                Console.WriteLine($"[AuthService] Warning: Could not add user {username} to Global Chat. Reason: {result.ErrorMessage}");
            }
            else
            {
                Console.WriteLine($"[AuthService] User {username} successfully added to Global Chat (DB + Redis updated).");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AuthService] Error joining Global Chat: {ex.Message}");
        }
    }

    private async Task<string> LoadEmailTemplateAsync(string templateName, string username, string code)
    {
        // Ищем в папке EmailTemplates
        var templatePath = Path.Combine(AppContext.BaseDirectory, "EmailTemplates", templateName);
        
        if (File.Exists(templatePath))
        {
            try
            {
                var html = await File.ReadAllTextAsync(templatePath);
                return html.Replace("{Code}", code)
                        .Replace("{Username}", username);
                        // .Replace("{Greetings}", ...) 
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AuthService] Error reading template {templateName}: {ex.Message}");
            }
        }
        else
        {
            Console.WriteLine($"[AuthService] Template not found at: {templatePath}");
        }

        return $"<h3>Hello {username}</h3><p>Your code: <strong>{code}</strong></p>";
    }
}
