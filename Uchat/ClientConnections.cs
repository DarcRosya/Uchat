using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Uchat.Services;
using Uchat.Shared.DTOs;

namespace Uchat
{
    public partial class MainWindow : Window
    {
        private HubConnection _hubConnection = null!;
        private ChatApiService _chatApiService = null!;
        private MessageApiService _messageApiService = null!;
        private TextBlock _connectionStatusIndicator = null!;
        private Dictionary<string, MainWindow.Chat.Message> _messageCache = new();
        
        private int? _currentChatId = null;
        private string _currentUsername = "Unknown";
        
        private DateTime? _oldestMessageDate = null;
        private bool _hasMoreMessages = true;
        private bool _isLoadingHistory = false;
        
        private void InitializeChatComponents()
        {
            _currentUsername = UserSession.Instance.Username ?? "Unknown";
            
            _connectionStatusIndicator = this.FindControl<TextBlock>("ConnectionStatusText") ?? new TextBlock();
            
            var token = UserSession.Instance.AccessToken ?? string.Empty;
            
            _chatApiService = new ChatApiService();
            _chatApiService.SetAuthToken(token);
            
            _messageApiService = new MessageApiService();
            _messageApiService.SetAuthToken(token);
            
            ConnectToSignalR();
        }        

        private async void ConnectToSignalR()
        {
            var token = UserSession.Instance.AccessToken;
            
            if (string.IsNullOrEmpty(token))
            {
                UpdateConnectionStatus("● No token - Login first!", Brushes.Red);
                return;
            }
            
            _hubConnection = new HubConnectionBuilder()
                .WithUrl($"{ServerConfig.ServerUrl}/chatHub?access_token={token}", options =>
                {
                    options.AccessTokenProvider = () => Task.FromResult<string?>(token);
                })
                .WithAutomaticReconnect()
                .Build();

            RegisterSignalRHandlers();

            try
            {
                UpdateConnectionStatus("● Connecting...", Brushes.Orange);
                
                await _hubConnection.StartAsync();
                
                UpdateConnectionStatus("● Connected", Brushes.Green);
                
                await LoadUserChatsAsync();
            }
            catch (Exception ex)
            {
                var status = GetConnectionErrorMessage(ex);
                UpdateConnectionStatus($"● {status}", Brushes.Red);
            }
        }

        private void RegisterSignalRHandlers()
        {
            _hubConnection.On<MessageDto>("ReceiveMessage", (message) =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    bool isCurrentChat = _currentChatId != null && message.ChatRoomId == _currentChatId.Value;

                    if (isCurrentChat)
                    {
                        // === СЦЕНАРИЙ А: Мы смотрим этот чат ===
                        
                        // 1. Рисуем сообщение
                        DisplayMessage(message);

                        // 2. Если это сообщение от МЕНЯ - значит отправка прошла успешно
                        // Очищаем поле ввода только здесь
                        if (message.Sender.Username == _currentUsername)
                        {
                            replyTheMessageBox.IsVisible = false;
                            chatTextBox.Text = string.Empty;
                        }
                        Dispatcher.UIThread.RunJobs(DispatcherPriority.Render);
                        ChatScrollViewer.ScrollToEnd();
                    }
                    else 
                    {
                        // === СЦЕНАРИЙ Б: Сообщение пришло в фоновый чат ===
                        
                        // Здесь НЕ вызываем DisplayMessage!
                        // Здесь обновляем счетчик в списке слева или кидаем пуш-уведомление
                        Console.WriteLine($"[Notification] New message in chat {message.ChatRoomId} from {message.Sender.Username}");
                        
                        // Пример на будущее:
                        // UpdateUnreadCounter(message.ChatRoomId);
                    }
                });
            });
            
            _hubConnection.On<string, string, DateTime>("MessageEdited", (messageId, newContent, editedAt) =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (_messageCache.TryGetValue(messageId, out var cachedMsg))
                    {
                        cachedMsg.ContentTextBlock.Text = newContent;
                        AddEditedLabel(cachedMsg);
                    }
                });
            });
            
            _hubConnection.On<string>("MessageDeleted", (messageId) =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (_messageCache.TryGetValue(messageId, out var cachedMsg))
                    {
                        RemoveMessageFromUI(cachedMsg);
                        _messageCache.Remove(messageId);
                    }
                    
                    CleanupReplyReferences(messageId);
                });
            });
            
            _hubConnection.On<List<string>>("RepliesCleared", (messageIds) =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    foreach (var msgId in messageIds)
                    {
                        if (_messageCache.TryGetValue(msgId, out var cachedMsg))
                        {
                            RemoveReplyUI(cachedMsg);
                        }
                    }
                });
            });
        }

        private async Task LoadUserChatsAsync()
        {
            try
            {
                var chats = await _chatApiService.GetUserChatsAsync();
                
                Dispatcher.UIThread.Post(() =>
                {
                    if (chats.Count > 0)
                    {
                        var firstChat = chats[0];
                        _ = OpenChatAsync(firstChat.Id);
                    }
                });
            }
            catch (Exception ex)
            {
                // Failed to load chats
            }
        }

        private async Task OpenChatAsync(int chatId)
        {
            try
            {
                _currentChatId = chatId;
                
                await LoadChatHistoryAsync(chatId);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error opening chat: {ex.Message}");
            }
        }

        private async Task LoadChatHistoryAsync(int chatId, int limit = 50)
        {
            try
            {
                var result = await _messageApiService.GetMessagesAsync(chatId, limit);
                
                if (result == null)
                {
                    return;
                }
                
                Dispatcher.UIThread.Post(() =>
                {
                    ChatMessagesPanel.Children.Clear();
                    _messageCache.Clear();
                    
                    var messages = result.Messages;
                    messages.Reverse();
                    
                    // Обновляем состояние пагинации
                    _hasMoreMessages = result.Pagination.HasMore;
                    if (messages.Count > 0)
                    {
                        _oldestMessageDate = messages[0].SentAt; // Самое старое сообщение
                    }
                    
                    foreach (var msg in messages)
                    {
                        DisplayMessage(msg);
                    }
                    
                    ChatScrollViewer.ScrollToEnd();
                });
            }
            catch (Exception ex)
            {
                // Failed to load chat history
            }
        }

