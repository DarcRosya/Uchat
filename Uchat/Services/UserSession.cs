using System;
using System.Threading;
using System.Threading.Tasks;
using Uchat.Shared.DTOs;
using Uchat.Shared;

namespace Uchat.Services;

public class UserSession
{
    private static UserSession? _instance;
    private static readonly object _lock = new object();
    private Timer? _refreshTimer;
    private AuthApiService? _authService;
    private string[]? _systemArgs;

    public static UserSession Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new UserSession();
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
    
    public bool NeedsRefresh => DateTime.UtcNow >= TokenExpiresAt.AddMinutes(-5);

    public void Initialize(string[] systemArgs)
    {
        _systemArgs = systemArgs;
        Logger.Log("UserSession initialized with system arguments");
    }

    public void SetSession(string accessToken, string refreshToken, int userId, string username, string email = "")
    {
        AccessToken = accessToken;
        RefreshToken = refreshToken;
        UserId = userId;
        Username = username;
        Email = email;
        TokenExpiresAt = DateTime.UtcNow.AddMinutes(30);
        
        Logger.Log($"Session set for user {username} (expires: {TokenExpiresAt:HH:mm:ss})");
        StartAutoRefresh();
    }

    public void SetSession(AuthResponseDto response)
    {
        SetSession(response.AccessToken, response.RefreshToken, response.UserId, response.Username, response.Email);
        TokenExpiresAt = response.ExpiresAt;
    }

    public void StartAutoRefresh()
    {
        StopAutoRefresh();
        
        if (_systemArgs == null)
        {
            Logger.Error("Cannot start auto-refresh: system arguments not initialized", new Exception("Call UserSession.Instance.Initialize(args) at application startup"));
            return;
        }
        
        _authService = new AuthApiService(_systemArgs);
        
        // Check every 60 seconds
        _refreshTimer = new Timer(async _ => await TryAutoRefresh(), null, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(60));
        
        Logger.Log("Auto-refresh timer started");
    }

    private async Task TryAutoRefresh()
    {
        try
        {
            if (string.IsNullOrEmpty(RefreshToken))
            {
                Logger.Log("Auto-refresh skipped: no refresh token");
                return;
            }

            if (!NeedsRefresh)
            {
                return;
            }

            Logger.Log("Token expiring soon, attempting auto-refresh...");
            
            if (_authService == null)
            {
                if (_systemArgs == null)
                {
                    Logger.Error("Auto-refresh failed: system arguments not available", new Exception("UserSession not properly initialized"));
                    return;
                }
                _authService = new AuthApiService(_systemArgs);
            }

            var result = await _authService.RefreshTokenAsync(RefreshToken);
            
            if (result != null)
            {
                Logger.Log("Auto-refresh successful!");
                // Tokens are already stored in AuthApiService.RefreshTokenAsync
            }
            else
            {
                Logger.Error("Auto-refresh failed - refresh token invalid or expired", new Exception("RefreshTokenAsync returned null"));
                Clear();
                // TODO: Show UI notification to user: "Session expired, please login again"
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Auto-refresh error", ex);
        }
    }

    public void StopAutoRefresh()
    {
        if (_refreshTimer != null)
        {
            _refreshTimer.Dispose();
            _refreshTimer = null;
            Logger.Log("Auto-refresh timer stopped");
        }
    }

    public void Clear()
    {
        StopAutoRefresh();
        TokenStorage.ClearTokens();
        
        AccessToken = string.Empty;
        RefreshToken = string.Empty;
        UserId = 0;
        Username = string.Empty;
        Email = string.Empty;
        TokenExpiresAt = DateTime.MinValue;
        
        Logger.Log("Session cleared");
    }

    public async Task<bool> TryRestoreSessionAsync()
    {
        var savedTokens = TokenStorage.LoadTokens();
        
        if (savedTokens == null)
        {
            Logger.Log("No saved tokens to restore");
            return false;
        }

        if (_systemArgs == null)
        {
            Logger.Error("Cannot restore session: system arguments not initialized", new Exception("Call UserSession.Instance.Initialize(args) at application startup"));
            return false;
        }

        // Check if the access token has expired
        if (savedTokens.ExpiresAt > DateTime.UtcNow.AddMinutes(5))
        {
            // Access token is still valid
            Logger.Log("Restoring session from saved tokens");
            SetSession(savedTokens.AccessToken, savedTokens.RefreshToken, savedTokens.UserId, savedTokens.Username);
            TokenExpiresAt = savedTokens.ExpiresAt;
            return true;
        }

        // Access token has expired, trying to refresh
        Logger.Log("Saved access token expired, attempting refresh");
        
        _authService = new AuthApiService(_systemArgs);
        var result = await _authService.RefreshTokenAsync(savedTokens.RefreshToken);
        
        if (result != null)
        {
            Logger.Log("Session restored via token refresh");
            return true;
        }

        Logger.Error("Failed to restore session - refresh token invalid", new Exception("RefreshTokenAsync returned null"));
        TokenStorage.ClearTokens();
        return false;
    }
}

