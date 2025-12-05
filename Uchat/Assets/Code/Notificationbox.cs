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


namespace Uchat
{
	public partial class MainWindow : Window
	{
		public partial class Chat
		{
			//static int notificationCount = 0;

			public class FriendRequest
			{
				private string username = string.Empty;
				private int contactId;
				private MainWindow mainWindow;

				private Grid friendRequestGrid = new Grid();
				private TextBlock usernameTextBlock = new TextBlock();
				private Button acceptRequestButton = new Button();
				private Button rejectRequestButton = new Button();

				public FriendRequest(string newUsername, int newContactId, MainWindow window)
				{
					username = newUsername;
					contactId = newContactId;
					mainWindow = window;

					// Настройка сетки
					friendRequestGrid.Classes.Add("friendRequestBox");
					friendRequestGrid.Height = 45; // Явно задаем высоту!
					friendRequestGrid.HorizontalAlignment = HorizontalAlignment.Stretch;
					friendRequestGrid.Background = Brush.Parse("#3b4252"); // Фон плашки
					friendRequestGrid.Margin = new Thickness(0, 0, 0, 5); // Отступ снизу

					friendRequestGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
					friendRequestGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(40)));
					friendRequestGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(40)));

					// Имя пользователя
					usernameTextBlock.Classes.Add("friendRequestUsername");
					usernameTextBlock.Text = username;
					usernameTextBlock.VerticalAlignment = VerticalAlignment.Center;
					usernameTextBlock.Margin = new Thickness(10, 0, 0, 0);
					usernameTextBlock.Foreground = Brushes.White; // Явно задаем белый цвет!
					usernameTextBlock.FontSize = 14;

					// Кнопка Принять (+)
					acceptRequestButton.Classes.Add("acceptRequestButton");
					acceptRequestButton.Content = "✔"; // Галочка выглядит лучше плюса
					acceptRequestButton.Background = Brush.Parse("#a3be8c"); // Зеленый
					acceptRequestButton.Foreground = Brushes.White;
					acceptRequestButton.Margin = new Thickness(2);
					acceptRequestButton.HorizontalContentAlignment = HorizontalAlignment.Center;
					acceptRequestButton.VerticalContentAlignment = VerticalAlignment.Center;
					acceptRequestButton.Click += AcceptRequestButton_Click;

					// Кнопка Отклонить (-)
					rejectRequestButton.Classes.Add("rejectRequestButton");
					rejectRequestButton.Content = "✖";
					rejectRequestButton.Background = Brush.Parse("#bf616a"); // Красный
					rejectRequestButton.Foreground = Brushes.White;
					rejectRequestButton.Margin = new Thickness(2);
					rejectRequestButton.HorizontalContentAlignment = HorizontalAlignment.Center;
					rejectRequestButton.VerticalContentAlignment = VerticalAlignment.Center;
					rejectRequestButton.Click += RejectRequestButton_Click;

					// Добавляем в Grid
					friendRequestGrid.Children.Add(usernameTextBlock);
					friendRequestGrid.Children.Add(acceptRequestButton);
					friendRequestGrid.Children.Add(rejectRequestButton);

					Grid.SetColumn(usernameTextBlock, 0);
					Grid.SetColumn(acceptRequestButton, 1);
					Grid.SetColumn(rejectRequestButton, 2);
				}

				public Grid Box { get { return friendRequestGrid; } }
                public string Username { get { return usernameTextBlock.Text; } set { usernameTextBlock.Text = value; } }

                private async void AcceptRequestButton_Click(object? sender, RoutedEventArgs e)
				{
					try
					{
						var success = await mainWindow._contactApiService.AcceptFriendRequestAsync(contactId);
						
						if (success)
						{
							if (friendRequestGrid.Parent is StackPanel parent)
								parent.Children.Remove(friendRequestGrid);
							
							await mainWindow.LoadUserChatsAsync();
						}
					}
					catch (Exception ex)
					{
						Logger.Error("Error accepting friend request", ex);
					}
				}

                private async void RejectRequestButton_Click(object? sender, RoutedEventArgs e)
                {
					try
					{
						await mainWindow._contactApiService.RejectFriendRequestAsync(contactId);
						
						// Remove this request from the list
						if (friendRequestGrid.Parent is StackPanel parent)
						{
							parent.Children.Remove(friendRequestGrid);
						}
					}
					catch (Exception ex)
					{
						Console.WriteLine($"Error rejecting friend request: {ex.Message}");
					}
                }

            }
		}
	}
}
