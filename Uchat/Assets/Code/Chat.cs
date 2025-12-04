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

namespace Uchat
{
	public partial class MainWindow : Window
	{
		private TextBlock? textBlockChange = new TextBlock();
		private Message? messageBeingEdited = null; 
		private bool isReplied = false;
		private string tempChatTextBox = "";
		public string replyToMessageContent = "";
		public string replyToMessageId = ""; 

        private void addFriend_Click(object? sender, RoutedEventArgs e)
		{
            AddContactOverlay.IsVisible = true;
		}

        private void CancelButton_Click(object? sender, RoutedEventArgs e)
		{
            AddContactTextBox.Text = "";
            AddContactOverlay.IsVisible = false;
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
			//заменить в будущем
			if (!String.IsNullOrEmpty(AddContactTextBox.Text))
			{
                var newContact = new Chat.Contact(AddContactTextBox.Text, "", 0, contactList);
                AddContactOverlay.IsVisible = false;
                AddContactTextBox.Text = "";
            }
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

			try
			{
				await SendMessageToServerAsync(text);
			
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
	}		private void DontReplyTheMessage_Click(object? sender, RoutedEventArgs e)
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
					catch (Exception ex)
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