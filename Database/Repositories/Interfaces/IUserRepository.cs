using Uchat.Database.Entities;

namespace Uchat.Database.Repositories.Interfaces;

/// <summary>
/// Interface for working with users (User)
/// </summary>
public interface IUserRepository
{
    /// NOTE: Checks the uniqueness of the username
    Task<User> CreateUserAsync(string username, string passwordHash, string? email = null);
    
    /// IMPORTANT: Returns NULL if the user is not found.
    Task<User?> GetByIdAsync(int userId);
    
    /// PURPOSE: Authorization, nickname search
    /// IMPORTANT: Case-insensitive search
    Task<User?> GetByUsernameAsync(string username);
    
    /// WHY: Password recovery, verification during registration
    Task<User?> GetByEmailAsync(string email);

    Task<User?> GetByUsernameOrEmailAsync(string identifier);
    
    /// Global user search
    /// SEARCH BY: Username, DisplayName, Bio
    Task<IEnumerable<User>> SearchUsersAsync(string query, int limit = 50);
    
    /// WHY: Mass data upload (chat participants)
    Task<IEnumerable<User>> GetUsersByIdsAsync(IEnumerable<int> userIds);

    /// IMPORTANT: Accepts ALREADY hashed new password
    Task<bool> ChangePasswordAsync(int userId, string newPasswordHash);
    
    /// NOTE: Checks the uniqueness of the email address.
    Task<bool> UpdateEmailAsync(int userId, string email);

    Task UpdateAsync(User user);
    
    /// WHY: Validation during registration
    Task<bool> UsernameExistsAsync(string username);
    
    Task<bool> EmailExistsAsync(string email);
    
    /// Statistics
    Task<long> GetTotalUsersCountAsync();
    
    /// Set or unset the EmailConfirmed flag for a user
    Task<bool> SetEmailConfirmedAsync(int userId, bool confirmed);
}
