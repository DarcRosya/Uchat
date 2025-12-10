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

					// Настройка Grid
					friendRequestGrid.Classes.Add("friendRequestBox");
					friendRequestGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
					friendRequestGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(55)));
					friendRequestGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(55)));

					// Настройка текста (показываем разный текст для групп и друзей)
					infoTextBlock.Classes.Add("friendRequestUsername");
					
					if (requestType == "Group")
					{
						// Если это группа, пишем название группы и кто пригласил (опционально)
						infoTextBlock.Text = $"Group: {groupName}";
						// Можно добавить ToolTip, чтобы видеть кто пригласил: 
						ToolTip.SetTip(infoTextBlock, $"Invited by: {username}");
					}
					else
					{
						// Если это друг, просто пишем имя
						infoTextBlock.Text = username;
					}

					// Кнопка "Принять"
					acceptRequestButton.Classes.Add("acceptRequestButton");
					var iconCheck = new Uri("avares://Uchat/Assets/Icons/check.png");
					acceptRequestButton.Content = new Image
					{
						Source = new Bitmap(AssetLoader.Open(iconCheck)),
						Width = 16, Height = 14
					};
					acceptRequestButton.Click += AcceptRequestButton_Click;

					// Кнопка "Отклонить"
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
    			public string Username { get { return infoTextBlock.Text; } } // Возвращает то, что написано (имя или группу)

                private async void AcceptRequestButton_Click(object? sender, RoutedEventArgs e)
				{
					acceptRequestButton.IsEnabled = false; // Блокируем кнопку от дабл-клика
					try
					{
						bool success = false;

						if (requestType == "GroupInvite" || requestType == "Group")
						{
							// Логика для ГРУПП (через ChatApiService)
							// contactId здесь выступает как ChatRoomId
							success = await mainWindow._chatApiService.AcceptGroupInviteAsync(contactId);
						}
						else
						{
							// Логика для ДРУЗЕЙ (через ContactApiService)
							// contactId здесь выступает как UserId друга
							success = await mainWindow._contactApiService.AcceptFriendRequestAsync(contactId);
						}
						
						if (success)
						{
							// Удаляем плашку запроса
							mainWindow.requestList.Children.Remove(friendRequestGrid);
							
							// Обновляем список чатов, чтобы появилась новая группа или чат с другом
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
							// Логика для ГРУПП
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
