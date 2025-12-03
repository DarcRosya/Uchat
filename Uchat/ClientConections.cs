using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Collections.Generic;
using System.Linq;
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
        public string? ReplyToMessageId { get; set; }
        public string? ReplyToContent { get; set; } // Текст сообщения, на которое ответили
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
        
        // Словарь для быстрого поиска UI сообщений по ID
        private Dictionary<string, MainWindow.Chat.Message> _messageCache = new();     
        
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
            .WithUrl($"{ServerConfig.ApiBaseUrl}/chatHub?access_token={UserSession.Instance.AccessToken}", options =>
            {
                // Токен уже в URL, но оставляем для совместимости
                options.AccessTokenProvider = () => Task.FromResult<string?>(UserSession.Instance.AccessToken);
            })
            .WithAutomaticReconnect()
            .Build();

            _connection.On<string, string, string, string?, string, string?>("ReceiveMessage", (chatId, user, message, replyContent, messageId, replyToMessageId) =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (chatId != currentChatId || string.IsNullOrEmpty(message))
                        return;

                    replyTheMessageBox.IsVisible = false;
                    
                    // Используем унифицированный метод отображения с replyContent, serverId и replyToMessageId
                    DisplayMessage(chatId, user, message, replyContent, messageId, null, replyToMessageId);
                    
                    chatTextBox.Text = string.Empty;
                    ChatScrollViewer.ScrollToEnd();
                });
            });
            
            // Обработчик редактирования сообщения
            _connection.On<string, string, DateTime>("MessageEdited", (messageId, newContent, editedAt) =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (_messageCache.TryGetValue(messageId, out var cachedMsg))
                    {
                        // Обновляем текст сообщения
                        cachedMsg.ContentTextBlock.Text = newContent;
                        
                        // Добавляем метку "edited"
                        var timeStackPanel = cachedMsg.Bubble.Child as StackPanel;
                        if (timeStackPanel != null)
                        {
                            var lastChild = timeStackPanel.Children[timeStackPanel.Children.Count - 1] as StackPanel;
                            if (lastChild != null)
                            {
                                // Проверяем, есть ли уже метка edited
                                bool hasEditedLabel = false;
                                foreach (var child in lastChild.Children)
                                {
                                    if (child is TextBlock tb && tb.Text == "edited")
                                    {
                                        hasEditedLabel = true;
                                        break;
                                    }
                                }
                                
                                if (!hasEditedLabel)
                                {
                                    var editedLabel = new TextBlock
                                    {
                                        Text = "edited",
                                        Foreground = Brush.Parse("#C1E1C1"),
                                        FontSize = 10,
                                        Padding = new Thickness(0, 0, 3, 0),
                                        Margin = new Thickness(0, 3, 0, 0),
                                        FontStyle = FontStyle.Italic,
                                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right
                                    };
                                    lastChild.Children.Add(editedLabel);
                                }
                            }
                        }
                    }
                });
            });
            
            // Обработчик удаления сообщения, на которое отвечали (устаревший, оставлен для совместимости)
            _connection.On<string>("ReplyTargetDeleted", (deletedMessageId) =>
            {
                Console.WriteLine($"[CLIENT] ReplyTargetDeleted received: {deletedMessageId}");
                System.Diagnostics.Debug.WriteLine($"[CLIENT] ReplyTargetDeleted received: {deletedMessageId}");
                Dispatcher.UIThread.Post(() =>
                {
                    int removedCount = 0;
                    Console.WriteLine($"[CLIENT] MessageCache has {_messageCache.Count} messages");
                    // Находим все сообщения, которые отвечали на удалённое
                    foreach (var kvp in _messageCache.ToList())
                    {
                        var msg = kvp.Value;
                        
                        Console.WriteLine($"[CLIENT] Checking message {kvp.Key}, ReplyToMessageId: {msg.ReplyToMessageId}");
                        
                        // Проверяем, отвечает ли это сообщение на удалённое
                        if (msg.ReplyToMessageId == deletedMessageId)
                        {
                            Console.WriteLine($"[CLIENT] Match found! Removing reply border from message {kvp.Key}");
                            
                            // Очищаем ReplyToMessageId в кеше
                            msg.ReplyToMessageId = null;
                            
                            // Получаем StackPanel сообщения
                            if (msg.Bubble.Child is StackPanel messageStackPanel)
                            {
                                // Ищем ReplyBorder внутри StackPanel
                                var replyBorder = messageStackPanel.Children.OfType<Border>()
                                    .FirstOrDefault(b => b.Name == "ReplyBorder");
                                
                                if (replyBorder != null)
                                {
                                    // Удаляем Border с ответом
                                    messageStackPanel.Children.Remove(replyBorder);
                                    removedCount++;
                                    Console.WriteLine($"[CLIENT] ReplyBorder removed successfully");
                                }
                                else
                                {
                                    Console.WriteLine($"[CLIENT] ReplyBorder not found in message");
                                }
                            }
                        }
                    }
                    Console.WriteLine($"[CLIENT] ReplyTargetDeleted processed: {removedCount} reply borders removed");
                });
            });

            // Объединённый обработчик удаления с очисткой ответов
            _connection.On<string, bool>("MessageDeletedWithReplies", (messageId, hasReplies) =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    // Если на это сообщение были ответы, сначала очищаем их
                    if (hasReplies)
                    {
                        foreach (var kvp in _messageCache.ToList())
                        {
                            var msg = kvp.Value;
                            
                            if (msg.ReplyToMessageId == messageId)
                            {
                                msg.ReplyToMessageId = null;
                                
                                if (msg.Bubble.Child is StackPanel messageStackPanel)
                                {
                                    var replyBorder = messageStackPanel.Children.OfType<Border>()
                                        .FirstOrDefault(b => b.Name == "ReplyBorder");
                                    
                                    if (replyBorder != null)
                                    {
                                        messageStackPanel.Children.Remove(replyBorder);
                                    }
                                }
                            }
                        }
                    }
                    
                    // Теперь удаляем само сообщение
                    if (_messageCache.TryGetValue(messageId, out var cachedMsg))
                    {
                        var bubble = cachedMsg.Bubble;
                        if (bubble.Parent is Grid messageGrid && messageGrid.Parent == ChatMessagesPanel)
                        {
                            ChatMessagesPanel.Children.Remove(messageGrid);
                        }
                        
                        _messageCache.Remove(messageId);
                        Console.WriteLine($"[CLIENT] Message {messageId} removed from cache and UI");
                    }
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
                    
                    // Очищаем текущие сообщения и кеш
                    ChatMessagesPanel.Children.Clear();
                    _messageCache.Clear();
                    
                    // Сортируем по времени отправки (старые сверху)
                    var sortedMessages = messages.OrderBy(m => m.SentAt).ToList();
                    
                    // Кешируем DTO по ID для Reply (временный кеш)
                    var dtoCacheForReply = new Dictionary<string, ChatMessage>();
                    foreach (var msg in sortedMessages)
                    {
                        dtoCacheForReply[msg.Id] = msg;
                    }
                    
                    // Отображаем загруженные сообщения с Reply
                    foreach (var msg in sortedMessages)
                    {
                        string? replyContent = null;
                        
                        // Если это ответ, находим исходное сообщение
                        if (!string.IsNullOrEmpty(msg.ReplyToMessageId) && dtoCacheForReply.TryGetValue(msg.ReplyToMessageId, out var originalMsg))
                        {
                            replyContent = originalMsg.Content;
                        }
                        else if (!string.IsNullOrEmpty(msg.ReplyToContent))
                        {
                            // Фолбек на сохранённый текст (для обратной совместимости)
                            replyContent = msg.ReplyToContent;
                        }
                        
                        DisplayMessage(chatId, msg.Sender.Username, msg.Content, replyContent, msg.Id, msg.EditedAt, msg.ReplyToMessageId);
                    }
                    
                    ChatScrollViewer.ScrollToEnd();
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load chat history: {ex.Message}");
            }
        }

        private void DisplayMessage(string chatId, string user, string message, string? replyContent = null, string? serverId = null, DateTime? editedAt = null, string? replyToMessageId = null)
        {
            if (currentChatId == null || chatId != currentChatId || string.IsNullOrEmpty(message))
                return;

            var timestamp = DateTime.Now.ToString("HH:mm");
            bool isGuest = (user != name);
            bool hasReply = !string.IsNullOrEmpty(replyContent);
            bool isEdited = editedAt.HasValue;
            
            // Используем класс Chat.Message для единообразия
            var chatMessage = new MainWindow.Chat.Message(hasReply, message, timestamp, isGuest, replyContent, serverId, isEdited, replyToMessageId);
            var bubble = chatMessage.Bubble;

            // Кешируем сообщение по serverId для быстрого доступа при редактировании
            if (!string.IsNullOrEmpty(serverId))
            {
                _messageCache[serverId] = chatMessage;
            }

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
                // Передаём replyToMessageId если это ответ
                string? replyMessageId = isReplied ? replyToMessageId : null;
                await _connection.InvokeAsync("SendMessage", currentChatId, name, messageText, replyMessageId);
                
                // Очищаем replyContent и replyId после отправки
                replyToMessageContent = "";
                replyToMessageId = "";
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
