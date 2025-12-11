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
using System.Linq;
using Uchat.Shared;


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
				private string requestType;
				private string groupName;
				private MainWindow mainWindow;

				private Grid friendRequestGrid = new Grid();
				private TextBlock infoTextBlock = new TextBlock();
				private Button acceptRequestButton = new Button();
				private Button rejectRequestButton = new Button();

				public FriendRequest(string username, int id, MainWindow window, string type = "DirectMessage", string newGroupName = "")
				{
					this.username = username;
					contactId = id;
					requestType = type;
					groupName = newGroupName;
					mainWindow = window;

					friendRequestGrid.Classes.Add("friendRequestBox");
					friendRequestGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
					friendRequestGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(55)));
					friendRequestGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(55)));

					infoTextBlock.Classes.Add("friendRequestUsername");
					
					if (requestType == "Group")
					{
						infoTextBlock.Text = $"Group: {groupName}";
						ToolTip.SetTip(infoTextBlock, $"Invited by: {username}");
					}
					else
					{
						infoTextBlock.Text = username;
					}

					acceptRequestButton.Classes.Add("acceptRequestButton");
					var iconCheck = new Uri("avares://Uchat/Assets/Icons/check.png");
					acceptRequestButton.Content = new Image
					{
						Source = new Bitmap(AssetLoader.Open(iconCheck)),
						Width = 16, Height = 14
					};
					acceptRequestButton.Click += AcceptRequestButton_Click;
					rejectRequestButton.Classes.Add("rejectRequestButton");
					var iconCross = new Uri("avares://Uchat/Assets/Icons/cross.png");
					rejectRequestButton.Content = new Image
					{
						Source = new Bitmap(AssetLoader.Open(iconCross)),
						Width = 12, Height = 12
					};
					rejectRequestButton.Click += RejectRequestButton_Click;

					// Добавляем элементы в Grid
					friendRequestGrid.Children.Add(infoTextBlock);
					friendRequestGrid.Children.Add(acceptRequestButton);
					friendRequestGrid.Children.Add(rejectRequestButton);

					Grid.SetColumn(infoTextBlock, 0);
					Grid.SetColumn(acceptRequestButton, 1);
					Grid.SetColumn(rejectRequestButton, 2);
				}

				public Grid Box { get { return friendRequestGrid; } }
    			public string Username { get { return infoTextBlock.Text; } }

                private async void AcceptRequestButton_Click(object? sender, RoutedEventArgs e)
				{
					acceptRequestButton.IsEnabled = false; 
					try
					{
						bool success = false;

						if (requestType == "GroupInvite" || requestType == "Group")
						{
							success = await mainWindow._chatApiService.AcceptGroupInviteAsync(contactId);
						}
						else
						{
							success = await mainWindow._contactApiService.AcceptFriendRequestAsync(contactId);
						}
						
						if (success)
						{
							mainWindow.requestList.Children.Remove(friendRequestGrid);
							
							await mainWindow.LoadUserChatsAsync();

							if (requestType == "GroupInvite" || requestType == "Group")
							{
								if (!Chat.GroupsActive)
								{
									mainWindow.SwitchToGroups_Click(null, null);
								}
							}
							else
							{
								if (Chat.GroupsActive)
								{
									mainWindow.SwitchToContacts_Click(null, null);
								}

								var newChat = mainWindow._chatContacts.Values
									.FirstOrDefault(c => c.ChatName == username);
							}
						}
						else
						{
							Logger.Error($"Server failed to accept request ({requestType})");
							acceptRequestButton.IsEnabled = true;
						}
					}
					catch (Exception ex)
					{
						Logger.Error("Error accepting request", ex);
						acceptRequestButton.IsEnabled = true;
					}
				}

				private async void RejectRequestButton_Click(object? sender, RoutedEventArgs e)
				{
					rejectRequestButton.IsEnabled = false;
					try
					{
						bool success = false;

						if (requestType == "GroupInvite" || requestType == "Group")
						{
							// Logic for GROUPS
							success = await mainWindow._chatApiService.RejectGroupInviteAsync(contactId);
						}
						else
						{
							// Логика для ДРУЗЕЙ
							success = await mainWindow._contactApiService.RejectFriendRequestAsync(contactId);
						}
						
						if (success)
						{
							mainWindow.requestList.Children.Remove(friendRequestGrid);
						}
						else
						{
							Logger.Error($"Server failed to reject request ({requestType})");
							rejectRequestButton.IsEnabled = true;
						}
					}
					catch (Exception ex)
					{
						Logger.Error($"Error rejecting request: {ex.Message}");
						rejectRequestButton.IsEnabled = true;
					}
				}
			}
		}
	}
}
