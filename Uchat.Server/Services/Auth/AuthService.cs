using System.Security.Cryptography;
using Uchat.Database.Entities;
using Uchat.Database.Repositories.Interfaces;
using Uchat.Server.DTOs;

namespace Uchat.Server.Services.Auth;

public class AuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly JwtService _jwtService;

    public AuthService(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        JwtService jwtService)
    {
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
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
}
