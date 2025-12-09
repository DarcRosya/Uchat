using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Uchat.Services;
using Uchat.Shared.DTOs;
using Uchat.Shared;

namespace Uchat
{
    public class FriendNotification
    {
        public int contactId { get; set; }
        public int chatRoomId { get; set; }
        public string Type { get; set; }
        public string friendUsername { get; set; } = string.Empty;
        public string friendDisplayName { get; set; } = string.Empty;
    }
    
    public partial class MainWindow : Window
    {
        private SemaphoreSlim _chatsLoadingSemaphore = new SemaphoreSlim(1, 1);
        private HubConnection _hubConnection = null!;
        private ChatApiService _chatApiService = null!;
        private MessageApiService _messageApiService = null!;
        public ContactApiService _contactApiService = null!;
        private TextBlock _connectionStatusIndicator = null!;
        private Dictionary<string, MainWindow.Chat.Message> _messageCache = new();
        public Dictionary<int, MainWindow.Chat.Contact> _chatContacts = new();
        private Dictionary<int, string> _messageDrafts = new();
        
        private int? _currentChatId = null;
        private string _currentUsername = "Unknown";
        
        private DateTime? _oldestMessageDate = null;
        private bool _hasMoreMessages = true;
        private bool _isLoadingHistory = false;
        
        // Reconnection tracking
        private bool _isReconnecting = false;
        private int _reconnectionAttempts = 0;
        private const int MaxReconnectionAttempts = 10;
        private const int InitialReconnectionDelayMs = 1000;
        
        private void InitializeChatComponents()
        {
            _currentUsername = UserSession.Instance.Username ?? "Unknown";
            
            _connectionStatusIndicator = this.FindControl<TextBlock>("ConnectionStatusText") ?? new TextBlock();
            
            var token = UserSession.Instance.AccessToken ?? string.Empty;
            
            Logger.Log($"Initializing API services for user: {_currentUsername}");
            Logger.Log($"Token length: {token.Length}, Token preview: {(token.Length > 10 ? token.Substring(0, 10) + "..." : token)}");

            _chatApiService = new ChatApiService(systemArgs);
            _chatApiService.SetAuthToken(token);
            
            _messageApiService = new MessageApiService(systemArgs);
            _messageApiService.SetAuthToken(token);
            
            _contactApiService = new ContactApiService(systemArgs);
            _contactApiService.SetAuthToken(token);
            
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
                .WithUrl($"{ConnectionConfig.GetServerUrl(systemArgs)}/chatHub?access_token={token}", options =>
                {
                    options.AccessTokenProvider = () => Task.FromResult<string?>(token);
                    
                    options.HttpMessageHandlerFactory = (message) =>
                    {
                        if (message is HttpClientHandler clientHandler)
                        {
                            clientHandler.ServerCertificateCustomValidationCallback +=
                                (sender, certificate, chain, sslPolicyErrors) => { return true; };
                        }
                        return message;
                    };
                })
                .WithAutomaticReconnect(new ExponentialBackoffRetryPolicy())
                .WithStatefulReconnect()
                .Build();

            RegisterSignalRHandlers();

            try
            {
                UpdateConnectionStatus("● Connecting...", Brushes.Orange);
                
                await _hubConnection.StartAsync();
                
                UpdateConnectionStatus("● Connected", Brushes.Green);
                _reconnectionAttempts = 0;
                
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
            // Connection state handlers
            _hubConnection.Reconnecting += async (error) =>
            {
                _isReconnecting = true;
                _reconnectionAttempts++;
                
                UpdateConnectionStatus("● Reconnecting...", Brushes.Orange);
                Logger.Log($"[RECONNECT] Attempting reconnection #{_reconnectionAttempts}");
                
                if (error != null)
                {
                    Logger.Log($"[RECONNECT ERROR] {error.Message}");
                }
                
                await Task.CompletedTask;
            };

            _hubConnection.Reconnected += async (connectionId) =>
            {
                _isReconnecting = false;
                _reconnectionAttempts = 0;
                
                UpdateConnectionStatus("● Connected", Brushes.Green);
                Logger.Log($"[RECONNECTED] Successfully reconnected with connectionId: {connectionId}");
                
                // Notify server that we've reconnected and need state restoration
                try
                {
                    await _hubConnection.InvokeAsync("OnReconnected");
                    Logger.Log("[RECONNECTED] Server notified of reconnection");
                }
                catch (Exception ex)
                {
                    Logger.Log($"[RECONNECTED ERROR] Failed to notify server: {ex.Message}");
                }
            };

            _hubConnection.Closed += async (error) =>
            {
                if (error != null)
                {
                    Logger.Log($"[CLOSED] Connection closed: {error.Message}");
                    UpdateConnectionStatus($"● Disconnected: {error.Message}", Brushes.Red);
                }
                else
                {
                    Logger.Log("[CLOSED] Connection closed by server");
                    UpdateConnectionStatus("● Disconnected", Brushes.Red);
                }
                
                Logger.Log("[CLOSED] Connection closed, waiting for user action or automatic recovery");
                
                await Task.CompletedTask;
            };

            // Reconnection acknowledgment from server
            _hubConnection.On("ReconnectionComplete", () =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    Logger.Log("[RECONNECTION_COMPLETE] Server confirmed state restoration");
                    UpdateConnectionStatus("● Connected", Brushes.Green);
                });
            });

