using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Threading.Tasks;
using static Uchat.MainWindow.Chat;
using Uchat.Shared;

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
					Type = "group",
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
                var requests = await _contactApiService.GetPendingRequestsAsync();
                
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    requestList.Children.Clear();
                    
                    if (requests.Count == 0)
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
                        foreach (var request in requests)
                        {
                            var friendRequest = new Chat.FriendRequest(
                                request.ContactUsername,
                                request.Id,
                                this
                            );
                            requestList.Children.Add(friendRequest.Box);
                        }
                        notificationButton.Background = Brush.Parse("#4da64d");
                    }
                });
            }
            catch (Exception ex)
            {
                Logger.Error("Error loading pending friend requests", ex);
            }
        }

		//создание групи туууууут
		private async void CreateGroupButton_Click(object sender, RoutedEventArgs e)
		{
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
        }
        private async void SendButton_Click(object? sender, RoutedEventArgs e)
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

			if (string.IsNullOrEmpty(text)) { return; }
			replyTheMessageBox.IsVisible = false;

			// Отправляем сообщение на сервер через SignalR
			try
			{
				await SendMessageToServerAsync(text);
			
				// Очищаем поля после успешной отправки
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