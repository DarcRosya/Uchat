using System;
using System.IO;
using System.Text.Json;
using Uchat.Shared;

namespace Uchat.Services
{
    /// <summary>
    /// Безопасное хранилище токенов на диске
    /// </summary>
    public static class TokenStorage
    {
        private static readonly string _tokensFilePath;
        private static readonly object _lock = new object();

        static TokenStorage()
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var appFolder = Path.Combine(appDataPath, "Uchat");
            
            if (!Directory.Exists(appFolder))
            {
                Directory.CreateDirectory(appFolder);
            }
            
            _tokensFilePath = Path.Combine(appFolder, "tokens.dat");
            Logger.Log($"TokenStorage initialized at: {_tokensFilePath}");
        }

        public static void SaveTokens(string accessToken, string refreshToken, DateTime expiresAt, int userId, string username)
        {
            lock (_lock)
            {
                try
                {
                    var data = new TokenData
                    {
                        AccessToken = accessToken,
                        RefreshToken = refreshToken,
                        ExpiresAt = expiresAt,
                        UserId = userId,
                        Username = username,
                        SavedAt = DateTime.UtcNow
                    };

                    var json = JsonSerializer.Serialize(data);
                    File.WriteAllText(_tokensFilePath, json);
                    
                    Logger.Log($"Tokens saved for user {username} (expires: {expiresAt:yyyy-MM-dd HH:mm:ss})");
                }
                catch (Exception ex)
                {
                    Logger.Error("Failed to save tokens", ex);
                }
            }
        }

        public static TokenData? LoadTokens()
        {
            lock (_lock)
            {
                try
                {
                    if (!File.Exists(_tokensFilePath))
                    {
                        Logger.Log("No saved tokens found");
                        return null;
                    }

                    var json = File.ReadAllText(_tokensFilePath);
                    var data = JsonSerializer.Deserialize<TokenData>(json);
                    
                    if (data == null)
                    {
                        Logger.Log("Failed to deserialize tokens");
                        return null;
                    }

                    Logger.Log($"Tokens loaded for user {data.Username} (expires: {data.ExpiresAt:yyyy-MM-dd HH:mm:ss})");
                    
                    // Проверяем не истек ли refresh token (обычно живет 30 дней)
                    if (data.SavedAt.AddDays(30) < DateTime.UtcNow)
                    {
                        Logger.Log("Refresh token expired (older than 30 days)");
                        ClearTokens();
                        return null;
                    }
                    
                    return data;
                }
                catch (Exception ex)
                {
                    Logger.Error("Failed to load tokens", ex);
                    return null;
                }
            }
        }

        public static void ClearTokens()
        {
            lock (_lock)
            {
                try
                {
                    if (File.Exists(_tokensFilePath))
                    {
                        File.Delete(_tokensFilePath);
                        Logger.Log("Tokens cleared");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("Failed to clear tokens", ex);
                }
            }
        }

        public static bool HasValidTokens()
        {
            var data = LoadTokens();
            return data != null;
        }
    }

    public class TokenData
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public DateTime SavedAt { get; set; }
    }
}
