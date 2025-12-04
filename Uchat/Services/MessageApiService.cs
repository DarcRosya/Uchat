using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Uchat.Shared.DTOs;

namespace Uchat.Services;

/// <summary>
/// HTTP клиент для работы с API сообщений
/// </summary>
public class MessageApiService
{
    private readonly HttpClient _httpClient;

    public MessageApiService()
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

    /// <summary>
    /// Получить сообщения чата с пагинацией
    /// </summary>
    /// <param name="chatId">ID чата</param>
    /// <param name="limit">Количество сообщений (по умолчанию 50)</param>
    /// <param name="before">Cursor для загрузки старых сообщений</param>
    public async Task<PaginatedMessagesDto?> GetMessagesAsync(int chatId, int limit = 50, DateTime? before = null)
    {
        try
        {
            var url = $"/api/chats/{chatId}/messages?limit={limit}";
            if (before.HasValue)
            {
                url += $"&before={before.Value:O}"; // ISO 8601 format
            }

            var response = await _httpClient.GetAsync(url);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Failed to get messages: {error}");
            }

            return await response.Content.ReadFromJsonAsync<PaginatedMessagesDto>();
        }
        catch (HttpRequestException ex)
        {
            throw new Exception($"Network error: {ex.Message}");
        }
    }

    /// <summary>
    /// Получить конкретное сообщение по ID
    /// </summary>
    public async Task<MessageDto?> GetMessageByIdAsync(int chatId, string messageId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/chats/{chatId}/messages/{messageId}");
            
            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadFromJsonAsync<MessageDto>();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Отправить новое сообщение
    /// </summary>
    public async Task<MessageDto?> SendMessageAsync(int chatId, MessageCreateDto message)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"/api/chats/{chatId}/messages", message);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Failed to send message: {error}");
            }

            return await response.Content.ReadFromJsonAsync<MessageDto>();
        }
        catch (HttpRequestException ex)
        {
            throw new Exception($"Network error: {ex.Message}");
        }
    }

    /// <summary>
    /// Редактировать сообщение
    /// </summary>
    public async Task<bool> EditMessageAsync(int chatId, string messageId, string newContent)
    {
        try
        {
            var dto = new EditMessageDto { Content = newContent };
            var response = await _httpClient.PatchAsJsonAsync($"/api/chats/{chatId}/messages/{messageId}", dto);
            
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Удалить сообщение (физически из БД)
    /// </summary>
    public async Task<bool> DeleteMessageAsync(int chatId, string messageId)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"/api/chats/{chatId}/messages/{messageId}");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Отметить сообщения как прочитанные до определённого времени
    /// </summary>
    public async Task<bool> MarkAsReadUntilAsync(int chatId, DateTime untilTimestamp)
    {
        try
        {
            var response = await _httpClient.PostAsync(
                $"/api/chats/{chatId}/messages/mark-read?until={untilTimestamp:O}", 
                null
            );
            
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