            // User online/offline notifications
            _hubConnection.On<int>("UserOnline", (userId) =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    Logger.Log($"[USER_ONLINE] User {userId} came online");
                    // Update UI to show user as online
                });
            });

            _hubConnection.On<int>("UserOffline", (userId) =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    Logger.Log($"[USER_OFFLINE] User {userId} went offline");
                    // Update UI to show user as offline
                });
            });

            _hubConnection.On<int>("UserReconnected", (userId) =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (userId == UserSession.Instance.UserId)
                    {
                        Logger.Log("[USER_RECONNECTED] You reconnected to the server");
                    }
                    else
                    {
                        Logger.Log($"[USER_RECONNECTED] User {userId} reconnected");
                    }
                });
            });

            // Message handlers
            _hubConnection.On<MessageDto>("ReceiveMessage", async (message) =>
            {
                Dispatcher.UIThread.Post(async () =>
                {
                    bool isCurrentChat = _currentChatId != null && message.ChatRoomId == _currentChatId.Value;

                    if (_chatContacts.TryGetValue(message.ChatRoomId, out var chatItem))
                    {
                        string preview = message.Content.Length > 30 ? message.Content.Substring(0, 30) + "..." : message.Content;
                        chatItem.UpdateLastMessage(preview);

                        contactsStackPanel.Children.Remove(chatItem.Box); 
                        contactsStackPanel.Children.Insert(0, chatItem.Box);
                    }

                    if (isCurrentChat)
                    {
                        DisplayMessage(message);
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
                            Logger.Log($"Received message for hidden chat {message.ChatRoomId}. Resurrecting in UI...");
                            
                            await LoadUserChatsAsync();
                            
                            if (!_chatContacts.TryGetValue(message.ChatRoomId, out chatItem))
                            {
                                var newContact = new MainWindow.Chat.Contact(
                                    message.Sender.DisplayName ?? message.Sender.Username,
                                    message.Content,
                                    1,
                                    this,
                                    message.ChatRoomId
                                );
                                _chatContacts[message.ChatRoomId] = newContact;
                                contactsStackPanel.Children.Insert(0, newContact.Box);
                            }
                        }
                });
            });
            
            _hubConnection.On<string, string, DateTime>("MessageEdited", (messageId, newContent, editedAt) =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (_messageCache.TryGetValue(messageId, out var cachedMsg))
                    {
                        cachedMsg.Content = newContent;
                        cachedMsg.ContentTextBlock.Text = newContent;
                        AddEditedLabel(cachedMsg);
                        
                        foreach (var msg in _messageCache.Values)
                        {
                            if (msg.ReplyToMessageId == messageId && msg.ReplyTextBlock != null)
                            {
                                msg.ReplyTextBlock.Text = newContent;
                            }
                        }
                    }
                    
                    if (_currentChatId.HasValue && _chatContacts.TryGetValue(_currentChatId.Value, out var chatItem))
                    {
                        string preview = newContent.Length > 30 ? newContent.Substring(0, 30) + "..." : newContent;
                        chatItem.UpdateLastMessage(preview);
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
            
            // Friend request handlers
            _hubConnection.On<Shared.DTOs.ContactDto>("FriendRequestReceived", (contact) =>
            {                                   
                Dispatcher.UIThread.Post(() =>
                {
                    var placeholder = requestList.Children.OfType<TextBlock>()
                        .FirstOrDefault(t => t.Text == "No pending requests");
                    if (placeholder != null)
                    {
                        requestList.Children.Remove(placeholder);
                    }

                    var requestItem = new MainWindow.Chat.FriendRequest(
                        contact.Nickname ?? contact.ContactUsername,
                        contact.ContactUserId,
                        this
                    );

                    requestList.Children.Insert(0, requestItem.Box);
                    requestList.InvalidateVisual();
                    
                    Logger.Log($"Added friend request from {contact.ContactUsername} to notification panel");
                });
            });
            
            _hubConnection.On<object>("FriendRequestAccepted", HandleNewFriendChat);
            _hubConnection.On<object>("FriendAdded", HandleNewFriendChat);
            
            _hubConnection.On<object>("FriendRequestRejected", (data) =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    try 
                    {
                        var json = System.Text.Json.JsonSerializer.Serialize(data);
                        var doc = System.Text.Json.JsonDocument.Parse(json);

                        if (doc.RootElement.TryGetProperty("contactId", out var idProp))
                        {
                            int contactId = idProp.GetInt32();
                            Logger.Log($"Friend request rejected: {contactId}");
                        }
                    }
                    catch (Exception ex) { Logger.Log($"Error parsing friend request rejection: {ex.Message}"); }
                });
            });
            
            _hubConnection.On<int>("FriendRemoved", (chatRoomId) =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    Logger.Log($"Friend removed, chat ID: {chatRoomId}");
                    RemoveChatFromUI(chatRoomId);
                });
            });
        }

        /// <summary>
        /// Manually attempt to reconnect to the server
        /// </summary>
        public async Task ReconnectAsync()
        {
            if (_isReconnecting)
            {
                Logger.Log("[RECONNECT] Already attempting to reconnect");
                return;
            }

            try
            {
                _isReconnecting = true;
                _reconnectionAttempts = 0;
                
                UpdateConnectionStatus("● Reconnecting...", Brushes.Orange);
                Logger.Log("[RECONNECT] Starting manual reconnection");
                
                await _hubConnection.StartAsync();
                
                UpdateConnectionStatus("● Connected", Brushes.Green);
                Logger.Log("[RECONNECT] Successfully reconnected");
            }
            catch (Exception ex)
            {
                _isReconnecting = false;
                var status = GetConnectionErrorMessage(ex);
                UpdateConnectionStatus($"● {status}", Brushes.Red);
                Logger.Log($"[RECONNECT ERROR] {ex.Message}");
            }
        }

        public bool IsOnline()
        {
            return _hubConnection?.State == HubConnectionState.Connected;
        }

        private async Task StartHeartbeat()
        {
            while (true)
            {
                try
                {
                    if (_hubConnection?.State == HubConnectionState.Connected)
                    {
                        await _hubConnection.InvokeAsync("Heartbeat");
                    }
                    
                    await Task.Delay(TimeSpan.FromSeconds(30));
                }
                catch (Exception ex)
                {
                    Logger.Log($"[HEARTBEAT ERROR] {ex.Message}");
                }
            }
        }

        private void ClearChatArea()
        {
            _currentChatId = null;
            ChatMessagesPanel.Children.Clear();
            _messageCache.Clear();
            chatTextBox.Text = string.Empty;
            chatTextBox.IsVisible = false;
            replyTheMessageBox.IsVisible = false;
            
            Logger.Log("Chat area cleared");
        }

        public async Task LoadUserChatsAsync()
        {
            if (_chatsLoadingSemaphore.CurrentCount == 0) return;
            await _chatsLoadingSemaphore.WaitAsync();

            try
            {
                var chats = await _chatApiService.GetUserChatsAsync();
                
                var sortedChats = chats
                    .OrderByDescending(c => c.LastMessageAt ?? DateTime.MinValue)
                    .ToList();

                await Dispatcher.UIThread.InvokeAsync(() => 
                {
                    var currentChatIds = sortedChats.Select(c => c.Id).ToHashSet();

                    var toRemove = _chatContacts.Keys.Where(k => !currentChatIds.Contains(k)).ToList();
                    foreach (var id in toRemove)
                    {
                        if (_chatContacts.TryGetValue(id, out var contactToRemove))
                        {
                            contactsStackPanel.Children.Remove(contactToRemove.Box);
                            _chatContacts.Remove(id);
                        }
                    }

                    for (int i = 0; i < sortedChats.Count; i++)
                    {
                        var chat = sortedChats[i];
                        bool isGroup = chat.Type != "DirectMessage";
                        
                        string previewText;

                        if (!string.IsNullOrEmpty(chat.LastMessageContent))
                        {
                            previewText = chat.LastMessageContent;
                        }
                        else
                        {
                            if (chat.Name == "Notes")
                            {
                                previewText = string.Empty;
                            }
                            else if (chat.Type == "DirectMessage")
                            {
                                previewText = "New Friend!";
                            }
                            else
                            {
                                previewText = "New Group!";
                            }
                        }

                        if (_chatContacts.TryGetValue(chat.Id, out var existingContact))
                        {
                            var currentIdx = contactsStackPanel.Children.IndexOf(existingContact.Box);
                            if (currentIdx != i)
                            {
                                contactsStackPanel.Children.Move(currentIdx, i);
                            }
                        }
                        else
                        {
                            var newContact = new MainWindow.Chat.Contact(
                                chat.Name ?? $"Chat {chat.Id}",
                                previewText,
                                chat.UnreadCount,
                                this,
                                chat.Id
                            );
                            
                            newContact.IsGroupChat = isGroup;
                            newContact.IsVisible = isGroup ? Chat.GroupsActive : !Chat.GroupsActive;
                            
                            if (i < contactsStackPanel.Children.Count)
                            {
                                contactsStackPanel.Children.Insert(i, newContact.Box);
                            }
                            else
                            {
                                contactsStackPanel.Children.Add(newContact.Box);
                            }
                            
                            _chatContacts[chat.Id] = newContact;
                        }
                    }
                });
            }
            finally
            {
                _chatsLoadingSemaphore.Release();
            }
        }

        public async Task OpenChatAsync(int chatId)
        {
            try
            {
                if (_currentChatId.HasValue && !string.IsNullOrWhiteSpace(chatTextBox.Text))
                {
                    _messageDrafts[_currentChatId.Value] = chatTextBox.Text;
                    Logger.Log($"Saved draft for chat {_currentChatId.Value}");
                }
                
                _currentChatId = chatId;
                
                Dispatcher.UIThread.Post(() =>
                {
                    ChatMessagesPanel.Children.Clear();
                    _messageCache.Clear();
                    
                    if (_messageDrafts.TryGetValue(chatId, out var draft))
                    {
                        chatTextBox.Text = draft;
                        Logger.Log($"Restored draft for chat {chatId}");
                    }
                    else
                    {
                        chatTextBox.Text = string.Empty;
                    }
                    
                    chatTextBox.IsVisible = true;
                    chatTextBox.IsEnabled = true;
                    replyTheMessageBox.IsVisible = false;
                });
                
                try
                {
                    await _hubConnection.InvokeAsync("JoinChatGroup", chatId);
                }
                catch (Exception ex)
                {
                    Logger.Log($"WARNING: Failed to join chat group {chatId}: {ex.Message}");
                }
                
                await LoadChatHistoryAsync(chatId);
            }
            catch (Exception ex)
            {
                Logger.Log($"Error opening chat: {ex.Message}");
            }
        }

        public void RemoveChatFromUI(int chatRoomId)
        {
            if (_chatContacts.TryGetValue(chatRoomId, out var contact))
            {
                contactsStackPanel.Children.Remove(contact.Box);

                if (Chat.chatsList.Contains(contact))
                {
                    Chat.chatsList.Remove(contact);
                }

                _chatContacts.Remove(chatRoomId);
                _messageDrafts.Remove(chatRoomId);
                
                Logger.Log($"Chat {chatRoomId} removed from UI and Cache");
            }

            if (_currentChatId.HasValue && _currentChatId.Value == chatRoomId)
            {
                ClearChatArea();
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
                    
                    _hasMoreMessages = result.Pagination.HasMore;
                    if (messages.Count > 0)
                    {
                        _oldestMessageDate = messages[0].SentAt;
                    }
                    
                    foreach (var msg in messages)
                    {
                        msg.ChatRoomId = chatId;
                        DisplayMessage(msg);
                    }
                    
                    ChatScrollViewer.ScrollToEnd();
                });
            }
            catch (Exception ex)
            {
                Logger.Log($"[ERROR] LoadChatHistoryAsync failed: {ex}");
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
                            msg.Id, msg.EditedAt.HasValue, msg.ReplyToMessageId,
                            msg.Sender.DisplayName ?? msg.Sender.Username
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
                Logger.Log($"Error loading history: {ex.Message}");
            }
            finally
            {
                await Task.Delay(200); 
                _isLoadingHistory = false;
            }
        }

        public void OnChatScrollChanged(object? sender, ScrollChangedEventArgs e)
        {
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

        private void HandleNewFriendChat(object data)
        {
            Dispatcher.UIThread.Post(async () =>
            {
                try
                {
                    var options = new System.Text.Json.JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };

                    var json = System.Text.Json.JsonSerializer.Serialize(data);
                    var notification = System.Text.Json.JsonSerializer.Deserialize<FriendNotification>(json, options);

                    if (notification != null && notification.chatRoomId > 0)
                    {
                        _ = _hubConnection.InvokeAsync("JoinChatGroup", notification.chatRoomId)
                            .ContinueWith(t => Logger.Log($"SignalR Join Error: {t.Exception?.InnerException?.Message}"), TaskContinuationOptions.OnlyOnFaulted);

                        string initMsg = notification.Type switch
                        {
                            "DirectMessage" => "New Friend!",
                            _ => "New Group!"
                        };

                        if (!_chatContacts.TryGetValue(notification.chatRoomId, out var contact))
                        {
                            contact = new MainWindow.Chat.Contact(
                                notification.friendDisplayName, 
                                initMsg, 
                                0, 
                                this, 
                                notification.chatRoomId
                            );
                            contact.AddMember(Chat.ClientName);
                            contact.AddMember(notification.friendDisplayName);

                            _chatContacts[notification.chatRoomId] = contact;
                            contactsStackPanel.Children.Insert(0, contact.Box);
                        }
                        else 
                        {
                            contact.UpdateLastMessage(initMsg);

                            contactsStackPanel.Children.Remove(contact.Box);
                            contactsStackPanel.Children.Insert(0, contact.Box);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log($"Error handling New Friend Chat: {ex.Message}");
                }
            });
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
            string? replyToName = message.ReplyTo?.SenderName;
            string? replyContent = message.ReplyTo?.Content;
            
            var chatMessage = new MainWindow.Chat.Message(
                hasReply,
                message.Content,
                timestamp,
                isGuest,
                replyContent,
                message.Id,
                message.EditedAt.HasValue,
                message.ReplyToMessageId,
                message.Sender.DisplayName ?? message.Sender.Username,
                replyToName
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
            
            if (_chatContacts.TryGetValue(_currentChatId.Value, out var contact))
            {
                contact.UpdateLastMessage(messageText);

                contactsStackPanel.Children.Remove(contact.Box);
                contactsStackPanel.Children.Insert(0, contact.Box);
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
            catch
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
            catch
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
            catch
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

        private void groupInfoAddMemberButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            AddPersonToGroup.IsVisible = true;
        }

        private void groupInfoLeaveButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            LeaveGroupAndConfirm.IsVisible = true;
        }

        private void ConfirmLeaveGroup_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            LeaveGroupAndConfirm.IsVisible = false;
            groupInfoBox.IsVisible = false;
        }

        private void CancelLeaveGroup_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            LeaveGroupAndConfirm.IsVisible = false;
        }

        private void InvitePersonToChat_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            // ЛОГИКА ДОБАВЛЕНИЕ ЧУВАКА!
        }

        private void CancelAddingGroup_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            AddPersonToGroup.IsVisible = false;
        }
    }
}
