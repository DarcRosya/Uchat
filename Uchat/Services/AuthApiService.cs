using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Uchat.Shared.DTOs;
using Uchat.Shared;

namespace Uchat.Services;

public class AuthApiService
{
    private readonly HttpClient _httpClient;
    private static readonly JsonSerializerOptions _jsonOptions = new() 
    { 
        PropertyNameCaseInsensitive = true 
    };

    public AuthApiService(string[] args)
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(ConnectionConfig.GetServerUrl(args))
        };
    }

    public async Task<RegisterResultDto?> RegisterAsync(string username, string email, string password)
    {
        try
        {
            Logger.Log($"Attempting registration for: {username}");
            
            var request = new
            {
                username,
                email,
                password
            };

            var response = await _httpClient.PostAsJsonAsync("/api/auth/register", request);

            if (!response.IsSuccessStatusCode)
            {
                Logger.Log($"Registration failed: {response.StatusCode}");
                try
                {
                    var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponse>(_jsonOptions);
                    throw new Exception(errorResponse?.Error ?? "Registration failed");
                }
                catch (Exception)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Registration failed: {error}");
                }
            }

            try
            {
                var result = await response.Content.ReadFromJsonAsync<RegisterResultDto>(_jsonOptions);
                return result;
            }
            catch 
            {
                return new RegisterResultDto { RequiresConfirmation = false };
            }
        }
        catch (HttpRequestException ex)
        {
            Logger.Error("Network error during registration", ex);
            throw new Exception($"Network error: {ex.Message}");
        }
    }

    public async Task<AuthResponseDto?> ConfirmEmailAsync(string email, string code)
    {
        try
        {
            var request = new 
            { 
                email = email, 
                code = code 
            };
            
            var response = await _httpClient.PostAsJsonAsync("/api/auth/confirm-email", request);

            if (!response.IsSuccessStatusCode)
            {
                try
                {
                    var err = await response.Content.ReadFromJsonAsync<ErrorResponse>(_jsonOptions);
                    throw new Exception(err?.Error ?? "Confirmation failed");
                }
                catch
                {
                    var text = await response.Content.ReadAsStringAsync();
                    throw new Exception(text);
                }
            }

            var result = await response.Content.ReadFromJsonAsync<AuthResponseDto>(_jsonOptions);
            if (result != null)
            {
                TokenStorage.SaveTokens(result.AccessToken, result.RefreshToken, result.ExpiresAt, result.UserId, result.Username);
                UserSession.Instance.SetSession(result.AccessToken, result.RefreshToken, result.UserId, result.Username);
            }

            return result;
        }
        catch (HttpRequestException ex)
        {
            Logger.Error("Network error during confirmation", ex);
            throw new Exception($"Network error: {ex.Message}");
        }
    }

    public async Task<AuthResponseDto?> LoginAsync(string identifier, string password)
    {
        try
        {
            Logger.Log($"Attempting login for: {identifier}");
            
            var request = new
            {
                identifier,
                password
            };

            var response = await _httpClient.PostAsJsonAsync("/api/auth/login", request);
            
            if (!response.IsSuccessStatusCode)
            {
                Logger.Log($"Login failed: {response.StatusCode}");
                try
                {
                    var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponse>(_jsonOptions);
                    throw new Exception(errorResponse?.Error ?? "Invalid credentials");
                }
                catch (Exception)
                {
                    throw new Exception("Invalid credentials");
                }
            }

            var result = await response.Content.ReadFromJsonAsync<AuthResponseDto>(_jsonOptions);
            
            if (result != null)
            {
                Logger.Log($"Login successful for {result.Username}");
                TokenStorage.SaveTokens(result.AccessToken, result.RefreshToken, result.ExpiresAt, result.UserId, result.Username);
                UserSession.Instance.SetSession(result.AccessToken, result.RefreshToken, result.UserId, result.Username);
            }
            
            return result;
        }
        catch (HttpRequestException ex)
        {
            Logger.Error("Network error during login", ex);
            throw new Exception($"Network error: {ex.Message}");
        }
    }

    public async Task<AuthResponseDto?> RefreshTokenAsync(string refreshToken)
    {
        try
        {
            Logger.Log("Attempting to refresh access token");
            
            var request = new { refreshToken };
            var response = await _httpClient.PostAsJsonAsync("/api/auth/refresh", request);
            
            if (!response.IsSuccessStatusCode)
            {
                Logger.Log($"Token refresh failed: {response.StatusCode}");
                return null;
            }

            var result = await response.Content.ReadFromJsonAsync<AuthResponseDto>(_jsonOptions);
            
            if (result != null)
            {
                Logger.Log("Token refresh successful");
                TokenStorage.SaveTokens(result.AccessToken, result.RefreshToken, result.ExpiresAt, result.UserId, result.Username);
                UserSession.Instance.SetSession(result.AccessToken, result.RefreshToken, result.UserId, result.Username);
            }
            
            return result;
        }
        catch
        {
            return null;
        }
    }

    public async Task LogoutAsync(string accessToken, string refreshToken)
    {
        try
        {
            _httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            
            var request = new { refreshToken };
            await _httpClient.PostAsJsonAsync("/api/auth/logout", request);
        }
        catch
        {
            // Ignore logout errors
        }
    }

    public async Task ForgotPasswordAsync(string email)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/auth/forgot-password", new { email });
        // Тут можно не проверять success, так как сервер всегда отвечает OK в целях безопасности
    }

    public async Task VerifyResetCodeAsync(string email, string code)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/auth/verify-reset-code", new { email, code });
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadFromJsonAsync<ErrorResponse>(_jsonOptions);
            throw new Exception(err?.Error ?? "Invalid code");
        }
    }

    public async Task ResetPasswordAsync(string email, string code, string newPassword)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/auth/reset-password", new { email, code, newPassword });
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadFromJsonAsync<ErrorResponse>(_jsonOptions);
            throw new Exception(err?.Error ?? "Failed to reset password");
        }
    }
}