private async Task LoadMoreHistoryAsync()
{
    if (!_currentChatId.HasValue || !_hasMoreMessages || _isLoadingHistory || !_oldestMessageDate.HasValue)
    {
        return;
    }

    _isLoadingHistory = true;

    try
    {
        var result = await _messageApiService.GetMessagesAsync(
            _currentChatId.Value, 
            limit: 30, 
            before: _oldestMessageDate.Value
        );

        if (result == null || result.Messages.Count == 0)
        {
            _hasMoreMessages = false;
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var scrollViewer = ChatScrollViewer;

            double oldExtentHeight = scrollViewer.Extent.Height;
            double oldOffset = scrollViewer.Offset.Y;
            
            var messages = result.Messages;
            messages.Reverse();

            _hasMoreMessages = result.Pagination.HasMore;
            _oldestMessageDate = messages[0].SentAt;

            var newControls = new List<Control>();

            for (int i = messages.Count - 1; i >= 0; i--)
            {
                var msg = messages[i];

                var timestamp = msg.SentAt.ToLocalTime().ToString("HH:mm");
                bool isGuest = (msg.Sender.Username != _currentUsername);
                bool hasReply = msg.ReplyTo != null;
                string? replyContent = msg.ReplyTo?.Content;

                var chatMessage = new MainWindow.Chat.Message(
                    hasReply, msg.Content, timestamp, isGuest, replyContent,
                    msg.Id, msg.EditedAt.HasValue, msg.ReplyToMessageId
                );

                _messageCache[msg.Id] = chatMessage;

                var grid = new Grid
                {
                    ColumnDefinitions = { new ColumnDefinition(new GridLength(1, GridUnitType.Star)) }
                };

                Grid.SetColumn(chatMessage.Bubble, 0);
                grid.Children.Add(chatMessage.Bubble);

                var messageContextMenu = new MainWindow.Chat.MessageContextMenu(this, chatMessage, grid);
                chatMessage.Bubble.ContextMenu = messageContextMenu.Result();
                // -------------------------------------
                
                newControls.Add(grid);
            }

            ChatMessagesPanel.Children.InsertRange(0, newControls);

            Dispatcher.UIThread.RunJobs(DispatcherPriority.Render);

            double newExtentHeight = scrollViewer.Extent.Height;
            
            double heightDifference = newExtentHeight - oldExtentHeight;

            if (heightDifference > 0)
            {
                scrollViewer.Offset = new Vector(0, oldOffset + heightDifference);
            }
        });
    }
    catch (Exception ex)
    {
        System.Diagnostics.Debug.WriteLine($"Error loading history: {ex.Message}");
    }
    finally
    {
        // Небольшая задержка, чтобы UI не дергался
        await Task.Delay(200); 
        _isLoadingHistory = false;
    }
}

        public void OnChatScrollChanged(object? sender, ScrollChangedEventArgs e)
        {
            // Строгая проверка флагов
            if (_isLoadingHistory || !_hasMoreMessages)
                return;

            var scrollViewer = sender as ScrollViewer;
            if (scrollViewer == null)
                return;

            if (scrollViewer.Extent.Height <= scrollViewer.Viewport.Height)
                return;

            if (scrollViewer.Offset.Y < 50)
            {
                _ = LoadMoreHistoryAsync();
            }
        }

        private void DisplayMessage(MessageDto message)
        {
            if (_currentChatId == null || message.ChatRoomId != _currentChatId.Value)
            {
                return;
            }

            var timestamp = message.SentAt.ToLocalTime().ToString("HH:mm");
            bool isGuest = (message.Sender.Username != _currentUsername);
            bool hasReply = message.ReplyTo != null;
            string? replyContent = message.ReplyTo?.Content;
            
            var chatMessage = new MainWindow.Chat.Message(
                hasReply,
                message.Content,
                timestamp,
                isGuest,
                replyContent,
                message.Id,
                message.EditedAt.HasValue,
                message.ReplyToMessageId
            );
            
            _messageCache[message.Id] = chatMessage;

            var grid = new Grid
            {
                ColumnDefinitions = { new ColumnDefinition(new GridLength(1, GridUnitType.Star)) }
            };

            Grid.SetColumn(chatMessage.Bubble, 0);
            grid.Children.Add(chatMessage.Bubble);

            var messageContextMenu = new MainWindow.Chat.MessageContextMenu(this, chatMessage, grid);
            chatMessage.Bubble.ContextMenu = messageContextMenu.Result();

            ChatMessagesPanel.Children.Add(grid);
        }

        public async Task SendMessageToServerAsync(string messageText)
        {
            if (string.IsNullOrWhiteSpace(messageText) || !_currentChatId.HasValue)
            {
                return;
            }
            
            try
            {
                var dto = new MessageCreateDto
                {
                    ChatRoomId = _currentChatId.Value,
                    SenderId = UserSession.Instance.UserId,
                    Content = messageText,
                    Type = "text",
                    ReplyToMessageId = isReplied ? replyToMessageId : null
                };
                
                var sentMessage = await _messageApiService.SendMessageAsync(_currentChatId.Value, dto);
                
                replyToMessageContent = "";
                replyToMessageId = "";
                isReplied = false;
            }
            catch (Exception ex)
            {
                UpdateConnectionStatus("● Send failed", Brushes.Red);
            }
        }

        public async Task EditMessageAsync(string messageId, string newContent)
        {
            if (!_currentChatId.HasValue || string.IsNullOrWhiteSpace(newContent))
            {
                return;
            }
            
            try
            {
                await _messageApiService.EditMessageAsync(_currentChatId.Value, messageId, newContent);
            }
            catch (Exception ex)
            {
                // Edit failed
            }
        }

        public async Task DeleteMessageAsync(string messageId)
        {
            if (!_currentChatId.HasValue)
            {
                return;
            }
            
            try
            {
                await _messageApiService.DeleteMessageAsync(_currentChatId.Value, messageId);
            }
            catch (Exception ex)
            {
                // Delete failed
            }
        }


        private void UpdateConnectionStatus(string text, IBrush color)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (_connectionStatusIndicator != null)
                {
                    _connectionStatusIndicator.Text = text;
                    _connectionStatusIndicator.Foreground = color;
                }
            });
        }

        private string GetConnectionErrorMessage(Exception ex)
        {
            if (ex.InnerException?.Message.Contains("401") == true)
                return "Unauthorized";
            if (ex.InnerException?.Message.Contains("404") == true)
                return "Server not found";
            if (ex.Message.Contains("Connection refused") || ex.Message.Contains("No connection"))
                return "Server offline";
            
            return "Connection failed";
        }

        private void AddEditedLabel(MainWindow.Chat.Message message)
        {
            var timeStackPanel = message.Bubble.Child as StackPanel;
            if (timeStackPanel == null) return;
            
            var lastChild = timeStackPanel.Children[timeStackPanel.Children.Count - 1] as StackPanel;
            if (lastChild == null) return;
            
            bool hasEditedLabel = lastChild.Children.OfType<TextBlock>()
                .Any(tb => tb.Text == "edited");
            
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

        private void RemoveMessageFromUI(MainWindow.Chat.Message message)
        {
            var bubble = message.Bubble;
            if (bubble.Parent is Grid messageGrid && messageGrid.Parent == ChatMessagesPanel)
            {
                ChatMessagesPanel.Children.Remove(messageGrid);
            }
        }

        private void CleanupReplyReferences(string deletedMessageId)
        {
            foreach (var kvp in _messageCache.ToList())
            {
                var msg = kvp.Value;
                
                if (msg.ReplyToMessageId == deletedMessageId)
                {
                    msg.ReplyToMessageId = null;
                    
                    RemoveReplyUI(msg);
                }
            }
        }
        
        private void RemoveReplyUI(MainWindow.Chat.Message message)
        {
            if (message.ReplyPreviewBorder != null)
            {
                if (message.ReplyPreviewBorder.Parent is Panel parentPanel)
                {
                    parentPanel.Children.Remove(message.ReplyPreviewBorder);
                    message.ReplyPreviewBorder = null;
                    return;
                }
            }

            if (message.Bubble.Child is StackPanel messageStackPanel)
            {
                var replyBorder = messageStackPanel.Children
                    .OfType<Border>()
                    .FirstOrDefault(b => b.Tag?.ToString() == "ReplyBorder");

                if (replyBorder != null)
                {
                    messageStackPanel.Children.Remove(replyBorder);
                }
            }
        }
    }
}
