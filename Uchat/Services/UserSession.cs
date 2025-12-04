using System;
using Uchat.Shared.DTOs;

namespace Uchat.Services;

/// <summary>
/// Singleton для хранения данных текущего пользователя и JWT токенов
/// </summary>
public class UserSession
{
    private static UserSession? _instance;
    private static readonly object _lock = new object();

    public static UserSession Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = new UserSession();
                    }
                }
            }
            return _instance;
        }
    }

    private UserSession() { }

    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime TokenExpiresAt { get; set; }

    public bool IsAuthenticated => !string.IsNullOrEmpty(AccessToken) && DateTime.UtcNow < TokenExpiresAt;

    public void SetSession(AuthResponseDto response)
    {
        AccessToken = response.AccessToken;
        RefreshToken = response.RefreshToken;
        UserId = response.UserId;
        Username = response.Username;
        Email = response.Email;
        TokenExpiresAt = response.ExpiresAt;
    }

    public void Clear()
    {
        AccessToken = string.Empty;
        RefreshToken = string.Empty;
        UserId = 0;
        Username = string.Empty;
        Email = string.Empty;
        TokenExpiresAt = DateTime.MinValue;
    }
}
