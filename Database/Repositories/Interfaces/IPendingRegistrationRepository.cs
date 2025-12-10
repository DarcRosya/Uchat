using Uchat.Database.Entities;

namespace Uchat.Database.Repositories.Interfaces;

public interface IPendingRegistrationRepository
{
    Task<PendingRegistration> CreateOrUpdateAsync(PendingRegistration pending);
    Task<PendingRegistration?> GetByEmailAsync(string email);
    Task<bool> UsernameExistsAsync(string username);
    Task<bool> EmailExistsAsync(string email); 
    Task<int> DeleteExpiredAsync(); 
    Task<bool> DeleteAsync(string email); 
}