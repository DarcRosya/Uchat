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

    public AuthService(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IChatRoomRepository chatRoomRepository,
        IChatRoomService chatRoomService,
        JwtService jwtService)
    {
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _chatRoomRepository = chatRoomRepository;
        _chatRoomService = chatRoomService;
        _jwtService = jwtService;
    }

    public async Task<AuthResponseDto> RegisterAsync(RegisterDto dto)
    {
        var existingUser = await _userRepository.GetByUsernameAsync(dto.Username);
        if (existingUser != null)
            throw new InvalidOperationException("Username already taken");

        var existingEmail = await _userRepository.GetByEmailAsync(dto.Email);
        if (existingEmail != null)
            throw new InvalidOperationException("Email already registered");

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);

        var createdUser = await _userRepository.CreateUserAsync(dto.Username, passwordHash, dto.Email);

        // Создаем личный чат "Заметки" для нового пользователя
        await CreatePersonalNotesChat(createdUser.Id, createdUser.Username);
        
        // Добавляем пользователя в глобальный публичный чат
        await AddUserToGlobalPublicChat(createdUser.Id, createdUser.Username);

        var accessToken = _jwtService.GenerateAccessToken(
            createdUser.Id,
            createdUser.Username,
            createdUser.Email
        );

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
            ExpiresAt = DateTime.UtcNow.AddMinutes(30)
        };
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
