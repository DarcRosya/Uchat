using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System;
using static Uchat.MainWindow.Chat;

namespace Uchat
{
	public partial class MainWindow : Window
	{
		private TextBlock? textBlockChange = new TextBlock();
		private bool isReplied = false;
		private string tempChatTextBox = "";

		private void addFriend_Click(object? sender, RoutedEventArgs e)
		{
			var contactGrid = new Grid
			{
				Background = Brush.Parse("#171a20"),
				ColumnDefinitions =
		{
			new ColumnDefinition(new GridLength(50, GridUnitType.Pixel)),
			new ColumnDefinition(new GridLength(1, GridUnitType.Star))
		}
			};

			var imageURL = new Uri("avares://Uchat/Assets/Icons/avatar.png");
			var avatarIcon = new Avalonia.Controls.Image
			{
				Source = new Bitmap(AssetLoader.Open(imageURL)),
				Stretch = Stretch.UniformToFill
			};

			var contactPanel = new StackPanel
			{
				Height = 50
			};

			var contactName = new TextBlock
			{
				Text = "John Cena",
				Foreground = Brush.Parse("#ffffff"),
				FontSize = 15,
				Margin = new Thickness(5, 15, 0, 0),
				HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
			};

			var lastMessage = new TextBlock
			{
				Text = "Do you see me?",
				Foreground = Brush.Parse("#999999"),
				FontSize = 10,
				Margin = new Thickness(5, 0, 0, 0),
				HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
				VerticalAlignment = Avalonia.Layout.VerticalAlignment.Bottom
			};

			contactPanel.Children.Add(contactName);
			contactPanel.Children.Add(lastMessage);

			contactGrid.Children.Add(avatarIcon);

			contactGrid.Children.Add(contactPanel);

			Grid.SetColumn(avatarIcon, 0);
			Grid.SetColumn(contactPanel, 1);

			contanctList.Children.Add(contactGrid);
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

		private void EditMessageButton_Click(object? sender, RoutedEventArgs e)
		{
			if ((textBlockChange.Text == chatTextBoxForEdit.Text)
				|| string.IsNullOrEmpty(chatTextBoxForEdit.Text))
			{
				CloseEditMode();
				textBlockChange = null;
				return;
			}

			if (textBlockChange != null)
			{
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


				textBlockChange.Text = chatTextBoxForEdit.Text;

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

				CloseEditMode();
				textBlockChange = null;
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