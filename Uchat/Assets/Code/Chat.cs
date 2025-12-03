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
		private Message? messageBeingEdited = null; // Сохраняем ссылку на Message для редактирования
		private bool isReplied = false;
		private string tempChatTextBox = "";
		public string replyToMessageContent = "";

		private void addFriend_Click(object? sender, RoutedEventArgs e)
		{
			var newContact = new Chat.Contact("New Chat", "Just an example", 0, contanctList);
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
				
				// Отправляем изменения на сервер
				if (!string.IsNullOrEmpty(messageBeingEdited.ServerId))
				{
					var connection = _connection;
					if (connection != null)
					{
						try
						{
							await connection.InvokeAsync("EditMessage", messageBeingEdited.ServerId, newContent);
							// UI обновится через обработчик MessageEdited
						}
						catch (Exception ex)
						{
							System.Diagnostics.Debug.WriteLine($"Failed to edit message: {ex.Message}");
						}
					}
				}
				else
				{
					// Фолбек для старых сообщений без serverId - только локальное обновление
					var editedText = new TextBlock
					{
						Text = "edited",
						Foreground = Brush.Parse("#C1E1C1"),
						FontSize = 10,
						Padding = new Thickness(0, 0, 3, 0),
						Margin = new Thickness(0, 3, 0, 0),
						FontStyle = FontStyle.Italic,
						HorizontalAlignment = HorizontalAlignment.Right,
					};

					textBlockChange.Text = newContent;

					if (textBlockChange.Parent is StackPanel textBlockAndTime)
					{
						int lastIndex = textBlockAndTime.Children.Count - 1;
						var time = textBlockAndTime.Children[lastIndex];

						if (time is StackPanel timeAndEdit)
						{
							if (timeAndEdit.Children.Count == 1)
							{
								int childrenIndex = timeAndEdit.Children.Count - 1;
								var saveTime = timeAndEdit.Children[childrenIndex];
								timeAndEdit.Children.Remove(timeAndEdit.Children[childrenIndex]);

								timeAndEdit.Children.Add(editedText);
								timeAndEdit.Children.Add(saveTime);
							}
						}
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