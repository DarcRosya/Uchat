using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System;
using System.Collections.Generic;
using Uchat.Shared;


namespace Uchat
{
	public partial class MainWindow : Window
	{
		public partial class Chat
		{
			public static string ClientName = "Uchat Client";

            public static List<Contact> ChatsList = new List<Contact>();
            public static bool GroupsActive = false;

			public static void ShowGroups(bool groupsNeeded)
			{
                foreach (Contact contact in Chat.ChatsList)
                {
                    contact.IsVisible = (contact.IsGroupChat == true) ? groupsNeeded : !groupsNeeded;
                }
            }
			public class Contact
			{
				private List<string> membersList = new List<string>();

				private bool isGroupChat = false;
				private bool isPinned = false;
				private string chatName = "";
				private string lastMessage = "";
				private int unreadMessages = 0;
				private int chatId = 0;

				private MainWindow mainWindow;
                private Grid contactGrid = new Grid();

				private Border avatarIconBorder = new Border();
                private Avalonia.Controls.Image avatarIcon = new Avalonia.Controls.Image();
                private Avalonia.Controls.Image pinIcon = new Avalonia.Controls.Image();
                private Border contactStatusBorder = new Border();

                private StackPanel contactStackPanel = new StackPanel();
                private StackPanel contactInfoStackPanel = new StackPanel();
                private TextBlock contactNameTextBlock = new TextBlock();
				private TextBlock lastMessageTextBlock = new TextBlock();

				private Border unreadMessageBorder = new Border();
				private TextBlock unreadMessageTextBlock = new TextBlock();
				public Contact(string newChatName, string newLastMessage, int newUnreadMessages, MainWindow window, int newChatId = 0)
				{
					this.mainWindow = window;
                    chatName = newChatName;
					lastMessage = newLastMessage;
					unreadMessages = newUnreadMessages;
					chatId = newChatId;

					contactGrid.Classes.Add("contactGrid");
					contactGrid.PointerPressed += ContactGridClicked;
					contactGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(50)));
					contactGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
					contactGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(50)));
					contactStackPanel.Height = 50;

                    avatarIconBorder.Classes.Add("avatarIconBorder");
					avatarIconBorder.Child = avatarIcon;
                    avatarIconBorder.Background= new SolidColorBrush(Colors.Transparent);

                    avatarIcon.Classes.Add("avatarIcon");
					var uriString = "avares://Uchat/Assets/Icons/avatar.png";
                    avatarIcon.Source = new Bitmap(AssetLoader.Open(new Uri(uriString)));

                    contactStatusBorder.Classes.Add("contactStatusBorder");

                    contactInfoStackPanel.Orientation = Avalonia.Layout.Orientation.Horizontal;
                    var icon = new Uri("avares://Uchat/Assets/Icons/pin.png");
                    pinIcon = new Image
                    {
                        Source = new Bitmap(AssetLoader.Open(icon)),
                        Width = 10,
                        Height = 10,
						IsVisible = false
                    };
                    contactInfoStackPanel.Children.Add(pinIcon);
                    contactInfoStackPanel.Children.Add(contactNameTextBlock);

                    contactNameTextBlock.Classes.Add("contactName");
					contactNameTextBlock.Text = chatName;

					lastMessageTextBlock.Classes.Add("lastMessage");
					lastMessageTextBlock.Text = lastMessage;

					unreadMessageBorder.Classes.Add("unreadMessageBorder");
					unreadMessageBorder.Child = unreadMessageTextBlock;

					unreadMessageTextBlock.Classes.Add("unreadMessage");
					unreadMessageTextBlock.Text = unreadMessages.ToString();

					contactStackPanel.Children.Add(contactInfoStackPanel);
					contactStackPanel.Children.Add(lastMessageTextBlock);

					contactGrid.Children.Add(avatarIconBorder);
                    contactGrid.Children.Add(contactStatusBorder);
                    contactGrid.Children.Add(contactStackPanel);
					contactGrid.Children.Add(unreadMessageBorder);

					Grid.SetColumn(avatarIcon, 0);
					Grid.SetColumn(contactStatusBorder, 0);
					Grid.SetColumn(contactStackPanel, 1);
					Grid.SetColumn(unreadMessageBorder, 2);

					ContactContextMenu contextMenu = new ContactContextMenu(this, mainWindow.contactsStackPanel, mainWindow);
					contactGrid.ContextMenu = contextMenu.Result();

