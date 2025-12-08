using Uchat.Database.Entities;

namespace Uchat.Database.Repositories.Interfaces;

public interface IRefreshTokenRepository
{
    /// Create a new refresh token in the database.
    /// IMPORTANT: TokenHash must already be calculated in the service!
    Task<RefreshToken> CreateAsync(RefreshToken token);

    /// Find active refresh token by hash
    /// Returns NULL if token is not found, revoked, or expired
    /// Loads associated User via Include
    Task<RefreshToken?> GetByTokenHashAsync(string tokenHash);
    
    /// Get all active user tokens
    /// Used to verify plain text tokens via BCrypt.Verify in the service
    Task<List<RefreshToken>> GetUserTokensAsync(int userId);

    /// Revoke refresh token (set IsRevoked = true)
    /// Used during logout
    Task<bool> RevokeTokenAsync(string tokenHash);

    /// Revoke ALL active refresh tokens for the user
    /// Used for “Sign out of all devices”
    Task<int> RevokeAllUserTokensAsync(int userId);

    /// [Background Job] Delete expired and revoked tokens
    /// Returns the number of deleted records
    Task<int> CleanupExpiredTokensAsync();
}