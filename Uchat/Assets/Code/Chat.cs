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
		private Message? messageBeingEdited = null; // Save the link to Message for editing
		private bool isReplied = false;
		private string tempChatTextBox = "";
		public string replyToMessageContent = "";
		public string replyToMessageId = "";

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
					Type = "Public"
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
                var friendRequestsTask = _contactApiService.GetPendingRequestsAsync();
                
                var groupInvitesTask = _chatApiService.GetPendingInvitesAsync(); 

                await Task.WhenAll(friendRequestsTask, groupInvitesTask);

                var friendRequests = friendRequestsTask.Result ?? new List<ContactDto>();
                var groupInvites = groupInvitesTask.Result ?? new List<ChatRoomDto>();

                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    requestList.Children.Clear();

                    bool hasAnyRequests = false;

                    if (friendRequests.Count > 0)
                    {
                        hasAnyRequests = true;
                        foreach (var request in friendRequests)
                        {
                            var friendRequestItem = new Chat.FriendRequest(
                                request.ContactUsername,
                                request.Id, 
                                this,
                                "DirectMessage"
                            );
                            requestList.Children.Add(friendRequestItem.Box);
                        }
                    }

                    if (groupInvites.Count > 0)
                    {
                        hasAnyRequests = true;
                        foreach (var invite in groupInvites)
                        {
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
                        BellIcon.Source = new Bitmap(AssetLoader.Open(new Uri("avares://Uchat/Assets/Icons/bell.png")));
                    }
                    else
                    {
                        BellIcon.Source = new Bitmap(AssetLoader.Open(new Uri("avares://Uchat/Assets/Icons/activated_bell.png")));
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Polling error: {ex.Message}");
            }
        }

		private async void CreateGroupButton_Click(object sender, RoutedEventArgs e)
		{
			string username = _currentUsername;
			string baseName;
			
			if (username.EndsWith("s", StringComparison.OrdinalIgnoreCase))
                baseName = $"{username}' Group";
            else
                baseName = $"{username}'s Group";

            var takenNumbers = new HashSet<int>();

            var existingGroupNames = _chatContacts.Values
                .Where(c => c.IsGroupChat)
                .Select(c => c.ChatName)
                .ToList();

            foreach (var name in existingGroupNames)
            {
                if (name.Equals(baseName, StringComparison.OrdinalIgnoreCase))
                {
                    takenNumbers.Add(0);
                    continue;
                }

                string prefixWithSpace = baseName + " ";
                if (name.StartsWith(prefixWithSpace, StringComparison.OrdinalIgnoreCase))
                {
                    string numberPart = name.Substring(prefixWithSpace.Length);

                    if (int.TryParse(numberPart, out int number))
                    {
                        takenNumbers.Add(number);
                    }
                }
            }

            int newSuffix = 0;
            while (takenNumbers.Contains(newSuffix))
            {
                newSuffix++;
            }

            string finalName;
            if (newSuffix == 0)
            {
                finalName = baseName; 
            }
            else
            {
                finalName = $"{baseName} {newSuffix}";
            }

			try 
            {
                var request = new Shared.DTOs.CreateChatRequestDto
                {
                    Name = finalName,
                    Type = "Public"
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

                    if (_chatContacts.TryGetValue(newChat.Id, out var createdContact))
                    {
                        var oldSelected = this._chatContacts.Values.FirstOrDefault(c => c.IsSelected); 
                        if (oldSelected != null)
                        {
                            oldSelected.SetSelected(false);
                        }

                        createdContact.SetSelected(true);

                        this.groupTopBarName.Text = createdContact.ChatName;
                        this.friendTopBarName.Text = createdContact.ChatName;
                        this.groupTopBar.IsVisible = createdContact.IsGroupChat;
                        this.friendTopBar.IsVisible = !createdContact.IsGroupChat;
                    }

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
				var success = await _chatApiService.AddMemberAsync(_currentChatId.Value, username);

				if (!success)
				{
					invalidDataInAddingPersontoGroup.Text = "User not found or already in group";
					invalidDataInAddingPersontoGroup.IsVisible = true;
					return;
				}

				invalidDataInAddingPersontoGroup.Foreground = Brushes.Green; 
                invalidDataInAddingPersontoGroup.Text = "Invite sent successfully!"; 
                invalidDataInAddingPersontoGroup.IsVisible = true;

                await Task.Delay(1500);

                usernameSendInviteTextBox.Text = "";
                invalidDataInAddingPersontoGroup.IsVisible = false;
                AddPersonToGroup.IsVisible = false; 
                
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
            var point = e.GetCurrentPoint(this);

            if (!point.Properties.IsLeftButtonPressed) return;

            if (_currentChatId == null) return;

            backgroundForGroupInfo.IsVisible = true;
            groupInfoBox.IsVisible = true;

            groupInfoName.Text = groupTopBarName.Text;
            
            e.Handled = true;

            await LoadGroupInfoAsync(_currentChatId.Value);
        }

        private void ExitInfoAboutGroup_Click(object? sender, RoutedEventArgs e)
        {
            groupInfoBox.IsVisible = false;
            backgroundForGroupInfo.IsVisible = false;

            if (PanelForGroupNameEdit != null) PanelForGroupNameEdit.IsVisible = false;
            if (PanelForGroupName != null) PanelForGroupName.IsVisible = true;
        }

        private async Task LoadGroupInfoAsync(int chatId)
        {
            groupInfoMembersStackPanel.Children.Clear();
            groupInfoNumberOfMembers.Text = "Loading...";

            try
            {
                var details = await _chatApiService.GetChatDetailsAsync(chatId);

                if (details != null)
                {
                    groupInfoName.Text = details.Name;
                    if (_currentChatId == chatId)
                    {
                        groupTopBarName.Text = details.Name;
                    }

                    if (_chatContacts.TryGetValue(chatId, out var contactItem))
                    {
                        contactItem.UpdateName(details.Name); 
                    }
                    
                    string suffix = details.Members.Count == 1 ? "member" : "members";
                    groupInfoNumberOfMembers.Text = $"{details.Members.Count} {suffix}";

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

        private void SearchTextBox_TextChanged(object? sender, TextChangedEventArgs? e)
        {
            var query = searchTextBox.Text?.Trim().ToLower() ?? string.Empty;

            foreach (var kvp in _chatContacts)
            {
                var contact = kvp.Value;
                
                bool nameMatches = string.IsNullOrEmpty(query) || contact.ChatName.ToLower().Contains(query);
                
                bool typeMatches = (Chat.GroupsActive && contact.IsGroupChat) || (!Chat.GroupsActive && !contact.IsGroupChat);

                contact.Box.IsVisible = nameMatches && typeMatches;
            }
        }

		private async Task ExecuteSearchActionAsync()
        {
            string query = searchTextBox.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(query)) return;

            var existingChat = _chatContacts.Values.FirstOrDefault(c => 
                c.ChatName.Equals(query, StringComparison.OrdinalIgnoreCase));

            if (existingChat != null)
            {
                if (existingChat.IsGroupChat != Chat.GroupsActive)
                {
                    if (existingChat.IsGroupChat) SwitchToGroups_Click(null, null);
                    else SwitchToContacts_Click(null, null);
                }
                
                existingChat.Box.IsVisible = true; 
                searchTextBox.Text = ""; 
                await OpenChatAsync(existingChat.ChatId);
                return;
            }

            if (Chat.GroupsActive)
            {
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
                addFriend_Click(null, null);
            }
        }

        private async void SearchTextBox_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                await ExecuteSearchActionAsync();
            }
        }

        private async void SearchButton_Click(object? sender, RoutedEventArgs e)
        {
            await ExecuteSearchActionAsync();
        }

        private void SwitchToGroups_Click(object? sender, RoutedEventArgs e)
        {
            Chat.GroupsActive = true;
            searchTextBox.Watermark = "Find group";
            
            GroupsButton.Background = Brush.Parse("#5da3a5");
            ContactsButton.Background = Brush.Parse("#3e4042");
            ContactsButton.FontWeight = FontWeight.Normal;
            GroupsButton.FontWeight = FontWeight.SemiBold;

            Chat.ShowGroups(true);
            
            SearchTextBox_TextChanged(null, null); 
        }

        private void SwitchToContacts_Click(object? sender, RoutedEventArgs e)
        {
            Chat.GroupsActive = false;
            searchTextBox.Watermark = "Find friend";
            
            ContactsButton.Background = Brush.Parse("#5e81ac");
            GroupsButton.Background = Brush.Parse("#3e4042");
            ContactsButton.FontWeight = FontWeight.SemiBold;
            GroupsButton.FontWeight = FontWeight.Normal;

            Chat.ShowGroups(false);

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

                await SendMessageToServerAsync(text);
                
                chatTextBox.Text = "";
                chatTextBoxForReply.Text = "";
                chatTextBox.IsVisible = true;
                chatTextBoxForReply.IsVisible = false;
                isReplied = false;
                replyToMessageId = "";
                ScrollToBottomButton.Margin = new Thickness(0, 0, 12, 20);
            }
            catch (Exception)
            {
                // If sending failed, leave the text in the field.
                // The user will be able to click the button again, as we will unlock it in finally.
            }
            finally
            {
                sendButton.IsEnabled = true;
                
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
            ScrollToBottomButton.Margin = new Thickness(0, 0, 12, 20);
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
				
				if (!string.IsNullOrEmpty(messageBeingEdited.ServerId))
				{
					try
					{
						await EditMessageAsync(messageBeingEdited.ServerId, newContent);
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
            ScrollToBottomButton.Margin = new Thickness(0, 0, 12, 20);
        }

        private void UpdateChatHeaders(MainWindow.Chat.Contact contact)
        {
            if (contact.IsGroupChat)
            {
                groupTopBarName.Text = contact.ChatName;
                groupTopBar.IsVisible = true;
                friendTopBar.IsVisible = false;
            }
            else
            {
                friendTopBarName.Text = contact.ChatName;
                friendTopBar.IsVisible = true;
                groupTopBar.IsVisible = false;
            }
        }
	}
}