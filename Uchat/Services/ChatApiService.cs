using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace Uchat.Services;

public class ChatApiService
{
    private readonly HttpClient _httpClient;

    public ChatApiService()
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(ServerConfig.ApiBaseUrl)
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

    public async Task<ChatRoomDto?> CreateChatAsync(CreateChatRequest request)
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

public class ChatRoomDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? IconUrl { get; set; }
    public string Type { get; set; } = string.Empty;
    public int CreatorId { get; set; }
    public DateTime CreatedAt { get; set; }
    public int MemberCount { get; set; }
}

public class ChatRoomDetailDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? IconUrl { get; set; }
    public string Type { get; set; } = string.Empty;
    public int CreatorId { get; set; }
    public DateTime CreatedAt { get; set; }
    public int? ParentChatRoomId { get; set; }
    public int? MaxMembers { get; set; }
    public bool DefaultCanSendMessages { get; set; }
    public bool DefaultCanInviteMembers { get; set; }
    public int? SlowModeSeconds { get; set; }
    public List<ChatMemberDto> Members { get; set; } = new();
}

public class ChatMemberDto
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public DateTime JoinedAt { get; set; }
}

public class CreateChatRequest
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "Private";
    public string? Description { get; set; }
    public string? IconUrl { get; set; }
    public List<int>? InitialMemberIds { get; set; }
    public int? ParentChatRoomId { get; set; }
    public int? MaxMembers { get; set; }
}
