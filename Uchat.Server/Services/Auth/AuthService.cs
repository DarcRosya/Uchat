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
            throw new InvalidOperationException("Username already taken");

        var existingEmail = await _userRepository.GetByEmailAsync(dto.Email);
        if (existingEmail != null)
            throw new InvalidOperationException("Email already registered");

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

        string htmlBody = GenerateEmailBody(dto.Username, code);

        await _emailSender.SendEmailAsync(dto.Email, "Confirm your Uchat account", htmlBody);

        return new RegisterResultDto
        {
            RequiresConfirmation = true,
            Message = "Confirmation code sent to email",
            PendingId = null // ID больше не нужен клиенту, мы работаем по Email
        };
    }

    // Вынес логику HTML в отдельный метод для чистоты
    private string GenerateEmailBody(string username, string code)
    {
        var templatePath = Path.Combine(AppContext.BaseDirectory, "Email.html");
        
        if (File.Exists(templatePath))
        {
            try
            {
                var html = File.ReadAllText(templatePath);
                return html.Replace("{Code}", code) // Используй фигурные скобки в HTML для надежности: {Code}
                           .Replace("{Username}", username);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AuthService] Failed to read email template: {ex.Message}");
            }
        }

        // Fallback, если файла нет
        return $"<h3>Welcome, {username}!</h3><p>Your confirmation code is: <strong>{code}</strong></p>";
    }

    public async Task<AuthResponseDto> ConfirmEmailAsync(string email, string code)
    {
        // 1. Ищем заявку по Email (безопаснее, чем искать просто по коду)
        var pending = await _pendingRepo.GetByEmailAsync(email);
        
        if (pending == null) 
            throw new InvalidOperationException("Registration request not found or expired.");

        // 2. Проверяем код
        if (pending.Code != code) 
            throw new InvalidOperationException("Invalid confirmation code.");

        // 3. Проверяем срок действия
        if (!pending.CodeExpiresAt.HasValue || pending.CodeExpiresAt.Value < DateTime.UtcNow)
            throw new InvalidOperationException("Confirmation code expired.");

        // 4. Финальная проверка перед созданием (Race condition check)
        if (await _userRepository.UsernameExistsAsync(pending.Username))
            throw new InvalidOperationException("Username already taken.");

        // 5. Создаем реального пользователя
        var createdUser = await _userRepository.CreateUserAsync(pending.Username, pending.PasswordHash, pending.Email);
        
        // Ставим EmailConfirmed = true
        await _userRepository.SetEmailConfirmedAsync(createdUser.Id, true);

        // 6. УДАЛЯЕМ заявку из Pending (вместо MarkAsUsed)
        await _pendingRepo.DeleteAsync(pending.Email);

        // 7. Инициализация чатов
        await CreatePersonalNotesChat(createdUser.Id, createdUser.Username);
        await AddUserToGlobalPublicChat(createdUser.Id, createdUser.Username);

        // 8. Выдаем токены
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
        
        // Отзываем старый токен
        await _refreshTokenRepository.RevokeTokenAsync(storedToken.TokenHash);
        
        // Генерируем новые
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

    public async Task<int> LogoutAllAsync(int userId)
    {
        return await _refreshTokenRepository.RevokeAllUserTokensAsync(userId);
    }

    private async Task CreatePersonalNotesChat(int userId, string username)
    {
        try
        {
            var result = await _chatRoomService.CreateChatAsync(
                creatorId: userId,
                name: "Notes",
                type: ChatRoomType.DirectMessage,
                description: "Personal notes and reminders",
                initialMemberIds: null // Создатель добавляется автоматически
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
            // Ищем глобальный чат по имени (создан в DbInitializer при старте сервера)
            var globalChat = await _chatRoomRepository.GetByNameAsync("Global Chat");
            
            if (globalChat == null)
            {
                // Критическая ошибка - глобальный чат должен существовать всегда
                Console.WriteLine($"[CRITICAL] Global Chat not found during registration of user {username}!");
                return;
            }
            
            // Добавляем нового пользователя в глобальный чат
            await _chatRoomRepository.AddMemberAsync(new ChatRoomMember
            {
                ChatRoomId = globalChat.Id,
                UserId = userId,
                JoinedAt = DateTime.UtcNow
            });
            
            Console.WriteLine($"[AuthService] User {username} added to Global Chat (ID: {globalChat.Id})");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to add user {username} to global chat: {ex.Message}");
        }
    }
}
