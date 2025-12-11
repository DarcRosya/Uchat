using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Uchat.Services;
using Uchat.Shared;
using Uchat.Shared.DTOs;

namespace Uchat
{
    public class FriendNotification
    {
        public int contactId { get; set; }
        public int chatRoomId { get; set; }
        public string Type { get; set; } // "DirectMessage" or "GroupInvite"
        public string friendUsername { get; set; } = string.Empty;
        public string friendDisplayName { get; set; } = string.Empty;

        public string GroupName { get; set; }
        public string InviterUsername { get; set; }
    }

    public partial class MainWindow : Window
    {
        private SemaphoreSlim _chatsLoadingSemaphore = new SemaphoreSlim(1, 1);
        private HubConnection _hubConnection = null!;
        private ChatApiService _chatApiService = null!;
        private MessageApiService _messageApiService = null!;
        public ContactApiService _contactApiService = null!;
        private TextBlock _connectionStatusIndicator = null!;
        private TextBlock _userStatusLabel = null!;
        private Border _userStatusDot = null!;
        private Dictionary<string, MainWindow.Chat.Message> _messageCache = new();
        public Dictionary<int, MainWindow.Chat.Contact> _chatContacts = new();
        private Dictionary<int, string> _messageDrafts = new();
        private readonly Dictionary<int, DateTime> _lastReadProgressTimestamps = new();
        private readonly HashSet<int> _onlineUsers = new();
        private readonly object _onlineUsersLock = new();

        private int? _currentChatId = null;
        private string _currentUsername = "Unknown";

        private DateTime? _oldestMessageDate = null;
        private bool _hasMoreMessages = true;
        private bool _isLoadingHistory = false;

        private SignalRReconnectionHandler? _reconnectionHandler;
        private HeartbeatService? _heartbeatService;
        private bool _isProgrammaticScroll = false;

        private void InitializeChatComponents()
        {
            _currentUsername = UserSession.Instance.Username ?? "Unknown";

            _connectionStatusIndicator = this.FindControl<TextBlock>("ConnectionStatusText") ?? new TextBlock();
            _userStatusLabel = this.FindControl<TextBlock>("userStatusLabel") ?? new TextBlock();
            _userStatusDot = this.FindControl<Border>("userStatusDot") ?? new Border();

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

            lock (_onlineUsersLock)
            {
                _onlineUsers.Clear();
            }

            if (_hubConnection != null)
            {
                await _hubConnection.DisposeAsync();
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
                .WithAutomaticReconnect()
                .Build();

            _hubConnection.Reconnected += async (connectionId) =>
            {
                await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    Logger.Log("[SIGNALR] Connection restored. Refreshing state...");
                    UpdateConnectionStatus("● Connected", Brushes.Green);

                    // 1. Refresh Sidebar, Update Last Messages, and Re-Join SignalR Groups
                    // Your existing LoadUserChatsAsync already contains the logic to 
                    // loop through chats and call _hubConnection.InvokeAsync("JoinChatGroup", ...)
                    await LoadUserChatsAsync();

                    // 2. If a chat is currently open, refresh its history to catch up on missed messages
                    if (_currentChatId.HasValue)
                    {
                        // Optionally verify we are still part of this chat
                        if (_currentChatId.HasValue)
                        {
                            Logger.Log($"[SIGNALR] Refreshing active chat {_currentChatId.Value}");
                            
                            // На всякий случай явно вступаем в группу текущего чата прямо сейчас
                            try 
                            {
                                await _hubConnection.InvokeAsync("JoinChatGroup", _currentChatId.Value);
                            }
                            catch { /* игнорируем, если не вышло */ }

                            // Подгружаем историю (без скролла, просто чтобы заполнить дыры)
                            await LoadChatHistoryAsync(_currentChatId.Value);
                        }
                    }
                });
            };

            // Setup reconnection handlers
            _reconnectionHandler = new SignalRReconnectionHandler(_hubConnection, UpdateConnectionStatus);
            _reconnectionHandler.SetupReconnectionHandlers();

            RegisterSignalRHandlers();

            try
            {
                UpdateConnectionStatus("● Connecting...", Brushes.Orange);

                await _hubConnection.StartAsync();

                UpdateConnectionStatus("● Connected", Brushes.Green);
                _reconnectionHandler.ResetReconnectionState();

                // Start heartbeat service
                _heartbeatService = new HeartbeatService(_hubConnection);
                _heartbeatService.StartHeartbeat();

                await LoadUserChatsAsync();
            }
            catch (Exception ex)
            {
                var status = GetConnectionErrorMessage(ex);
                UpdateConnectionStatus($"● {status}", Brushes.Red);
                _heartbeatService?.StopHeartbeat();
            }
        }

