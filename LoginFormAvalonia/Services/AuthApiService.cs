using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace LoginFormAvalonia.Services;

public class AuthApiService
{
    private readonly HttpClient _httpClient;

    public AuthApiService()
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(ServerConfig.ServerUrl)
        };
    }

    public async Task<AuthResponse?> RegisterAsync(string username, string email, string password)
    {
        try
        {
            var request = new
            {
                username,
                email,
                password
            };

            var response = await _httpClient.PostAsJsonAsync("/api/auth/register", request);
            
            if (!response.IsSuccessStatusCode)
            {
                try
                {
                    var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponse>();
                    throw new Exception(errorResponse?.Error ?? "Registration failed");
                }
                catch (Exception)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Registration failed: {error}");
                }
            }

            return await response.Content.ReadFromJsonAsync<AuthResponse>();
        }
        catch (HttpRequestException ex)
        {
            throw new Exception($"Network error: {ex.Message}");
        }
    }

    public async Task<AuthResponse?> LoginAsync(string identifier, string password)
    {
        try
        {
            var request = new
            {
                identifier,
                password
            };

            var response = await _httpClient.PostAsJsonAsync("/api/auth/login", request);
            
            if (!response.IsSuccessStatusCode)
            {
                try
                {
                    var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponse>();
                    throw new Exception(errorResponse?.Error ?? "Invalid credentials");
                }
                catch (Exception)
                {
                    throw new Exception("Invalid credentials");
                }
            }

            return await response.Content.ReadFromJsonAsync<AuthResponse>();
        }
        catch (HttpRequestException ex)
        {
            throw new Exception($"Network error: {ex.Message}");
        }
    }

    public async Task<AuthResponse?> RefreshTokenAsync(string refreshToken)
    {
        try
        {
            var request = new { refreshToken };
            var response = await _httpClient.PostAsJsonAsync("/api/auth/refresh", request);
            
            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadFromJsonAsync<AuthResponse>();
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
}

public class AuthResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}

public class ErrorResponse
{
    public string Error { get; set; } = string.Empty;
}
