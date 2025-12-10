using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Input;
using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Linq;
using System.Threading.Tasks;
using static Uchat.MainWindow.Chat;
using Uchat.Shared;
using System.Collections.Generic;
using Uchat.Shared.DTOs;

namespace Uchat
{
	public partial class MainWindow : Window
	{
		private TextBlock? textBlockChange = new TextBlock();
		private Message? messageBeingEdited = null; // Сохраняем ссылку на Message для редактирования
		private bool isReplied = false;
		private string tempChatTextBox = "";
		public string replyToMessageContent = "";
		public string replyToMessageId = ""; // ID сообщения, на которое отвечают

		private async void addFriend_Click(object? sender, RoutedEventArgs e)
		{
			if (String.IsNullOrEmpty(searchTextBox.Text)) return;

			string input = searchTextBox.Text;

			if (Chat.GroupsActive)
			{
				// Create a new group
				var request = new Shared.DTOs.CreateChatRequestDto
				{
					Name = input,
					Type = "Public",
					Description = null
				};
				var newChat = await _chatApiService.CreateChatAsync(request);

				if (newChat != null)
				{
					// Add group to UI
					await LoadUserChatsAsync();

					// Open the new chat
					await OpenChatAsync(newChat.Id);
				}
			}
			else
			{
				// Send friend request
				var (success, errorMessage) = await _contactApiService.SendFriendRequestAsync(input);

				if (!success)
				{
                    // Show error message
                    //AddContactErrorText.Text = errorMessage ?? "Failed to send friend request";
                    //AddContactErrorText.IsVisible = true;
                    searchTextBox.Text = string.Empty;
                    return; // Don't close overlay
				}
				Chat.requestListener = input;
                //// Success - hide error and reload
                //AddContactErrorText.IsVisible = false;
            }

			// Clear textbox and hide overlay
			searchTextBox.Text = string.Empty;
		}