        private void RegisterSignalRHandlers()
        {
            // Register reconnection events
            _hubConnection.On("UserReconnected", () =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    Logger.Log("[RECONNECTION] Server confirmed reconnection");
                });
            });

            _hubConnection.On("Pong", () =>
            {
                // Heartbeat response - connection is alive
                Logger.Log("[HEARTBEAT] Pong received");
            });

            _hubConnection.On<MessageDto>("ReceiveMessage", async (message) =>
            {
                await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    bool isCurrentChat = _currentChatId != null && message.ChatRoomId == _currentChatId.Value;

                    // 1. Обновляем/создаем контакт в списке (твой код)
                    if (!_chatContacts.TryGetValue(message.ChatRoomId, out var chatItem))
                    {
                        await LoadUserChatsAsync();
                        if (!_chatContacts.TryGetValue(message.ChatRoomId, out chatItem)) return;
                    }

                    string preview = message.Content.Length > 30 ? message.Content.Substring(0, 30) + "..." : message.Content;
                    chatItem.UpdateLastMessage(preview);
                    UpdateChatListPosition(chatItem);

                    if (isCurrentChat)
                    {
                        // Проверяем, находимся ли мы внизу
                        // (Offset + Viewport) >= Extent - (высота пары сообщений ~100px)
                        bool isUserAtBottom = ChatScrollViewer.Offset.Y >= (ChatScrollViewer.Extent.Height - ChatScrollViewer.Viewport.Height - 100);

                        // Если сообщение от меня — считаем, что мы всегда внизу
                        if (message.Sender.Username == _currentUsername) isUserAtBottom = true;

                        DisplayMessage(message);

                        if (message.Sender.Username == _currentUsername)
                        {
                            replyTheMessageBox.IsVisible = false;
                            chatTextBox.Text = string.Empty;
                        }

                        // Ждем отрисовки нового сообщения
                        Dispatcher.UIThread.RunJobs(DispatcherPriority.Render);

                        if (isUserAtBottom)
                        {
                            // Если были внизу — автоскролл
                            ChatScrollViewer.ScrollToEnd();
                            
                            // И помечаем прочитанным
                            Dispatcher.UIThread.Post(() => CheckVisibilityAndMarkAsRead(), DispatcherPriority.Background);
                        }
                        else
                        {
                            // [ВАЖНО] Если мы читали историю наверху — увеличиваем счетчик!
                            // Пользователь НЕ видел это сообщение.
                            chatItem.IncrementUnread();
                            
                            // Тут можно добавить кнопку "Вниз ⬇️"
                        }
                    }
                    else
                    {
                        // Чат закрыт — просто инкремент
                        chatItem.IncrementUnread();
                        chatItem.ShowUnreadMessages();
                    }
                });
            });

            _hubConnection.On<string, string, DateTime>("MessageEdited", (messageId, newContent, editedAt) =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (_messageCache.TryGetValue(messageId, out var cachedMsg))
                    {
                        cachedMsg.Content = newContent; // Update internal content field
                        cachedMsg.ContentTextBlock.Text = newContent;
                        AddEditedLabel(cachedMsg);

                        // Update all reply previews that reference this message
                        foreach (var msg in _messageCache.Values)
                        {
                            if (msg.ReplyToMessageId == messageId && msg.ReplyTextBlock != null)
                            {
                                msg.ReplyTextBlock.Text = newContent;
                            }
                        }
                    }

                    // FIX 1: Update LastMessage in the sidebar if it is the last message
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

            _hubConnection.On<int, string, DateTime>("ChatLastMessageUpdated", (chatId, newContent, newTime) =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (_chatContacts.TryGetValue(chatId, out var chatItem))
                    {
                        string displayContent = string.IsNullOrEmpty(newContent) ? "" : 
                                                (newContent.Length > 30 ? newContent.Substring(0, 30) + "..." : newContent);
                        
                        chatItem.UpdateLastMessage(displayContent);

                        chatItem.LastMessageAt = newTime;

                        UpdateChatListPosition(chatItem);
                    }
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

            _hubConnection.On<Shared.DTOs.ContactDto>("FriendRequestReceived", (contact) =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    Console.WriteLine($"Friend request received from {contact.ContactUsername}");

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

            // Handler for when someone accepts YOUR friend request (you are the requester)
            _hubConnection.On<object>("FriendRequestAccepted", HandleNewFriendChat);

            // Handler for when YOU accept someone's friend request (you are the accepter)
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
                            Console.WriteLine($"Friend request rejected: {contactId}");
                        }
                        else if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Number)
                        {
                            int contactId = doc.RootElement.GetInt32();
                            Console.WriteLine($"Friend request rejected: {contactId}");
                        }
                    }
                    catch (Exception ex) { Console.WriteLine(ex); }
                });
            });

            // Handler for when someone removes you from friends or you remove them
            _hubConnection.On<int>("FriendRemoved", (chatRoomId) =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    Logger.Log($"Friend removed, chat ID: {chatRoomId}");
                    RemoveChatFromUI(chatRoomId);
                });
            });

            _hubConnection.On<object>("GroupInviteReceived", (data) =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    try
                    {
                        var json = System.Text.Json.JsonSerializer.Serialize(data);
                        var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                        var notif = System.Text.Json.JsonSerializer.Deserialize<FriendNotification>(json, options);

                        if (notif == null)
                        {
                            return;
                        }

                        // Field mapping if GroupName/InviterUsername came from the server
                        if (notif.Type == "GroupInvite")
                        {
                            if (!string.IsNullOrEmpty(notif.InviterUsername)) notif.friendUsername = notif.InviterUsername;
                            if (!string.IsNullOrEmpty(notif.GroupName)) notif.friendDisplayName = notif.GroupName;
                        }

                        var requestItem = new Chat.FriendRequest(
                            notif.friendUsername, // Who is inviting
                            notif.chatRoomId,     // Chat ID (important for buttons!)
                            this,
                            notif.Type,           // Passing the type!
                            notif.friendDisplayName // Passing the group name
                        );

                        // Удаляем placeholder "No pending requests"
                        var placeholder = requestList.Children.OfType<TextBlock>().FirstOrDefault(t => t.Text == "No pending requests");
                        if (placeholder != null) requestList.Children.Remove(placeholder);

                        requestList.Children.Insert(0, requestItem.Box);
                    }
                    catch (Exception ex) { Console.WriteLine("Group invite error: " + ex); }
                });
            });

            _hubConnection.On<int, string>("ChatNameUpdated", (chatId, newName) =>
            {
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var chatToUpdate = Chat.ChatsList.FirstOrDefault(c => c.ChatId == chatId);

                    if (chatToUpdate != null)
                    {
                        chatToUpdate.UpdateName(newName);
                    }

                    if (_currentChatId == chatId)
                    {
                        groupTopBarName.Text = newName;
                        groupInfoName.Text = newName;
                    }
                });
            });

            _hubConnection.On<int, string>("MemberLeft", (chatId, username) =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (_currentChatId == chatId && groupInfoBox.IsVisible)
                    {
                        RemoveMemberFromUiList(username);
                    }
                });
            });

            _hubConnection.On<int, string>("MemberJoined", (chatId, username) =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (_currentChatId == chatId && groupInfoBox.IsVisible)
                    {
                        AddMemberToUiList(username, false);
                        
                        UpdateMemberCountText();
                    }
                });
            });

            _hubConnection.On<int>("UserOnline", (userId) =>
            {
                if (userId <= 0)
                {
                    return;
                }

                lock (_onlineUsersLock)
                {
                    _onlineUsers.Add(userId);
                }

                Dispatcher.UIThread.Post(() => RefreshContactsForUser(userId));
            });

            _hubConnection.On<int>("UserOffline", (userId) =>
            {
                if (userId <= 0)
                {
                    return;
                }

                lock (_onlineUsersLock)
                {
                    _onlineUsers.Remove(userId);
                }

                Dispatcher.UIThread.Post(() => RefreshContactsForUser(userId));
            });
        }

        private void ClearChatArea()
        {
            _currentChatId = null;
            ChatMessagesPanel.Children.Clear();
            _messageCache.Clear();
            chatTextBox.Text = string.Empty;
            chatTextBox.IsVisible = false;
            replyTheMessageBox.IsVisible = false;
            BottomContainer.IsVisible = false;
            PlaceHolder.IsVisible = true;

            groupTopBar.IsVisible = false;
            friendTopBar.IsVisible = true;
            friendTopBarName.Text = "";

            if (groupInfoBox != null)
            {
                groupInfoBox.IsVisible = false;
                backgroundForGroupInfo.IsVisible = false;
            }
            
            Logger.Log("Chat area cleared");
        }

        private bool IsUserOnline(int userId)
        {
            lock (_onlineUsersLock)
            {
                return _onlineUsers.Contains(userId);
            }
        }

        private void RefreshContactPresence(MainWindow.Chat.Contact contact)
        {
            if (contact == null)
            {
                return;
            }

            if (string.Equals(contact.ChatName, "Notes", StringComparison.OrdinalIgnoreCase))
            {
                contact.UpdatePresence(false);
                return;
            }

            var userId = UserSession.Instance.UserId;
            var others = contact.ParticipantIds
                .Where(id => id != userId)
                .Distinct()
                .ToList();

            if (!others.Any())
            {
                contact.UpdatePresence(false);
                return;
            }

            var someoneElseOnline = others.Any(IsUserOnline);
            contact.UpdatePresence(someoneElseOnline);
        }

        private void RefreshContactsForUser(int userId)
        {
            foreach (var contact in _chatContacts.Values)
            {
                if (contact.ParticipantIds.Contains(userId))
                {
                    RefreshContactPresence(contact);
                }
            }
        }

        public async Task LoadUserChatsAsync()
        {
            if (_chatsLoadingSemaphore.CurrentCount == 0) return;
            await _chatsLoadingSemaphore.WaitAsync();

            try
            {
                var chats = await _chatApiService.GetUserChatsAsync();

                await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    // 1. Создаем список ID для проверки удаленных чатов
                    var currentChatIds = chats.Select(c => c.Id).ToHashSet();

                    // 2. Удаляем из UI чаты, которых больше нет в списке (если вдруг удалили чат)
                    var toRemove = _chatContacts.Keys.Where(k => !currentChatIds.Contains(k)).ToList();
                    foreach (var id in toRemove)
                    {
                        if (_chatContacts.TryGetValue(id, out var contactToRemove))
                        {
                            contactsStackPanel.Children.Remove(contactToRemove.Box);
                            _chatContacts.Remove(id);
                        }
                    }

                    // Update or add chats
                    // Use index i to place the chat in the correct position in the UI (for sorting)
                    for (int i = 0; i < chats.Count; i++)
                    {
                        var chat = chats[i];
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
                            existingContact.SetParticipants(chat.ParticipantIds);
                            
                            if (chat.ClearedHistoryAt.HasValue)
                            {
                                existingContact.ClearedHistoryAt = chat.ClearedHistoryAt;
                            }
                            else if (existingContact.ClearedHistoryAt == null)
                            {
                                existingContact.ClearedHistoryAt = null; 
                            }

                            existingContact.UpdateLastMessage(previewText, chat.UnreadCount);
                            existingContact.ClearedHistoryAt = chat.ClearedHistoryAt;
                            existingContact.UpdateLastMessage(previewText, chat.UnreadCount);
                            existingContact.IsGroupChat = isGroup;
                            RefreshContactPresence(existingContact);

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
                                chat.Id,
                                chat.ParticipantIds
                            );
                            newContact.ClearedHistoryAt = chat.ClearedHistoryAt;

                            newContact.IsGroupChat = isGroup;
                            newContact.IsPinned = chat.IsPinned;
                            newContact.PinnedAt = chat.PinnedAt ?? DateTime.MinValue;
                            newContact.LastMessageAt = chat.LastMessageAt ?? DateTime.MinValue;

                            newContact.IsVisible = (isGroup == Chat.GroupsActive);
                            RefreshContactPresence(newContact);

                            if (i < contactsStackPanel.Children.Count)
                                contactsStackPanel.Children.Insert(i, newContact.Box);
                            else
                                contactsStackPanel.Children.Add(newContact.Box);

                            _chatContacts[chat.Id] = newContact;
                        }
                    }
                    SearchTextBox_TextChanged(null, null);

                    if (_hubConnection.State == HubConnectionState.Connected)
                    {
                        foreach (var chat in chats)
                        {
                            try
                            {
                                await _hubConnection.InvokeAsync("JoinChatGroup", chat.Id);
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Error joining group {chat.Id}: {ex.Message}");
                            }
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
                }

                _currentChatId = chatId;

                string chatName = "Chat";
                bool isGroup = false;

                if (_chatContacts.TryGetValue(chatId, out var contact))
                {
                    chatName = contact.ChatName;
                    isGroup = contact.IsGroupChat;
                    // contact.SetUnreadCount(0);
                }
                else 
                {
                    chatName = "Loading...";
                }

                Dispatcher.UIThread.Post(() =>
                {
                    ChatMessagesPanel.Children.Clear();
                    _messageCache.Clear();
                    PlaceHolder.IsVisible = false;

                    chatTextBox.IsVisible = true;   
                    chatTextBox.IsEnabled = true;

                    if (sendButton != null) 
                        sendButton.IsEnabled = true;  
            
                    if (BottomContainer != null)
                        BottomContainer.IsVisible = true;

                    if (_messageDrafts.TryGetValue(chatId, out var draft))
                    {
                        chatTextBox.Text = draft;
                        Logger.Log($"Restored draft for chat {chatId}");
                    }
                    else
                    {
                        chatTextBox.Text = string.Empty;
                    }

                    if (AddPersonToGroup != null) AddPersonToGroup.IsVisible = false;
                    if (LeaveGroupAndConfirm != null) LeaveGroupAndConfirm.IsVisible = false;
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
                System.Diagnostics.Debug.WriteLine($"Error opening chat: {ex.Message}");
            }
        }

        public void RemoveChatFromUI(int chatRoomId)
        {
            if (_chatContacts.TryGetValue(chatRoomId, out var contact))
            {
                contactsStackPanel.Children.Remove(contact.Box);

                if (Chat.ChatsList.Contains(contact))
                {
                    Chat.ChatsList.Remove(contact);
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
            _isProgrammaticScroll = true;

            try
            {
                var result = await _messageApiService.GetMessagesAsync(chatId, limit);

                if (_currentChatId != chatId) return;

                if (result == null)
                {
                    _isProgrammaticScroll = false;
                    return;
                }

                var historyMessages = result.Messages ?? new List<MessageDto>();
                int unreadCount = 0;
                DateTime? visibleFrom = null;

                if (_chatContacts.TryGetValue(chatId, out var contact))
                {
                    unreadCount = contact.UnreadCount;
                    visibleFrom = contact.ClearedHistoryAt; // Берем дату очистки/входа
                }

                await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    if (_currentChatId != chatId) return;
                    _isProgrammaticScroll = true; 

                    ChatMessagesPanel.Opacity = 0; 
                    ChatMessagesPanel.Children.Clear();
                    _messageCache.Clear();

                    var messages = historyMessages.ToList();
                    if (visibleFrom.HasValue)
                    {
                        // Используем ToUniversalTime для надежного сравнения
                        var cutOffDate = visibleFrom.Value.ToUniversalTime();
                        messages = messages.Where(m => m.SentAt.ToUniversalTime() >= cutOffDate).ToList();
                    } 

                    if (messages.Count == 0 && unreadCount > 0 && contact != null)
                    {
                        contact.SetUnreadCount(0);
                        unreadCount = 0; // Сбрасываем локальную переменную тоже
                    }

                    messages.Reverse();

                    _hasMoreMessages = result.Pagination.HasMore;
                    if (messages.Count > 0) _oldestMessageDate = messages[0].SentAt;

                    int firstUnreadIndex = -1;
                    if (unreadCount > 0)
                    {
                        firstUnreadIndex = messages.Count - unreadCount;
                        if (firstUnreadIndex < 0) firstUnreadIndex = 0;
                    }

                    for (int i = 0; i < messages.Count; i++)
                    {
                        if (unreadCount > 5 && i == firstUnreadIndex)
                        {
                            ChatMessagesPanel.Children.Add(CreateUnreadSeparator());
                        }

                        var msg = messages[i];
                        msg.ChatRoomId = chatId;
                        DisplayMessage(msg);
                    }

                    Dispatcher.UIThread.RunJobs(DispatcherPriority.Render);

                    if (unreadCount > 0)
                    {
                        Control? targetScrollControl = null;

                        if (firstUnreadIndex < messages.Count)
                        {
                            string targetMsgId = messages[firstUnreadIndex].Id;
                            
                            Control? msgControl = null;
                            int msgControlIndex = -1;

                            for(int k=0; k<ChatMessagesPanel.Children.Count; k++)
                            {
                                var child = ChatMessagesPanel.Children[k] as Control;
                                if (child?.Tag is string tagId && tagId == targetMsgId)
                                {
                                    msgControl = child;
                                    msgControlIndex = k;
                                    break;
                                }
                            }

                            if (msgControl != null)
                            {
                                // By default, we want to scroll to the message
                                targetScrollControl = msgControl;

                                // Check if our line is DIRECTLY ABOVE it?
                                if (msgControlIndex > 0)
                                {
                                    var prevChild = ChatMessagesPanel.Children[msgControlIndex - 1] as Control;
                                    if (prevChild != null && prevChild.Tag is string tag && tag == "UnreadSeparator")
                                    {
                                        // If there is a line, scroll to the LINE. 
                                        // This will place it at the top of the screen.
                                        targetScrollControl = prevChild;
                                    }
                                }
                            }
                        }

                        // Perform a jump
                        if (targetScrollControl != null)
                        {
                            // We obtain the exact Y (already taking into account the line, if there is one)
                            double targetY = targetScrollControl.Bounds.Y;
                            
                            // Set the scroll. 
                            // If scrolling to a line, set it exactly (0). 
                            // If scrolling to a message (no line), indent slightly (-10).
                            double offsetAdjustment = (targetScrollControl.Tag as string == "UnreadSeparator") ? 0 : 10;
                            
                            ChatScrollViewer.Offset = new Vector(0, Math.Max(0, targetY - offsetAdjustment));
                        }
                    }
                    else
                    {
                        ChatScrollViewer.ScrollToEnd();
                    }

                    await Task.Delay(200); 

                    ChatMessagesPanel.Opacity = 1;
                    _isProgrammaticScroll = false;
                    
                    // Check visibility (this will only send a report about what is below the line and fits on the screen)
                    CheckVisibilityAndMarkAsRead();

                }, DispatcherPriority.Loaded);
            }
            catch (Exception ex)
            {
                Logger.Log($"[ERROR] LoadChatHistoryAsync failed: {ex}");
                _isProgrammaticScroll = false;
                ChatMessagesPanel.Opacity = 1;
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
                            isReply: hasReply,
                            text: msg.Content,
                            timestamp: timestamp,
                            type: isGuest,
                            replyContent: replyContent,
                            serverId: msg.Id,
                            isEdited: msg.EditedAt.HasValue,
                            replyToMessageId: msg.ReplyToMessageId,
                            username: msg.Sender.DisplayName ?? msg.Sender.Username,
                            replyToUsername: msg.ReplyTo?.SenderName, 
                            sentAt: msg.SentAt 
                        );

                        _messageCache[msg.Id] = chatMessage;

                        var grid = new Grid
                        {
                            ColumnDefinitions = { new ColumnDefinition(new GridLength(1, GridUnitType.Star)) },
                            Tag = msg.Id
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
                System.Diagnostics.Debug.WriteLine($"Error loading history: {ex.Message}");
            }
            finally
            {
                // A slight delay to prevent the UI from jerking
                await Task.Delay(200);
                _isLoadingHistory = false;
            }
        }

        public void OnChatScrollChanged(object? sender, ScrollChangedEventArgs e)
        {
            // If the flag is set, we ignore any scroll changes
            // This will prevent the read method from working while we are loading the chat
            if (_isProgrammaticScroll) return;
            
            if (sender is not ScrollViewer scrollViewer) return;
            var distanceFromBottom = ChatScrollViewer.Extent.Height - ChatScrollViewer.Viewport.Height - ChatScrollViewer.Offset.Y;

            if (distanceFromBottom > 250)
            {
                ScrollToBottomButton.IsVisible = true;
            }
            else
            {
                ScrollToBottomButton.IsVisible = false;
            }

            if (_isLoadingHistory || !_hasMoreMessages)
                return;

            if (scrollViewer == null)
                return;

            if (scrollViewer.Extent.Height <= scrollViewer.Viewport.Height)
                return;

            if (!_isLoadingHistory && _hasMoreMessages && scrollViewer.Offset.Y < 50)
            {
                _ = LoadMoreHistoryAsync();
            }

            if (e.ExtentDelta.Y != 0 || e.OffsetDelta.Y != 0) 
            {
                CheckVisibilityAndMarkAsRead();
            }
        }

        private void ScrollToBottomButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            ChatScrollViewer.ScrollToEnd();
        }

        private void HandleNewFriendChat(object data)
        {
            _ = Dispatcher.UIThread.InvokeAsync(async () =>
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
                        await _hubConnection.InvokeAsync("JoinChatGroup", notification.chatRoomId);

                        string initMsg = notification.Type switch
                        {
                            "DirectMessage" => "New Friend!",
                            _ => "New Group!"
                        };

                        bool isGroup = (notification.Type != "DirectMessage");

                        if (!_chatContacts.TryGetValue(notification.chatRoomId, out var contact))
                        {
                            contact = new MainWindow.Chat.Contact(
                                notification.friendDisplayName,
                                initMsg,
                                0,
                                this,
                                notification.chatRoomId
                            );

                            contact.ClearedHistoryAt = DateTime.UtcNow;

                            contact.IsGroupChat = isGroup;
                            contact.IsVisible = (Chat.GroupsActive == isGroup);
                            contact.UpdatePresence(true);

                            _chatContacts[notification.chatRoomId] = contact;
                            contactsStackPanel.Children.Insert(0, contact.Box);
                        }
                        else
                        {
                            contact.UpdateLastMessage(initMsg);
                            contact.IsGroupChat = isGroup;
                            contact.IsVisible = (Chat.GroupsActive == isGroup);

                            contact.ClearedHistoryAt = DateTime.UtcNow;

                            contactsStackPanel.Children.Remove(contact.Box);
                            contactsStackPanel.Children.Insert(0, contact.Box);
                        }

                        await LoadUserChatsAsync();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error handling New Friend Chat: {ex.Message}");
                }
            });
        }

        private void DisplayMessage(MessageDto message)
        {
            if (_currentChatId == null || message.ChatRoomId != _currentChatId.Value)
            {
                return;
            }

            if (_messageCache.ContainsKey(message.Id))
            {
                return;
            }

            var timestamp = message.SentAt.ToLocalTime().ToString("HH:mm");
            bool isGuest = (message.Sender.Username != _currentUsername);
            bool hasReply = message.ReplyTo != null;
            string? replyToName = message.ReplyTo?.SenderName;
            string? replyContent = message.ReplyTo?.Content;

            var chatMessage = new MainWindow.Chat.Message(
                isReply: hasReply,
                text: message.Content,
                timestamp: timestamp,
                type: isGuest,               
                replyContent: replyContent,
                serverId: message.Id,         
                isEdited: message.EditedAt.HasValue,
                replyToMessageId: message.ReplyToMessageId,
                username: message.Sender.DisplayName ?? message.Sender.Username,
                replyToUsername: replyToName,
                sentAt: message.SentAt       
            );

            _messageCache[message.Id] = chatMessage;

            var grid = new Grid
            {
                ColumnDefinitions = { new ColumnDefinition(new GridLength(1, GridUnitType.Star)) },
                Tag = message.Id
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
                UpdateChatListPosition(contact);
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

                var sentMessageDto = await _messageApiService.SendMessageAsync(_currentChatId.Value, dto);

                replyToMessageContent = "";
                replyToMessageId = "";
                isReplied = false;

                if (sentMessageDto != null)
                {
                    await Dispatcher.UIThread.InvokeAsync(() => 
                    {
                        // Метод DisplayMessage сам проверит _messageCache.ContainsKey
                        // Так что дублей не будет, даже если SignalR придет позже.
                        DisplayMessage(sentMessageDto);
                        
                        // Скроллим вниз, так как это мое сообщение
                        ChatScrollViewer.ScrollToEnd();
                        
                        // Обновляем сайдбар точными данными с сервера (время и контент)
                        if (_chatContacts.TryGetValue(_currentChatId.Value, out var c))
                        {
                            c.LastMessageAt = sentMessageDto.SentAt;
                            // Обновим еще раз, вдруг сервер как-то отформатировал текст
                            string preview = sentMessageDto.Content.Length > 30 ? sentMessageDto.Content.Substring(0, 30) + "..." : sentMessageDto.Content;
                            c.UpdateLastMessage(preview);
                            UpdateChatListPosition(c);
                        }
                    });
                }
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
            if (!_currentChatId.HasValue) return;

            if (_messageCache.TryGetValue(messageId, out var cachedMsg))
            {
                RemoveMessageFromUI(cachedMsg);
                _messageCache.Remove(messageId);
                
                CleanupReplyReferences(messageId);
            }

            // 2. Отправляем запрос на API
            bool success = await _messageApiService.DeleteMessageAsync(_currentChatId.Value, messageId);

            if (!success)
            {
                Console.WriteLine($"Failed to delete message {messageId} on server.");
                return;
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
                SetProfileOnlineState(_hubConnection != null && _hubConnection.State == HubConnectionState.Connected);
            });
        }

        private void SetProfileOnlineState(bool isOnline)
        {
            var activeBrush = isOnline ? Brush.Parse("#4da64d") : Brush.Parse("#c57179");

            if (_userStatusLabel != null)
            {
                _userStatusLabel.Text = isOnline ? "Online" : "Offline";
                _userStatusLabel.Foreground = activeBrush;
                userStatusDot.Background = activeBrush;

            }

            //if (_userStatusDot != null)
            //{
            //    _userStatusDot.Background = activeBrush;
            //    _userStatusDot.BorderBrush = activeBrush;
            //}
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

        public void SortChatsInUI()
        {
            Chat.ChatsList.Sort((a, b) =>
            {
                int pinComparison = b.IsPinned.CompareTo(a.IsPinned);
                if (pinComparison != 0) return pinComparison;

                if (a.IsPinned)
                {
                    return b.PinnedAt.CompareTo(a.PinnedAt);
                }

                return b.LastMessageAt.CompareTo(a.LastMessageAt);
            });

            contactsStackPanel.Children.Clear();
            foreach (var contact in Chat.ChatsList)
            {
                contact.IsVisible = contact.IsGroupChat ? Chat.GroupsActive : !Chat.GroupsActive;
                contactsStackPanel.Children.Add(contact.Box);
            }
        }

        private void UpdateChatListPosition(MainWindow.Chat.Contact contact)
        {
            if (contact.IsPinned)
            {
                return;
            }

            if (contactsStackPanel.Children.Contains(contact.Box))
            {
                contactsStackPanel.Children.Remove(contact.Box);
            }

            int pinnedCount = 0;
            foreach (var child in contactsStackPanel.Children)
            {
                // Check whether the element is a pinned contact
                if (child is Grid grid && grid.DataContext is MainWindow.Chat.Contact c && c.IsPinned)
                {
                    pinnedCount++;
                }
                // If we encounter the first unpinned one, we can stop counting, 
                // but it is safer to go through the ChatsList
            }

            // A more reliable way to calculate the index for insertion:
            // We count all pinned items that should currently be visible
            int insertIndex = Chat.ChatsList.Count(c => c.IsPinned && c.IsVisible);

            // Защита: индекс не может быть больше реального кол-ва детей
            if (insertIndex > contactsStackPanel.Children.Count)
                insertIndex = contactsStackPanel.Children.Count;

            contactsStackPanel.Children.Insert(insertIndex, contact.Box);
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
                lastChild.Children.Insert(0, editedLabel);
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

        private void RemoveMemberFromUiList(string username)
        {
            foreach (var control in groupInfoMembersStackPanel.Children.ToList())
            {
                if (control is DockPanel panel)
                {
                    var textBlock = panel.Children.OfType<TextBlock>().FirstOrDefault();

                    if (textBlock != null)
                    {
                        string displayedName = textBlock.Text?.Replace(" (Owner)", "") ?? "";

                        if (displayedName == username)
                        {
                            groupInfoMembersStackPanel.Children.Remove(panel);
                            UpdateMemberCountText();
                            return;
                        }
                    }
                }
            }
        }

        private void UpdateMemberCountText()
        {
            int count = groupInfoMembersStackPanel.Children.Count;
            string suffix = count == 1 ? "member" : "members";
            groupInfoNumberOfMembers.Text = $"{count} {suffix}";
        }

        private void groupInfoAddMemberButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            AddPersonToGroup.IsVisible = true;
        }

        private void groupInfoLeaveButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            LeaveGroupAndConfirm.IsVisible = true;
        }

        private async void ConfirmLeaveGroup_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            LeaveGroupAndConfirm.IsVisible = false;
            if (_currentChatId == null) return;

            int chatIdToLeave = _currentChatId.Value;

            try
            {
                bool success = await _chatApiService.LeaveChatAsync(chatIdToLeave);

                if (success)
                {
                    try
                    {
                        await _hubConnection.InvokeAsync("LeaveChatGroup", chatIdToLeave);
                    }
                    catch (Exception hubEx)
                    {
                        Logger.Error("Failed to leave SignalR group", hubEx);
                    }

                    groupInfoBox.IsVisible = false;
                    backgroundForGroupInfo.IsVisible = false;
                    RemoveChatFromUI(chatIdToLeave);
                    ClearChatArea();

                    friendTopBarName.Text = "";
                    friendTopBar.IsVisible = true;
                    PlaceHolder.IsVisible = true;
                    BottomContainer.IsVisible = false;

                    Logger.Log($"Successfully left chat {chatIdToLeave}");
                }
                else
                {
                    UpdateConnectionStatus("Failed to leave group", Brushes.Red);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Exception while leaving group", ex);
            }
        }

        private void CancelLeaveGroup_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            LeaveGroupAndConfirm.IsVisible = false;
        }

        private void CancelAddingGroup_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            AddPersonToGroup.IsVisible = false;
        }

        private void editTheGroupNameButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            PanelForGroupName.IsVisible = false;
            PanelForGroupNameEdit.IsVisible = true;

            editTheGroupNameTextBox.Text = groupInfoName.Text;
        }

        private async void acceptNewNameForGroup_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            string? newNameForGroup = editTheGroupNameTextBox.Text?.Trim();

            if (string.IsNullOrEmpty(newNameForGroup) || _currentChatId == null)
            {
                PanelForGroupNameEdit.IsVisible = false;
                PanelForGroupName.IsVisible = true;
                return;
            }

            PanelForGroupNameEdit.IsEnabled = false;

            try
            {
                var updateDto = new { Name = newNameForGroup };
                bool success = await _chatApiService.UpdateChatAsync(_currentChatId.Value, updateDto);

                if (success)
                {
                    groupInfoName.Text = newNameForGroup;
                    groupTopBarName.Text = newNameForGroup;

                    if (_chatContacts.TryGetValue(_currentChatId.Value, out var contact))
                    {
                        contact.UpdateName(newNameForGroup);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to rename group", ex);
            }
            finally
            {
                PanelForGroupNameEdit.IsEnabled = true;

                PanelForGroupNameEdit.IsVisible = false;
                PanelForGroupName.IsVisible = true;
            }
        }

        private async Task ReportReadProgressAsync(int chatId, DateTime messageTimestamp, bool isTotalRead)
        {
            if (!_currentChatId.HasValue || _currentChatId.Value != chatId) return;

            var normalized = messageTimestamp.ToUniversalTime();

            if (_lastReadProgressTimestamps.TryGetValue(chatId, out var lastReported) && normalized <= lastReported)
            {
                return;
            }

            _lastReadProgressTimestamps[chatId] = normalized;

            try
            {
                if (_hubConnection.State == HubConnectionState.Connected)
                {
                    await _hubConnection.InvokeAsync("ReportReadProgress", chatId, normalized);

                    if (_chatContacts.TryGetValue(chatId, out var contact))
                    {
                        DateTime minVisibleDateUtc = contact.ClearedHistoryAt?.ToUniversalTime() ?? DateTime.MinValue;

                        if (isTotalRead)
                        {
                            contact.SetUnreadCount(0);
                            RemoveUnreadSeparator();
                        }
                        else
                        {
                            int realUnreadLocal = 0;
                            
                            // Count the messages in the cache that are newer than the one we just read
                            foreach (var msg in _messageCache.Values)
                            {
                                var msgSentUtc = msg.SentAt.ToUniversalTime();

                                bool newerThanRead = msgSentUtc > normalized;
                                bool newerThanClear = msgSentUtc >= minVisibleDateUtc;
                                bool isNotMyMessage = msg.Sender != _currentUsername; 

                                if (newerThanRead && newerThanClear && isNotMyMessage)
                                {
                                    realUnreadLocal++;
                                }
                            }

                            // Update the counter to the actual number remaining (of those that have been loaded)
                            // This will give the effect of “decreasing” numbers when scrolling ( cool!! )
                            int displayCount = Math.Min(realUnreadLocal, contact.UnreadCount);
                            
                            contact.SetUnreadCount(displayCount);

                            if (displayCount == 0)
                            {
                                RemoveUnreadSeparator();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"[ReadProgress] Failed: {ex.Message}");
                _lastReadProgressTimestamps.Remove(chatId);
            }
        }

        private void CheckVisibilityAndMarkAsRead()
        {
            if (!_currentChatId.HasValue || ChatMessagesPanel.Children.Count == 0) return;

            var scrollViewer = ChatScrollViewer;
            var viewportTop = scrollViewer.Offset.Y;
            var viewportBottom = viewportTop + scrollViewer.Viewport.Height;

            DateTime? maxReadTimestamp = null;
            bool isLastMessageVisible = false; 

            // Let's start from the END (new messages) UP
            for (int i = ChatMessagesPanel.Children.Count - 1; i >= 0; i--)
            {
                var child = ChatMessagesPanel.Children[i] as Control;
                if (child == null || child.Bounds.Height <= 0) continue;

                var bounds = child.Bounds;

                bool isVisible = (bounds.Bottom > viewportTop + 5) && (bounds.Top < viewportBottom - 5);

                if (isVisible)
                {
                    if (child.Tag is string msgId && _messageCache.TryGetValue(msgId, out var cachedMsg))
                    {
                        maxReadTimestamp = cachedMsg.SentAt;
                        
                        if (i == ChatMessagesPanel.Children.Count - 1)
                        {
                            isLastMessageVisible = true;
                        }
                    }
                    
                    break; 
                }
            }

            if (maxReadTimestamp.HasValue)
            {
                _ = ReportReadProgressAsync(_currentChatId.Value, maxReadTimestamp.Value, isLastMessageVisible);
            }
        }

        private void RemoveUnreadSeparator()
        {
            var separator = ChatMessagesPanel.Children
                .FirstOrDefault(c => c is Control ctrl && ctrl.Tag as string == "UnreadSeparator");

            if (separator != null)
            {
                ChatMessagesPanel.Children.Remove(separator);
            }
        }
    }
}
