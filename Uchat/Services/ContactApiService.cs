using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Uchat.Shared.DTOs;

namespace Uchat.Services
{
    public class ContactApiService
    {
        private readonly HttpClient _httpClient;
        private string? _authToken;

        public ContactApiService()
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(ServerConfig.ServerUrl)
            };
        }

        public void SetAuthToken(string token)
        {
            _authToken = token;
            Logger.Log($"ContactApiService: Setting auth token (length: {token?.Length ?? 0})");
            
            // Сначала очищаем чтобы не было дублей
            _httpClient.DefaultRequestHeaders.Authorization = null;
            
            if (!string.IsNullOrEmpty(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization = 
                    new AuthenticationHeaderValue("Bearer", token);
                Logger.Log("ContactApiService: Authorization header set");
            }
            else
            {
                Logger.Log("ContactApiService: Warning - empty token provided");
            }
        }

        public async Task<(bool Success, string? ErrorMessage)> SendFriendRequestAsync(string targetUsername)
        {
            try
            {
                Logger.Log($"Sending friend request to: {targetUsername}");
                
                var request = new HttpRequestMessage(HttpMethod.Post, "/api/contacts/send-request")
                {
                    Content = JsonContent.Create(new { Username = targetUsername })
                };
                
                if (!string.IsNullOrEmpty(_authToken))
                {
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _authToken);
                }
                
                var response = await _httpClient.SendAsync(request);
                Logger.Log($"Response status: {response.StatusCode}");
                
                if (response.IsSuccessStatusCode)
                {
                    return (true, null);
                }
                
                // Читаем тело ошибки
                var errorBody = await response.Content.ReadAsStringAsync();
                Logger.Log($"Error body: {errorBody}");
                
                try
                {
                    var errorJson = JsonSerializer.Deserialize<Dictionary<string, string>>(errorBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (errorJson != null && errorJson.TryGetValue("message", out var message))
                    {
                        return (false, message);
                    }
                }
                catch { }
                
                return (false, $"Error: {response.StatusCode}");
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to send friend request", ex);
                return (false, ex.Message);
            }
        }

        /// <summary>
        /// Принять запрос в друзья
        /// </summary>
        public async Task<bool> AcceptFriendRequestAsync(int contactId)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, $"/api/contacts/{contactId}/accept");
                
                if (!string.IsNullOrEmpty(_authToken))
                {
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _authToken);
                }
                
                var response = await _httpClient.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to accept friend request {contactId}", ex);
                return false;
            }
        }

        public async Task<bool> RejectFriendRequestAsync(int contactId)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, $"/api/contacts/{contactId}/reject");
                
                if (!string.IsNullOrEmpty(_authToken))
                {
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _authToken);
                }
                
                var response = await _httpClient.SendAsync(request);
        
                if (!response.IsSuccessStatusCode)
                {
                    // ЧИТАЕМ ОШИБКУ СЕРВЕРА
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"[API ERROR] Reject failed: {response.StatusCode} - {errorContent}");
                    return false;
                }
                
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[API EXCEPTION] {ex.Message}");
                return false;
            }
        }

        public async Task<List<ContactDto>> GetPendingRequestsAsync()
        {
            try
            {
                Logger.Log("API: GET /api/contacts/pending");
                Logger.Log($"API: Token length: {_authToken?.Length ?? 0}");

                // Создаем запрос вручную чтобы контролировать заголовки
                var request = new HttpRequestMessage(HttpMethod.Get, "/api/contacts/pending");

                // Явно прибиваем токен к запросу
                if (!string.IsNullOrEmpty(_authToken))
                {
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _authToken);
                    Logger.Log("API: Authorization header explicitly set on request");
                }
                else
                {
                    Logger.Log("ALARM: Token is empty in GetPendingRequestsAsync!");
                }

                // Отправляем
                var response = await _httpClient.SendAsync(request);
                
                Logger.Log($"API Response: {response.StatusCode}");

                // Читаем ответ
                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    Logger.Log($"API Error: {response.StatusCode} - {response.ReasonPhrase}");
                    Logger.Log($"API Error Body: {errorBody}");
                    return new List<ContactDto>();
                }

                // ВАЖНО: PropertyNameCaseInsensitive для десериализации camelCase с сервера
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var data = await response.Content.ReadFromJsonAsync<List<ContactDto>>(options);
                Logger.Log($"API Response: {data?.Count ?? 0} pending requests");
                
                if (data != null && data.Count > 0)
                {
                    foreach (var req in data)
                    {
                        Logger.Log($"  - Request: ID={req.Id}, Username={req.ContactUsername}, Status={req.Status}");
                    }
                }
                
                return data ?? new List<ContactDto>();
            }
            catch (Exception ex)
            {
                Logger.Error("API: Failed to get pending requests", ex);
                return new List<ContactDto>();
            }
        }

        /// <summary>
        /// Получить список всех контактов (друзей)
        /// </summary>
        public async Task<List<ContactDto>> GetContactsAsync()
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, "/api/contacts");
                
                if (!string.IsNullOrEmpty(_authToken))
                {
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _authToken);
                }
                
                var response = await _httpClient.SendAsync(request);
                
                if (!response.IsSuccessStatusCode)
                {
                    return new List<ContactDto>();
                }
                
                var data = await response.Content.ReadFromJsonAsync<List<ContactDto>>();
                return data ?? new List<ContactDto>();
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to get contacts", ex);
                return new List<ContactDto>();
            }
        }
        
        /// <summary>
        /// Удалить контакт (друга) по ChatRoomId
        /// </summary>
        public async Task<bool> DeleteContactByChatRoomAsync(int chatRoomId)
        {
            try
            {
                Logger.Log($"Deleting contact by chatRoomId: {chatRoomId}");
                
                var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/contacts/by-chat/{chatRoomId}");
                
                if (!string.IsNullOrEmpty(_authToken))
                {
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _authToken);
                }
                
                var response = await _httpClient.SendAsync(request);
                Logger.Log($"Delete contact response: {response.StatusCode}");
                
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to delete contact by chatRoom {chatRoomId}", ex);
                return false;
            }
        }
        
        /// <summary>
        /// Удалить контакт (друга)
        /// </summary>
        public async Task<bool> DeleteContactAsync(int contactId)
        {
            try
            {
                Logger.Log($"Deleting contact ID: {contactId}");
                
                var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/contacts/{contactId}");
                
                if (!string.IsNullOrEmpty(_authToken))
                {
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _authToken);
                }
                
                var response = await _httpClient.SendAsync(request);
                Logger.Log($"Delete contact response: {response.StatusCode}");
                
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to delete contact {contactId}", ex);
                return false;
            }
        }
    }
}
