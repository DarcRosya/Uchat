/*
 * ============================================================================
 * ENTITY MODEL: REFRESH TOKEN (Токен обновления)
 * ============================================================================
 * 
 * ============================================================================
 * JWT FLOW С REFRESH TOKENS
 * ============================================================================
 * 
 * 1. Login:
 *    Client                Server                 Database
 *      |                     |                        |
 *      |--POST /auth/login-->|                        |
 *      |  {username, pwd}    |                        |
 *      |                     |---Query Users--------->|
 *      |                     |<---Return User---------|
 *      |                     |                        |
 *      |                     |--Generate JWT Token----|
 *      |                     |  (expires in 15 min)   |
 *      |                     |                        |
 *      |                     |--Generate Refresh----->|
 *      |                     |  Token (30 days)       |
 *      |                     |<---Save to DB----------|
 *      |                     |                        |
 *      |<--Return tokens-----|                        |
 *      |  {accessToken,      |                        |
 *      |   refreshToken}     |                        |
 * 
 * 2. Access protected resource:
 *      |--GET /api/profile-->|                        |
 *      |  Bearer: JWT        |                        |
 *      |                     |--Validate JWT----------|
 *      |                     |  (check signature,     |
 *      |                     |   expiration)          |
 *      |<--Return data-------|                        |
 * 
 * 3. JWT expired (after 15 min):
 *      |--GET /api/profile-->|                        |
 *      |  Bearer: JWT        |                        |
 *      |                     |--Validate JWT----------|
 *      |                     |  ❌ EXPIRED            |
 *      |<--401 Unauthorized--|                        |
 *      |                     |                        |
 *      |--POST /auth/refresh>|                        |
 *      |  {refreshToken}     |                        |
 *      |                     |---Find in DB---------->|
 *      |                     |<---Return Token--------|
 *      |                     |  ✅ Valid, not expired |
 *      |                     |                        |
 *      |                     |--Generate NEW JWT------|
 *      |                     |  (expires in 15 min)   |
 *      |                     |                        |
 *      |                     |--Update LastUsedAt---->|
 *      |                     |<---Saved---------------|
 *      |                     |                        |
 *      |<--Return tokens-----|                        |
 *      |  {accessToken,      |                        |
 *      |   refreshToken}     |                        |
 * 
 * 4. Logout:
 *      |--POST /auth/logout->|                        |
 *      |  {refreshToken}     |                        |
 *      |                     |---Delete from DB------>|
 *      |                     |<---Deleted-------------|
 *      |<--200 OK------------|                        |
 * 
 * ============================================================================
 */

namespace Uchat.Database.Entities;

/// <summary>
/// Refresh Token для JWT авторизации
/// 
/// Хранит долгоживущие токены (30 дней) для обновления JWT access tokens
/// </summary>
public class RefreshToken
{
    public int Id { get; set; }
    
    /// Хеш токена (SHA256)
    /// Генерация:
    ///   var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    ///   var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    public required string TokenHash { get; set; }
    public required int UserId { get; set; }
    public DateTime CreatedAt { get; set; }
    
    /// Когда токен истекает
    /// Обычно: CreatedAt + 30 дней
    public DateTime ExpiresAt { get; set; }
    
    /// Был ли токен отозван (вручную logout)
    /// true = токен больше нельзя использовать (даже если не истек)
    public bool IsRevoked { get; set; }
    public User User { get; set; } = null!;
}

/*
 * ============================================================================
 * ПРИМЕРЫ ИСПОЛЬЗОВАНИЯ
 * ============================================================================
 * 
 * 1. СОЗДАНИЕ ТОКЕНА ПРИ LOGIN:
 * 
 *    public async Task<(RefreshToken, string)> CreateRefreshTokenAsync(int userId)
 *    {
 *        // Генерируем случайный токен
 *        var tokenBytes = RandomNumberGenerator.GetBytes(64);
 *        var token = Convert.ToBase64String(tokenBytes);
 *        
 *        // Хешируем токен для хранения в БД
 *        var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
 *        
 *        var refreshToken = new RefreshToken
 *        {
 *            UserId = userId,
 *            TokenHash = tokenHash,
 *            CreatedAt = DateTime.UtcNow,
 *            ExpiresAt = DateTime.UtcNow.AddDays(30),
 *            IsRevoked = false
 *        };
 *        
 *        _context.RefreshTokens.Add(refreshToken);
 *        await _context.SaveChangesAsync();
 *        
 *        // Возвращаем и запись БД и сам токен (для отправки клиенту)
 *        return (refreshToken, token);
 *    }
 * 
 * 
 * 2. ВАЛИДАЦИЯ ТОКЕНА:
 * 
 *    public async Task<RefreshToken?> ValidateRefreshTokenAsync(string token)
 *    {
 *        // Хешируем полученный токен
 *        var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
 *        
 *        // Ищем по хешу в БД
 *        var refreshToken = await _context.RefreshTokens
 *            .Include(t => t.User)
 *            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash);
 *        
 *        // Проверки:
 *        if (refreshToken == null) return null;                       // Токен не найден
 *        if (refreshToken.IsRevoked) return null;                     // Токен отозван
 *        if (refreshToken.ExpiresAt < DateTime.UtcNow) return null;   // Токен истек
 *        if (refreshToken.User.IsBlocked) return null;                // Пользователь заблокирован
 *        
 *        return refreshToken;
 *    }
 * 
 * 
 * 3. LOGOUT (отозвать токен):
 * 
 *    public async Task<bool> RevokeTokenAsync(string token)
 *    {
 *        var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
 *        
 *        var refreshToken = await _context.RefreshTokens
 *            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash);
 *        
 *        if (refreshToken == null) return false;
 *        
 *        refreshToken.IsRevoked = true;
 *        await _context.SaveChangesAsync();
 *        
 *        return true;
 *    }
 * 
 * 
 * 4. "ВЫЙТИ СО ВСЕХ УСТРОЙСТВ":
 * 
 *    public async Task<int> RevokeAllUserTokensAsync(int userId)
 *    {
 *        var tokens = await _context.RefreshTokens
 *            .Where(t => t.UserId == userId && !t.IsRevoked)
 *            .ToListAsync();
 *        
 *        foreach (var token in tokens)
 *        {
 *            token.IsRevoked = true;
 *        }
 *        
 *        await _context.SaveChangesAsync();
 *        return tokens.Count;
 *    }
 * 
 * 
 * 5. ОЧИСТКА ИСТЕКШИХ ТОКЕНОВ (Background Job):
 * 
 *    public async Task<int> CleanupExpiredTokensAsync()
 *    {
 *        var expiredTokens = await _context.RefreshTokens
 *            .Where(t => t.ExpiresAt < DateTime.UtcNow || t.IsRevoked)
 *            .ToListAsync();
 *        
 *        _context.RefreshTokens.RemoveRange(expiredTokens);
 *        await _context.SaveChangesAsync();
 *        
 *        return expiredTokens.Count;
 *    }
 * 
 * ============================================================================
 */
