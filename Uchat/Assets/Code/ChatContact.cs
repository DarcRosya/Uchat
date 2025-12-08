using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;


namespace Uchat
{
	public partial class MainWindow : Window
	{
		public partial class Chat
		{
			public static List<Contact> chatsList = new List<Contact>();
			public static bool GroupsActive = false;

			public static void ShowGroups(bool groupsNeeded)
			{
                foreach (Contact contact in Chat.chatsList)
                {
                    contact.IsVisible = (contact.IsGroupChat == true) ? groupsNeeded : !groupsNeeded;
                }
            }
			public class Contact
			{
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
				private Ellipse contactStatusEllipse = new Ellipse();

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

                    var imageURL = new Uri("avares://Uchat/Assets/Icons/avatar.png");
					if(GroupsActive == true) imageURL = new Uri("avares://Uchat/Assets/Icons/group.png");
                    avatarIcon.Classes.Add("avatarIcon");
					avatarIcon.Source = new Bitmap(AssetLoader.Open(imageURL));

					contactStatusBorder.Classes.Add("contactStatusBorder");
					contactStatusBorder.Child = contactStatusEllipse;

					contactStatusEllipse.Classes.Add("contactStatusEllipse");

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

					Chat.chatsList.Add(this);
                }
				public void Pin(bool pinNeeded)
				{
					isPinned = pinNeeded;
					pinIcon.IsVisible = pinNeeded;
                }

				public Grid Box { get { return contactGrid; } }
				public IBrush Background { get { return contactGrid.Background; } set { contactGrid.Background = value; } }

                public IBrush StatusColor { get { return contactStatusEllipse.Fill; } set { contactStatusEllipse.Fill = value; } }

                public IBrush StatusBackground { get { return contactStatusBorder.BorderBrush; } set { contactStatusBorder.BorderBrush = value; } }

                public IBrush LastMessageForeground { get { return lastMessageTextBlock.Foreground; } set { lastMessageTextBlock.Foreground = value; } }
                public bool IsVisible { get { return contactGrid.IsVisible; } set { contactGrid.IsVisible = value; } }
                public bool IsGroupChat { get { return isGroupChat; } set { isGroupChat = value; } }
                public bool IsPinned { get { return isPinned; } set { isPinned = value; } }
                public int ChatId { get { return chatId; } set { chatId = value; } }
				public string ChatName { get { return chatName; } }
				
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
					// FIX 3: Only handle left mouse button clicks
					if (!e.GetCurrentPoint(sender as Control).Properties.IsLeftButtonPressed)
					{
						return; // Right click will show context menu automatically
					}
					
					string color = (GroupsActive == true) ? "#4a8284" : "#4b678a";

                    foreach (Contact contact in Chat.chatsList)
					{
						contact.Background = Brush.Parse("#171a20");
						contact.LastMessageForeground = Brush.Parse("#999999");
						contact.StatusBackground = Brush.Parse("#171a20");
                    }
					this.Background = Brush.Parse(color);
                    this.LastMessageForeground = Brush.Parse("#FFFFFF");
					this.StatusBackground = Brush.Parse(color);

                    if (chatId > 0)
					{
						_ = mainWindow.OpenChatAsync(chatId);
					}
				}
            }

            public class Group : Contact
			{
				private List<string> membersName;
				public Group(string groupName, string newLastMessage, int newUnreadMessages, MainWindow window, int newChatId = 0)
					: base(groupName, newLastMessage, newUnreadMessages, window, newChatId)
				{

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
					if (contact.ChatName == "Notes") return;

					contact.Box.IsEnabled = false;

					try 
					{
						var success = await mainWindow._contactApiService.DeleteContactByChatRoomAsync(contact.ChatId);

						if (success)
						{
							mainWindow.RemoveChatFromUI(contact.ChatId);
						}
						else
						{
							contact.Box.IsEnabled = true;
						}
					}
					catch (Exception ex)
					{
						Logger.Error("Failed to delete chat", ex);
						contact.Box.IsEnabled = true;
					}
				}
			}
        }
        private async void NotificationButton_Click(object sender, RoutedEventArgs e)
        {
            NotificationBox.IsVisible = !NotificationBox.IsVisible;

            if (NotificationBox.IsVisible)
                await LoadPendingFriendRequestsAsync();
        }
    }
}