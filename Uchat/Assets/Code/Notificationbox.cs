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

					friendRequestGrid.Classes.Add("friendRequestBox");
					friendRequestGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
					friendRequestGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(55)));
					friendRequestGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(55)));

					usernameTextBlock.Classes.Add("friendRequestUsername");
					usernameTextBlock.Text = username;

					acceptRequestButton.Classes.Add("acceptRequestButton");
                    var icon = new Uri("avares://Uchat/Assets/Icons/check.png");
					acceptRequestButton.Content = new Image
					{
                        Source = new Bitmap(AssetLoader.Open(icon)),
                        Width = 16,
                        Height = 14,
                    };
                    acceptRequestButton.Click += AcceptRequestButton_Click;

					rejectRequestButton.Classes.Add("rejectRequestButton");
					icon = new Uri("avares://Uchat/Assets/Icons/cross.png");
					rejectRequestButton.Content = new Image
					{
						Source = new Bitmap(AssetLoader.Open(icon)),
						Width = 12,
						Height = 12,
					};
					rejectRequestButton.Click += RejectRequestButton_Click;

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
                            mainWindow.requestList.Children.Remove(friendRequestGrid);
							
							await mainWindow.LoadUserChatsAsync();
						}
						else
						{
							Logger.Error("Server failed to accept request");
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
						bool success = await mainWindow._contactApiService.RejectFriendRequestAsync(contactId);
						
						if (success)
						{
							mainWindow.requestList.Children.Remove(friendRequestGrid);
						}
						else
						{
							Logger.Error("Server failed to reject request");
						}
					}
					catch (Exception ex)
					{
						Logger.Error($"Error rejecting friend request: {ex.Message}");
					}
                }

            }
		}
	}
}
