using Avalonia.Interactivity;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System;


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
				private string chatName = "";
				private string lastMessage = "";
				private int unreadMessages = 0;
				private int chatId = 0;

				private MainWindow mainWindow = new MainWindow();
                private Grid contactGrid = new Grid();
				private Border avatarIconBorder = new Border();
                private Avalonia.Controls.Image avatarIcon = new Avalonia.Controls.Image();
				private StackPanel contactStackPanel = new StackPanel();
				private TextBlock contactNameTextBlock = new TextBlock();
				private TextBlock lastMessageTextBlock = new TextBlock();
				private Border unreadMessageBorder = new Border();
				private TextBlock unreadMessageTextBlock = new TextBlock();
				public Contact(string newChatName, string newLastMessage, int newUnreadMessages, MainWindow window, int newChatId = 0)
				{
					this.mainWindow = window;
					isGroupChat = Chat.GroupsActive;
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

                    string iconPath;
                    if (GroupsActive)
                    {
                        iconPath = "avares://Uchat/Assets/Icons/group_contact.png";
						avatarIconBorder.Padding = new Avalonia.Thickness(0);
                    }
                    else
                    {
                        iconPath = "avares://Uchat/Assets/Icons/avatar.png";
                        avatarIconBorder.Background= new SolidColorBrush(Colors.Transparent);
                    }

                    var imageURL = new Uri(iconPath);
					avatarIcon.Classes.Add("avatarIcon");
					avatarIcon.Source = new Bitmap(AssetLoader.Open(imageURL));

					contactNameTextBlock.Classes.Add("contactName");
					contactNameTextBlock.Text = chatName;

					lastMessageTextBlock.Classes.Add("lastMessage");
					lastMessageTextBlock.Text = lastMessage;

					unreadMessageBorder.Classes.Add("unreadMessageBorder");
					unreadMessageBorder.Child = unreadMessageTextBlock;

					unreadMessageTextBlock.Classes.Add("unreadMessage");
					unreadMessageTextBlock.Text = unreadMessages.ToString();

					contactStackPanel.Children.Add(contactNameTextBlock);
					contactStackPanel.Children.Add(lastMessageTextBlock);

					contactGrid.Children.Add(avatarIconBorder);
					contactGrid.Children.Add(contactStackPanel);
					contactGrid.Children.Add(unreadMessageBorder);

					Grid.SetColumn(avatarIcon, 0);
					Grid.SetColumn(contactStackPanel, 1);
					Grid.SetColumn(unreadMessageBorder, 2);

					ContactContextMenu contextMenu = new ContactContextMenu(this, mainWindow.contactsStackPanel);
					contactGrid.ContextMenu = contextMenu.Result();

					Chat.chatsList.Add(this);
                }

				public Grid Box { get { return contactGrid; } }
				public IBrush Background { get { return contactGrid.Background; } set { contactGrid.Background = value; } }
				public bool IsVisible { get { return contactGrid.IsVisible; } set { contactGrid.IsVisible = value; } }
                public bool IsGroupChat { get { return isGroupChat; } set { isGroupChat = value; } }
				public int ChatId { get { return chatId; } set { chatId = value; } }
                private void ContactGridClicked(object sender, Avalonia.Input.PointerPressedEventArgs e)
				{
					string color = (GroupsActive == true) ? "#5da3a5" : "#5e81ac";

                    foreach (Contact contact in Chat.chatsList)
					{
						contact.Background = Brush.Parse("#171a20");
					}
					this.Background = Brush.Parse(color);
					
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

                public ContactContextMenu(Contact contact, StackPanel contactList)
				{
					this.contact = contact;
					this.contactList = contactList;

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
					contactList.Children.Remove(contact.Box);
                    contactList.Children.Insert(0, contact.Box);
                }

                private void menuItemDeleteContact_Click(object? sender, RoutedEventArgs e)
                {
					Chat.chatsList.Remove(contact);
					contactList.Children.Remove(contact.Box);
                }
            }
		}
	}
}