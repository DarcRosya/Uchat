using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Uchat.Shared.DTOs;

namespace Uchat.Services;

public class ChatApiService
{
    private readonly HttpClient _httpClient;

    public ChatApiService()
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(ServerConfig.ServerUrl)
        };
    }

    public void SetAuthToken(string token)
    {
        _httpClient.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    public async Task<List<ChatRoomDto>> GetUserChatsAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/chats");
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Failed to get chats: {error}");
            }

            return await response.Content.ReadFromJsonAsync<List<ChatRoomDto>>() ?? new List<ChatRoomDto>();
        }
        catch (HttpRequestException ex)
        {
            throw new Exception($"Network error: {ex.Message}");
        }
    }

    public async Task<ChatRoomDetailDto?> GetChatByIdAsync(int chatId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/chats/{chatId}");
            
            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadFromJsonAsync<ChatRoomDetailDto>();
        }
        catch
        {
            return null;
        }
    }

    public async Task<ChatRoomDto?> CreateChatAsync(CreateChatRequestDto request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/chats", request);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Failed to create chat: {error}");
            }

            return await response.Content.ReadFromJsonAsync<ChatRoomDto>();
        }
        catch (HttpRequestException ex)
        {
            throw new Exception($"Network error: {ex.Message}");
        }
    }

    public async Task AddMemberAsync(int chatId, int userId, string? role = null)
    {
        try
        {
            var request = new { userId, role };
            var response = await _httpClient.PostAsJsonAsync($"/api/chats/{chatId}/members", request);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Failed to add member: {error}");
            }
        }
        catch (HttpRequestException ex)
        {
            throw new Exception($"Network error: {ex.Message}");
        }
    }

    public async Task RemoveMemberAsync(int chatId, int memberId)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"/api/chats/{chatId}/members/{memberId}");
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Failed to remove member: {error}");
            }
        }
        catch (HttpRequestException ex)
        {
            throw new Exception($"Network error: {ex.Message}");
        }
    }
}
