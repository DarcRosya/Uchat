using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Uchat.Server.Services;

public class JwtService
{
    private readonly IConfiguration _configuration;

    public JwtService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GenerateAccessToken(int userId, string username, string email)
    {
        var jwtSettings = _configuration.GetSection("Jwt");

        var secretKey = Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]!);
        
        var expiryMinutes = int.Parse(jwtSettings["ExpiryMinutes"]!);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, username),
            new Claim(ClaimTypes.Email, email),
            
            // JwtRegisteredClaimNames.Jti  unique token ID (for revocation)
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        // Create a signature key
        // SymmetricSecurityKey - a single key for encryption AND decryption
        var key = new SymmetricSecurityKey(secretKey);
        
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],                      // Who issued the token (your server)
            audience: jwtSettings["Audience"],                  // Who the token is for (your client)
            claims: claims,                                     // Data inside the token
            expires: DateTime.UtcNow.AddMinutes(expiryMinutes), // When it expires
            signingCredentials: credentials                     // Signature
        );

        // Превратить токен в строку
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public (string PlainToken, string Hash) GenerateRefreshTokens()
    {
        var randomBytes = new byte[64];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(randomBytes);
        }
        var plainToken = Convert.ToBase64String(randomBytes);
        var hash = HashRefreshToken(plainToken);
        
        return (plainToken, hash);
    }

    public string HashRefreshToken(string refreshToken)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(refreshToken);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }

    public DateTime GetRefreshTokenExpiry()
    {
        var jwtSettings = _configuration.GetSection("Jwt");
        var expiryDays = int.Parse(jwtSettings["RefreshTokenExpiryDays"]!);
        return DateTime.UtcNow.AddDays(expiryDays);
    }
}