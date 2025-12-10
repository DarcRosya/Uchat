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

    public ChatApiService(string[] args)
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(ConnectionConfig.GetServerUrl(args))
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

    public async Task<ChatRoomDetailDto?> GetChatDetailsAsync(int chatId)
    {
        try
        {
            var result = await _httpClient.GetFromJsonAsync<ChatRoomDetailDto>($"/api/chats/{chatId}");
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting chat details: {ex.Message}");
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

    public async Task<bool> AcceptGroupInviteAsync(int chatId)
    {
        try
        {
            var response = await _httpClient.PostAsync($"/api/chats/{chatId}/accept", null);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Failed to accept invite: {error}");
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error accepting invite: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> RejectGroupInviteAsync(int chatId)
    {
        try
        {
            var response = await _httpClient.PostAsync($"/api/chats/{chatId}/reject", null);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Failed to reject invite: {error}");
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error rejecting invite: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> PinChatAsync(int chatId, bool isPinned)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"api/chats/{chatId}/pin", new { IsPinned = isPinned });
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error pinning chat: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> AddMemberAsync(int chatId, string username)
    {
        try
        {
            // Отправляем объект с полем Username, как ждет обновленный DTO на сервере
            var request = new { Username = username };
            
            var response = await _httpClient.PostAsJsonAsync($"/api/chats/{chatId}/members", request);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Failed to add member: {error}");
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Network error: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> LeaveChatAsync(int chatId)
    {
        try
        {
            // Вызываем эндпоинт выхода
            var response = await _httpClient.PostAsync($"/api/chats/{chatId}/leave", null);
            
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error leaving chat: {ex.Message}");
            return false;
        }
    }

    public async Task<List<ChatRoomDto>> GetPendingInvitesAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/chats/invites/pending");
            
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<List<ChatRoomDto>>();
                return result ?? new List<ChatRoomDto>();
            }
            else 
            {
                Console.WriteLine($"[API Error] Group Invites: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Exception] GetPendingInvitesAsync: {ex.Message}");
        }

        return new List<ChatRoomDto>();
    }

    public async Task<ChatRoomDto?> JoinPublicGroupByNameAsync(string groupName)
    {
        try
        {
            // Send a POST request. Encode the group name so that special characters do not break the URL.
            var encodedName = Uri.EscapeDataString(groupName);
            var response = await _httpClient.PostAsync($"/api/chats/join/{encodedName}", null);

            if (!response.IsSuccessStatusCode)
            {
                return null; 
            }

            return await response.Content.ReadFromJsonAsync<ChatRoomDto>();
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> UpdateChatAsync(int chatId, object updateDto)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"/api/chats/{chatId}", updateDto);
            
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating chat: {ex.Message}");
            return false;
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