        private async Task LoadPendingFriendRequestsAsync()
        {
            try
            {
                // 1. Запускаем обе задачи параллельно (чтобы было быстрее)
                var friendRequestsTask = _contactApiService.GetPendingRequestsAsync();
                
                // Тебе нужно добавить этот метод в ChatApiService, который дергает новый эндпоинт из Шага 1
                var groupInvitesTask = _chatApiService.GetPendingInvitesAsync(); 

                await Task.WhenAll(friendRequestsTask, groupInvitesTask);

                var friendRequests = friendRequestsTask.Result ?? new List<ContactDto>();
                var groupInvites = groupInvitesTask.Result ?? new List<ChatRoomDto>();

                // 2. Обновляем UI
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    // Запоминаем текущее количество элементов, чтобы не мигать зря, если ничего не изменилось
                    // (опциональная оптимизация, пока оставим просто Clear)
                    requestList.Children.Clear();

                    bool hasAnyRequests = false;

                    // --- РИСУЕМ ЗАЯВКИ В ДРУЗЬЯ ---
                    if (friendRequests.Count > 0)
                    {
                        hasAnyRequests = true;
                        foreach (var request in friendRequests)
                        {
                            var friendRequestItem = new Chat.FriendRequest(
                                request.ContactUsername,
                                request.Id, // Здесь ID юзера
                                this,
                                "DirectMessage"
                            );
                            requestList.Children.Add(friendRequestItem.Box);
                        }
                    }

                    // --- РИСУЕМ ПРИГЛАШЕНИЯ В ГРУППЫ ---
                    if (groupInvites.Count > 0)
                    {
                        hasAnyRequests = true;
                        foreach (var invite in groupInvites)
                        {
                            // В DTO группы мы положили имя пригласившего в LastMessageContent (см. Шаг 1)
                            string inviterName = invite.LastMessageContent ?? "Someone";
                            
                            var groupRequestItem = new Chat.FriendRequest(
                                inviterName,    
                                invite.Id,      
                                this,            
                                "GroupInvite",    
                                invite.Name      
                            );
                            requestList.Children.Add(groupRequestItem.Box);
                        }
                    }

                    // --- ОБРАБОТКА ПУСТОГО СПИСКА ---
                    if (!hasAnyRequests)
                    {
                        var noRequestsText = new TextBlock
                        {
                            Text = "No pending requests",
                            Foreground = new SolidColorBrush(Color.FromRgb(136, 142, 152)),
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Margin = new Thickness(0, 20, 0, 0)
                        };
                        requestList.Children.Add(noRequestsText);
                        notificationButton.Background = Brushes.Transparent;
                    }
                    else
                    {
                        notificationButton.Background = Brush.Parse("#4da64d");
                    }
                });
            }
            catch (Exception ex)
            {
                // Не спамим в лог ошибками соединения каждую секунду, если сервер упал
                // Logger.Error("Error loading pending requests", ex); 
                System.Diagnostics.Debug.WriteLine($"Polling error: {ex.Message}");
            }
        }

		private async void CreateGroupButton_Click(object sender, RoutedEventArgs e)
		{
			string username = _currentUsername;
			string groupName;
			
			if (username.EndsWith("s", StringComparison.OrdinalIgnoreCase))
                groupName = $"{username}' Group";
            else
                groupName = $"{username}'s Group";

			try 
            {
                var request = new Shared.DTOs.CreateChatRequestDto
                {
                    Name = groupName,
                    Type = "Public", 
                    Description = $"Group created by {username}" 
                };

                var newChat = await _chatApiService.CreateChatAsync(request);

                if (newChat != null)
                {
                    if (!Chat.GroupsActive)
                    {
                        SwitchToGroups_Click(null, null);
                    }

                    await LoadUserChatsAsync();

                    await OpenChatAsync(newChat.Id);
                    
                    if (searchTextBox != null) 
                        searchTextBox.Text = string.Empty;
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to create group", ex);
            }
        }

        private async void InvitePersonToChat_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
		{
			string username = usernameSendInviteTextBox.Text?.Trim();
			
			if (string.IsNullOrEmpty(username)) return;
			if (_currentChatId == null) return;

			try 
			{
				// 1. Вызываем API для добавления пользователя
				// Предполагается метод AddMemberAsync(int chatId, string username) в _chatApiService
				var success = await _chatApiService.AddMemberAsync(_currentChatId.Value, username);

				if (!success)
				{
					invalidDataInAddingPersontoGroup.Text = "User not found or already in group";
					invalidDataInAddingPersontoGroup.IsVisible = true;
					return;
				}

				invalidDataInAddingPersontoGroup.Foreground = Brushes.Green; // (опционально поменять цвет на зеленый)
                invalidDataInAddingPersontoGroup.Text = "Invite sent successfully!"; 
                invalidDataInAddingPersontoGroup.IsVisible = true;

                // Даем пользователю прочитать сообщение пару секунд и закрываем (через Task.Delay, если хочешь)
                await Task.Delay(1500);

                // 3. Очищаем и закрываем
                usernameSendInviteTextBox.Text = "";
                invalidDataInAddingPersontoGroup.IsVisible = false;
                AddPersonToGroup.IsVisible = false; 
                
                // Вернем цвет ошибки обратно на красный для будущих ошибок
                invalidDataInAddingPersontoGroup.Foreground = Brush.Parse("#F44336");
            }
            catch (Exception ex)
            {
                invalidDataInAddingPersontoGroup.Text = "Server error";
                invalidDataInAddingPersontoGroup.IsVisible = true;
                Logger.Error("Error inviting user", ex);
            }
		}

		private void AddMemberToUiList(string username, bool isOwner = false)
        {
            var memberPanel = new DockPanel
            {
                Height = 40,
                LastChildFill = true,
                Margin = new Thickness(0, 0, 0, 5)
            };

            var img = new Image
            {
                Width = 25,
                Height = 25,
                Margin = new Thickness(10, 0, 10, 0),
                Source = new Bitmap(AssetLoader.Open(new Uri("avares://Uchat/Assets/Icons/avatar.png")))
            };
            DockPanel.SetDock(img, Dock.Left);

            var name = new TextBlock
            {
                Text = username,
                // Если владелец — золотой цвет, иначе белый
                Foreground = isOwner ? Brush.Parse("#FFD700") : Brushes.White,
                FontWeight = isOwner ? FontWeight.Bold : FontWeight.Normal,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                FontSize = 14
            };

            if (isOwner)
            {
                name.Text += " (Owner)";
            }

            memberPanel.Children.Add(img);
            memberPanel.Children.Add(name);

            groupInfoMembersStackPanel.Children.Add(memberPanel);
        }

		private async void groupTopBar_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            // Берем свойства клика относительно САМОГО ОКНА (this), а не контрола-отправителя.
            // Это надежнее, так как исключает проблемы с вложенностью элементов.
            var point = e.GetCurrentPoint(this);

            // Если нажата НЕ левая кнопка (например, правая) — выходим
            if (!point.Properties.IsLeftButtonPressed) return;

            // Если чат не выбран — выходим
            if (_currentChatId == null) return;

            // 1. Показываем панели (МГНОВЕННАЯ РЕАКЦИЯ)
            backgroundForGroupInfo.IsVisible = true;
            groupInfoBox.IsVisible = true;

            // 2. Ставим текущее имя из заголовка (чтобы не было пустоты)
            groupInfoName.Text = groupTopBarName.Text;
            
            // 3. Блокируем дальнейшую обработку клика (чтобы не проваливалось под окно)
            e.Handled = true;

            // 4. Запускаем загрузку данных с сервера
            // (try/catch внутри LoadGroupInfoAsync сам обработает ошибки)
            await LoadGroupInfoAsync(_currentChatId.Value);
        }

        // 2. ЗАКРЫТИЕ ПАНЕЛИ (Крестик)
        private void ExitInfoAboutGroup_Click(object? sender, RoutedEventArgs e)
        {
            // Скрываем всё
            groupInfoBox.IsVisible = false;
            backgroundForGroupInfo.IsVisible = false;

            // Сбрасываем панели редактирования имени в исходное состояние
            if (PanelForGroupNameEdit != null) PanelForGroupNameEdit.IsVisible = false;
            if (PanelForGroupName != null) PanelForGroupName.IsVisible = true;
        }

        // 3. ЗАГРУЗКА ДАННЫХ С СЕРВЕРА
        private async Task LoadGroupInfoAsync(int chatId)
        {
            // Очищаем старый список
            groupInfoMembersStackPanel.Children.Clear();
            groupInfoNumberOfMembers.Text = "Loading...";

            try
            {
                // Запрос к API (метод должен быть в ChatApiService)
                var details = await _chatApiService.GetChatDetailsAsync(chatId);

                if (details != null)
                {
                    // Обновляем имя (вдруг изменилось)
                    groupInfoName.Text = details.Name;
                    
                    // Обновляем счетчик
                    string suffix = details.Members.Count == 1 ? "member" : "members";
                    groupInfoNumberOfMembers.Text = $"{details.Members.Count} {suffix}";

                    // Заполняем список участников
                    foreach (var member in details.Members)
                    {
                        bool isOwner = (member.UserId == details.CreatorId);
                        AddMemberToUiList(member.Username, isOwner);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to load group info", ex);
                groupInfoNumberOfMembers.Text = "Info unavailable";
            }
        }

		private void SearchTextBox_TextChanged(object? sender, TextChangedEventArgs e)
        {
            var query = searchTextBox.Text?.Trim().ToLower() ?? string.Empty;

            foreach (var kvp in _chatContacts)
            {
                var contact = kvp.Value;
                
                // А. Совпадает ли имя? (или поиск пустой)
                bool nameMatches = string.IsNullOrEmpty(query) || contact.ChatName.ToLower().Contains(query);
                
                // Б. Подходит ли тип чата для текущей вкладки?
                bool typeMatches = (Chat.GroupsActive && contact.IsGroupChat) || (!Chat.GroupsActive && !contact.IsGroupChat);

                // Показываем элемент, только если совпало И имя, И тип вкладки
                contact.Box.IsVisible = nameMatches && typeMatches;
            }
        }

		private async Task ExecuteSearchActionAsync()
        {
            string query = searchTextBox.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(query)) return;

            // А. Локальный поиск (ищем в уже загруженных чатах)
            var existingChat = _chatContacts.Values.FirstOrDefault(c => 
                c.ChatName.Equals(query, StringComparison.OrdinalIgnoreCase));

            if (existingChat != null)
            {
                // Если чат на другой вкладке — переключаем её
                if (existingChat.IsGroupChat != Chat.GroupsActive)
                {
                    if (existingChat.IsGroupChat) SwitchToGroups_Click(null, null);
                    else SwitchToContacts_Click(null, null);
                }
                
                // Сбрасываем фильтр и открываем чат
                existingChat.Box.IsVisible = true; 
                searchTextBox.Text = ""; 
                await OpenChatAsync(existingChat.ChatId);
                return;
            }

            // Б. Если локально не нашли
            if (Chat.GroupsActive)
            {
                // Вкладка ГРУППЫ -> Ищем публичную группу на сервере
                try
                {
                    UpdateConnectionStatus("Searching public group...", Brushes.Orange);
                    var joinedChat = await _chatApiService.JoinPublicGroupByNameAsync(query);

                    if (joinedChat != null)
                    {
                        await LoadUserChatsAsync();
                        await OpenChatAsync(joinedChat.Id);
                        searchTextBox.Text = "";
                        UpdateConnectionStatus("Joined group!", Brushes.Green);
                    }
                    else
                    {
                        UpdateConnectionStatus("Group not found", Brushes.Red);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("Error searching public group", ex);
                    UpdateConnectionStatus("Error searching", Brushes.Red);
                }
            }
            else
            {
                // Вкладка КОНТАКТЫ -> Отправляем заявку в друзья
                addFriend_Click(null, null);
            }
        }

        // 2. Обработчик ENTER в поле ввода
        private async void SearchTextBox_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                await ExecuteSearchActionAsync();
            }
        }

        // 3. Обработчик КЛИКА ПО ЛУПЕ (Button Click)
        private async void SearchButton_Click(object? sender, RoutedEventArgs e)
        {
            await ExecuteSearchActionAsync();
        }

        // 4. ПЕРЕКЛЮЧЕНИЕ ВКЛАДОК (С обновлением фильтра!)
        private void SwitchToGroups_Click(object? sender, RoutedEventArgs e)
        {
            Chat.GroupsActive = true;
            searchTextBox.Watermark = "Find group";
            
            // Визуальное оформление кнопок
            GroupsButton.Background = Brush.Parse("#5da3a5");
            ContactsButton.Background = Brush.Parse("#3e4042");
            ContactsButton.FontWeight = FontWeight.Normal;
            GroupsButton.FontWeight = FontWeight.SemiBold;

            Chat.ShowGroups(true);
            
            // ВАЖНО: Пересчитываем фильтр поиска для новой вкладки
            SearchTextBox_TextChanged(null, null); 
        }

        private void SwitchToContacts_Click(object? sender, RoutedEventArgs e)
        {
            Chat.GroupsActive = false;
            searchTextBox.Watermark = "Find friend";
            
            // Визуальное оформление кнопок
            ContactsButton.Background = Brush.Parse("#5e81ac");
            GroupsButton.Background = Brush.Parse("#3e4042");
            ContactsButton.FontWeight = FontWeight.SemiBold;
            GroupsButton.FontWeight = FontWeight.Normal;

            Chat.ShowGroups(false);
            
            // ВАЖНО: Пересчитываем фильтр поиска для новой вкладки
            SearchTextBox_TextChanged(null, null);
        }

        private async void SendButton_Click(object? sender, RoutedEventArgs e)
        {
            if (!sendButton.IsEnabled) return; 
            sendButton.IsEnabled = false;

            try
            {
                string text;

                if (isReplied)
                {
                    text = chatTextBoxForReply.Text?.Trim() ?? "";
                }
                else
                {
                    text = chatTextBox.Text?.Trim() ?? "";
                }

                if (string.IsNullOrEmpty(text)) 
                { 
                    return; 
                }

                replyTheMessageBox.IsVisible = false;

                // Отправляем сообщение на сервер через SignalR
                // Пока мы тут ждем (await), кнопка выключена, нажать второй раз нельзя
                await SendMessageToServerAsync(text);
                
                // Очищаем поля ТОЛЬКО после успешной отправки
                chatTextBox.Text = "";
                chatTextBoxForReply.Text = "";
                chatTextBox.IsVisible = true;
                chatTextBoxForReply.IsVisible = false;
                isReplied = false;
                replyToMessageId = "";
            }
            catch (Exception)
            {
                // Если не удалось отправить, оставляем текст в поле
                // Пользователь сможет нажать кнопку еще раз, так как мы разблокируем её в finally
            }
            finally
            {
                sendButton.IsEnabled = true;
                
                // Полезно вернуть фокус в поле ввода, чтобы можно было сразу писать дальше
                if (chatTextBox.IsVisible)
                {
                    chatTextBox.Focus();
                }
            }
        }
		private void DontReplyTheMessage_Click(object? sender, RoutedEventArgs e)
		{
			replyTheMessageBox.IsVisible = false;
			chatTextBoxForReply.IsVisible = false;
			chatTextBox.Text = chatTextBoxForReply.Text;
			chatTextBox.IsVisible = true;
			isReplied = false;
			replyToMessageId = "";
			chatTextBoxForReply.Text = "";
		}


		private void DontEditTheMessage_Click(object? sender, RoutedEventArgs e)
		{
			CloseEditMode();
		}

		private async void EditMessageButton_Click(object? sender, RoutedEventArgs e)
		{
			if ((textBlockChange?.Text == chatTextBoxForEdit.Text)
				|| string.IsNullOrEmpty(chatTextBoxForEdit.Text))
			{
				CloseEditMode();
				textBlockChange = null;
				messageBeingEdited = null;
				return;
			}

			if (textBlockChange != null && messageBeingEdited != null)
			{
				var newContent = chatTextBoxForEdit.Text;
				
				// Отправляем изменения через REST API
				if (!string.IsNullOrEmpty(messageBeingEdited.ServerId))
				{
					try
					{
						await EditMessageAsync(messageBeingEdited.ServerId, newContent);
						// UI обновится через SignalR обработчик MessageEdited
					}
					catch
					{
						// Failed to edit message
					}
				}

				CloseEditMode();
				textBlockChange = null;
				messageBeingEdited = null;
			}
		}

		private void CloseEditMode()
		{
			editTheMessageBox.IsVisible = false;
			chatTextBoxForEdit.IsVisible = false;
			editTheMessageButton.IsVisible = false;
			chatTextBox.Text = tempChatTextBox;
			chatTextBox.IsVisible = true;
		}

		private void answerTheMessage_ActualThemeVariantChanged(object? sender, System.EventArgs e)
		{
		}

		private void answerTheMessage_ActualThemeVariantChanged_1(object? sender, System.EventArgs e)
		{
		}

		private void EditMessageButton_ActualThemeVariantChanged(object? sender, System.EventArgs e)
		{
		}

		private void editTheMessageBox_ActualThemeVariantChanged(object? sender, System.EventArgs e)
		{
		}

		private void TextBlock_ActualThemeVariantChanged(object? sender, EventArgs e)
		{
		}
	}
}