					Chat.ChatsList.Add(this);
                }
				public void Pin(bool pinNeeded)
				{
					isPinned = pinNeeded;
					pinIcon.IsVisible = pinNeeded;
                }

				public Grid Box { get { return contactGrid; } }
				public IBrush Background { get { return contactGrid.Background; } set { contactGrid.Background = value; } }

                public IBrush StatusColor { get { return contactStatusBorder.Background; } set { contactStatusBorder.Background = value; } }

                public IBrush StatusBackground { get { return contactStatusBorder.BorderBrush; } set { contactStatusBorder.BorderBrush = value; } }

                public IBrush LastMessageForeground { get { return lastMessageTextBlock.Foreground; } set { lastMessageTextBlock.Foreground = value; } }
                public bool IsVisible { get { return contactGrid.IsVisible; } set { contactGrid.IsVisible = value; } }
                public bool IsGroupChat { get { return isGroupChat; } set { isGroupChat = value; UpdateIcon(); } }
                public bool IsPinned { get { return isPinned; } set { isPinned = value; } }
                public int ChatId { get { return chatId; } set { chatId = value; } }
				public string ChatName { get { return chatName; } }
				public void AddMember(string name)
				{
					membersList.Add(name);
				}

				private void UpdateIcon()
				{
					var uriString = isGroupChat 
						? "avares://Uchat/Assets/Icons/group.png" 
						: "avares://Uchat/Assets/Icons/avatar.png";

                    avatarIcon.Source = new Bitmap(AssetLoader.Open(new Uri(uriString)));
                    contactStatusBorder.IsVisible = !isGroupChat;
				}

				public void UpdateName(string newName)
				{
					chatName = newName;

					contactNameTextBlock.Text = newName;
				}
				
				/// <summary>
				/// Обновить последнее сообщение и счетчик непрочитанных
				/// </summary>
				public void UpdateLastMessage(string newLastMessage, int? newUnreadCount = null)
				{
					lastMessage = newLastMessage;
					lastMessageTextBlock.Text = newLastMessage;
					
					if (newUnreadCount.HasValue)
					{
						unreadMessages = newUnreadCount.Value;
						unreadMessageTextBlock.Text = newUnreadCount.Value.ToString();
					}
				}
				
                private void ContactGridClicked(object sender, Avalonia.Input.PointerPressedEventArgs e)
				{
					if (!e.GetCurrentPoint(sender as Control).Properties.IsLeftButtonPressed)
					{
						return;
					}
					
					string color = (GroupsActive == true) ? "#4a8284" : "#4b678a";

                    foreach (Contact contact in Chat.ChatsList)
					{
						contact.Background = Brush.Parse("#171a20");
						contact.LastMessageForeground = Brush.Parse("#999999");
						contact.StatusBackground = Brush.Parse("#171a20");
                    }
					this.Background = Brush.Parse(color);
                    this.LastMessageForeground = Brush.Parse("#FFFFFF");
					this.StatusBackground = Brush.Parse(color);

                    mainWindow.groupTopBar.IsVisible = this.isGroupChat;
                    mainWindow.friendTopBar.IsVisible = !this.isGroupChat;

                    mainWindow.groupTopBarName.Text = this.ChatName;
                    mainWindow.friendTopBarName.Text = this.ChatName;

                    mainWindow.PlaceHolder.IsVisible = false;
                    mainWindow.BottomContainer.IsVisible = true;

                    if (chatId > 0)
					{
						_ = mainWindow.OpenChatAsync(chatId);
					}
				}
            }

            public class ContactContextMenu
			{
				private ContextMenu contextMenu = new ContextMenu();
				private Contact contact;
				private StackPanel contactList;
				private MainWindow mainWindow;

                public ContactContextMenu(Contact contact, StackPanel contactList, MainWindow window)
				{
					this.contact = contact;
					this.contactList = contactList;
					this.mainWindow = window;

                    var iconPinURL = new Uri("avares://Uchat/Assets/Icons/pin.png");
                    var iconDeleteURL = new Uri("avares://Uchat/Assets/Icons/delete.png");

					MenuItem menuItemPinContact = new MenuItem
					{
						Icon = new Image
						{
							Source = new Bitmap(AssetLoader.Open(iconPinURL)),
							Width = 16,
							Height = 16
						},
						Header = "Pin",
                    };
                    menuItemPinContact.Click += menuItemPinContact_Click;
                    contextMenu.Items.Add(menuItemPinContact);

                    MenuItem menuItemDeleteContact = new MenuItem
                    {
                        Icon = new Image
                        {
                            Source = new Bitmap(AssetLoader.Open(iconDeleteURL)),
                            Width = 16,
                            Height = 16
                        },
                        Header = "Delete",
                        Foreground = Brush.Parse("#c57179")
                    };
                    menuItemDeleteContact.Click += menuItemDeleteContact_Click;
                    contextMenu.Items.Add(menuItemDeleteContact);
                }

                public ContextMenu Result()
                {
                    return contextMenu;
                }

                private void menuItemPinContact_Click(object? sender, RoutedEventArgs e)
                {
					bool currentState = contact.IsPinned;

                    contact.Pin(!currentState);
                }

                private async void menuItemDeleteContact_Click(object? sender, RoutedEventArgs e)
                {
                    // 1. Защита: нельзя удалить чат "Notes" (Избранное)
                    if (contact.ChatName == "Notes") return;

                    // Блокируем элемент, чтобы пользователь не нажал дважды
                    contact.Box.IsEnabled = false;

                    try 
                    {
                        bool success = false;

                        // 2. ПРОВЕРКА: Группа это или Личный чат?
                        if (contact.IsGroupChat)
                        {
                            // === ЛОГИКА ДЛЯ ГРУПП ===
                            // Мы "покидаем" группу на сервере
                            success = await mainWindow._chatApiService.LeaveChatAsync(contact.ChatId);
                        }
                        else
                        {
                            // === ЛОГИКА ДЛЯ ЛИЧНЫХ ЧАТОВ ===
                            // Мы удаляем контакт/историю (как было раньше)
                            success = await mainWindow._contactApiService.DeleteContactByChatRoomAsync(contact.ChatId);
                        }

                        // 3. Обработка результата
                        if (success)
                        {
                            // Если сервер сказал "ОК" — удаляем чат из интерфейса
                            mainWindow.RemoveChatFromUI(contact.ChatId);
                        }
                        else
                        {
                            // Если ошибка — разблокируем обратно
                            contact.Box.IsEnabled = true;
                            Logger.Log("Server returned false when deleting/leaving chat");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("Failed to delete/leave chat", ex);
                        contact.Box.IsEnabled = true;
                    }
                }
			}
        }
        private async void NotificationButton_Click(object sender, RoutedEventArgs e)
        {
            NotificationBox.IsVisible = !NotificationBox.IsVisible;
        }
    }
}