using Database.Entities;

namespace Database.Repositories.Interfaces;

public interface IRefreshTokenRepository
{
    /// Создать новый refresh token в БД
    /// ВАЖНО: TokenHash должен быть уже вычислен в TokenService!
    Task<RefreshToken> CreateAsync(RefreshToken token);

    /// Найти активный refresh token по хешу
    /// Возвращает NULL если токен не найден, отозван или истёк
    /// Загружает связанного User через Include
    Task<RefreshToken?> GetByTokenHashAsync(string tokenHash);

    /// Отозвать refresh token (установить IsRevoked = true)
    /// Используется при logout
    Task<bool> RevokeTokenAsync(string tokenHash);

    /// Отозвать ВСЕ активные refresh tokens пользователя
    /// Используется при "Выйти со всех устройств"
    Task<int> RevokeAllUserTokensAsync(int userId);

    /// [Background Job] Удалить истекшие и отозванные токены
    /// Возвращает количество удалённых записей
    Task<int> CleanupExpiredTokensAsync();
}