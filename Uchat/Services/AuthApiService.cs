using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Uchat.Shared.DTOs;

namespace Uchat.Services;

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

    public async Task<AuthResponseDto?> RegisterAsync(string username, string email, string password)
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

            return await response.Content.ReadFromJsonAsync<AuthResponseDto>();
        }
        catch (HttpRequestException ex)
        {
            throw new Exception($"Network error: {ex.Message}");
        }
    }

    public async Task<AuthResponseDto?> LoginAsync(string identifier, string password)
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

            return await response.Content.ReadFromJsonAsync<AuthResponseDto>();
        }
        catch (HttpRequestException ex)
        {
            throw new Exception($"Network error: {ex.Message}");
        }
    }

    public async Task<AuthResponseDto?> RefreshTokenAsync(string refreshToken)
    {
        try
        {
            var request = new { refreshToken };
            var response = await _httpClient.PostAsJsonAsync("/api/auth/refresh", request);
            
            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadFromJsonAsync<AuthResponseDto>();
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
