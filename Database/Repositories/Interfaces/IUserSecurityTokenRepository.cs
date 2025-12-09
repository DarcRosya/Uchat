using Uchat.Database.Entities;

namespace Uchat.Database.Repositories.Interfaces;

public interface IUserSecurityTokenRepository
{
    Task<UserSecurityToken> CreateAsync(UserSecurityToken token);
    Task<UserSecurityToken?> GetByTokenAsync(string token, TokenType type);
    Task<bool> MarkAsUsedAsync(int id);
    Task<int> DeleteExpiredAsync(DateTime now);
}
