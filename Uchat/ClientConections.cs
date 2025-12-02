using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Uchat.Services;

namespace Uchat
{
    public class ChatMessage
    {
        public string Id { get; set; } = string.Empty;
        public int ChatId { get; set; }
        public MessageSender Sender { get; set; } = new();
        public string Content { get; set; } = string.Empty;
        public string Type { get; set; } = "text";
        public DateTime SentAt { get; set; }
        public DateTime? EditedAt { get; set; }
        public bool IsDeleted { get; set; }
    }

    public class MessageSender
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
    }

    public partial class MainWindow : Window
    {
        private HubConnection _connection = null!;
        private ChatApiService _chatApiService = null!;
        
        private string? currentChatId = null; // Текущий активный чат
        private string name = "Unknown"; // Инициализируется в конструкторе после парсинга аргументов
        
        private TextBlock _connectionStatusIndicator = null!;     
        
        private void InitializeChatComponents()
        {
            // Инициализируем имя пользователя из сессии
            name = UserSession.Instance.Username ?? "Unknown";
            
            // Находим контрол ПОСЛЕ InitializeComponent
            _connectionStatusIndicator = this.FindControl<TextBlock>("ConnectionStatusText") ?? new TextBlock();
            
            // Инициализируем API сервис
            _chatApiService = new ChatApiService();
            _chatApiService.SetAuthToken(UserSession.Instance.AccessToken ?? string.Empty);
            
            ConnectToServer();
        }

        private async void ConnectToServer()
        {
            // Проверяем наличие токена
            if (string.IsNullOrEmpty(UserSession.Instance.AccessToken))
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (_connectionStatusIndicator != null)
                    {
                        _connectionStatusIndicator.Text = "● No token - Login first!";
                        _connectionStatusIndicator.Foreground = Brushes.Red;
                    }
                });
                return;
            }
            
            // Server connection с JWT токеном из UserSession
            // ВАЖНО: Токен передаётся через query string, т.к. SignalR не поддерживает custom headers
            _connection = new HubConnectionBuilder()
            .WithUrl($"{ServerConfig.ServerUrl}/chatHub?access_token={UserSession.Instance.AccessToken}", options =>
            {
                // Токен уже в URL, но оставляем для совместимости
                options.AccessTokenProvider = () => Task.FromResult<string?>(UserSession.Instance.AccessToken);
            })
            .WithAutomaticReconnect()
            .Build();

            _connection.On<string, string, string>("ReceiveMessage", (chatId, user, message) =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (chatId != currentChatId || string.IsNullOrEmpty(message))
                        return;

                    replyTheMessageBox.IsVisible = false;
                    
                    // Используем унифицированный метод отображения
                    DisplayMessage(chatId, user, message);
                    
                    chatTextBox.Text = string.Empty;
                    ChatScrollViewer.ScrollToEnd();
                });
            });

            try
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (_connectionStatusIndicator != null)
                    {
                        _connectionStatusIndicator.Text = "● Connecting...";
                        _connectionStatusIndicator.Foreground = Brushes.Orange;
                    }
                });
                
                await _connection.StartAsync();
                
                Dispatcher.UIThread.Post(() =>
                {
                    if (_connectionStatusIndicator != null)
                    {
                        _connectionStatusIndicator.Text = "● Connected";
                        _connectionStatusIndicator.Foreground = Brushes.Green;
                    }
                });
                
                // Загружаем список чатов пользователя
                await LoadUserChats();
            }
            catch (Exception ex)
            {
                var statusMsg = "Connection failed";
                
                if (ex.InnerException != null && ex.InnerException.Message.Contains("401"))
                    statusMsg = "Unauthorized";
                else if (ex.InnerException != null && ex.InnerException.Message.Contains("404"))
                    statusMsg = "Server not found";
                else if (ex.Message.Contains("Connection refused") || ex.Message.Contains("No connection"))
                    statusMsg = "Server offline";
                
                Dispatcher.UIThread.Post(() =>
                {
                    if (_connectionStatusIndicator != null)
                    {
                        _connectionStatusIndicator.Text = $"● {statusMsg}";
                        _connectionStatusIndicator.Foreground = Brushes.Red;
                    }
                });
            }
        }

        private async Task LoadUserChats()
        {
            try
            {
                var chats = await _chatApiService.GetUserChatsAsync();
                
                Dispatcher.UIThread.Post(() =>
                {
                    System.Diagnostics.Debug.WriteLine($"Loaded {chats.Count} chats for user {name}");
                    
                    // Отображаем информацию о чатах
                    foreach (var chat in chats)
                    {
                        System.Diagnostics.Debug.WriteLine($"Chat: {chat.Name} (ID: {chat.Id}, Type: {chat.Type}, Members: {chat.MemberCount})");
                    }
                    
                    // Подключаемся к первому чату (обычно это "Заметки")
                    if (chats.Count > 0)
                    {
                        var firstChat = chats[0];
                        currentChatId = firstChat.Id.ToString();
                        System.Diagnostics.Debug.WriteLine($"Joining chat: {firstChat.Name} (ID: {currentChatId})");
                        
                        // Присоединяемся к чату и загружаем историю
                        Task.Run(async () =>
                        {
                            await _connection.InvokeAsync("JoinGroup", currentChatId);
                            await _connection.InvokeAsync("NewUserNotification", currentChatId, name);
                            await LoadChatHistory(currentChatId);
                        });
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("No chats found for user");
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load chats: {ex.Message}");
            }
        }

        private async Task LoadChatHistory(string chatId)
        {
            try
            {
                var messages = await _connection.InvokeAsync<List<ChatMessage>>("GetChatHistory", chatId, 50);
                
                Dispatcher.UIThread.Post(() =>
                {
                    System.Diagnostics.Debug.WriteLine($"Loaded {messages.Count} messages for chat {chatId}");
                    
                    // Очищаем текущие сообщения
                    ChatMessagesPanel.Children.Clear();
                    
                    // Отображаем загруженные сообщения
                    foreach (var msg in messages)
                    {
                        DisplayMessage(chatId, msg.Sender.Username, msg.Content);
                    }
                    
                    ChatScrollViewer.ScrollToEnd();
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load chat history: {ex.Message}");
            }
        }

        private void DisplayMessage(string chatId, string user, string message)
        {
            if (currentChatId == null || chatId != currentChatId || string.IsNullOrEmpty(message))
                return;

            var timestamp = DateTime.Now.ToString("HH:mm");
            bool isGuest = (user != name);
            
            // Используем класс Chat.Message для единообразия
            var chatMessage = new MainWindow.Chat.Message(false, message, timestamp, isGuest);
            var bubble = chatMessage.Bubble;

            var grid = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(new GridLength(1, GridUnitType.Star))
                }
            };

            Grid.SetColumn(bubble, 0);
            grid.Children.Add(bubble);

            // Добавляем контекстное меню
            var messageContextMenu = new MainWindow.Chat.MessageContextMenu(this, chatMessage, grid);
            bubble.ContextMenu = messageContextMenu.Result();

            ChatMessagesPanel.Children.Add(grid);
        }

        public async Task SendMessageToServerAsync(string messageText)
        {
            if (string.IsNullOrWhiteSpace(messageText) || currentChatId == null)
                return;
                
            try
            {
                await _connection.InvokeAsync("SendMessage", currentChatId, name, messageText);
            }
            catch (Exception)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (_connectionStatusIndicator != null)
                    {
                        _connectionStatusIndicator.Text = "● Failed to send";
                        _connectionStatusIndicator.Foreground = Brushes.Orange;
                    }
                });
                throw;
            }
        }
    }
}
