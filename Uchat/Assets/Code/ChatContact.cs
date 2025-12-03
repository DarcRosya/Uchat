using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.LogicalTree;
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
			static List<Contact> ChatcontactList = new List<Contact>();
			//static bool areGroups = false;
			public class Contact
			{
				private string chatName = "";
				private string lastMessage = "";
				private int unreadMessages = 0;

				private StackPanel contactList = new StackPanel(); 
				private Grid contactGrid = new Grid();
				private Avalonia.Controls.Image avatarIcon = new Avalonia.Controls.Image();
				private StackPanel contactStackPanel = new StackPanel();
				private TextBlock contactNameTextBlock = new TextBlock();
				private TextBlock lastMessageTextBlock = new TextBlock();

				public Contact (string newChatName, string newLastMessage, int newUnreadMessages, StackPanel attachedContactList)
				{
					contactList = attachedContactList;
					chatName = newChatName;
                    lastMessage = newLastMessage;

                    contactGrid.Classes.Add("contactGrid");
					contactGrid.PointerPressed += ContactGridClicked;
                    contactGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(50)));
                    contactGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
                    contactStackPanel.Height = 50;


                    var imageURL = new Uri("avares://Uchat/Assets/Icons/avatar.png");
					avatarIcon.Classes.Add("avatarIcon");
					avatarIcon.Source = new Bitmap(AssetLoader.Open(imageURL));

					contactNameTextBlock.Classes.Add("contactName");
					contactNameTextBlock.Text = chatName;

					lastMessageTextBlock.Classes.Add("lastMessage");
					lastMessageTextBlock.Text = lastMessage;

                    contactStackPanel.Children.Add(contactNameTextBlock);
					contactStackPanel.Children.Add(lastMessageTextBlock);

					contactGrid.Children.Add(avatarIcon);
					contactGrid.Children.Add(contactStackPanel);

					Grid.SetColumn(avatarIcon, 0);
					Grid.SetColumn(contactStackPanel, 1);

					ContactContextMenu contextMenu = new ContactContextMenu(this, contactList);
					contactGrid.ContextMenu = contextMenu.Result();

                    attachedContactList.Children.Add(this.Box);
					Chat.ChatcontactList.Add(this);
                }

                public Grid Box { get { return contactGrid; } }
                public IBrush Background { get { return contactGrid.Background; } set { contactGrid.Background = value; } }

				private void ContactGridClicked(object sender, Avalonia.Input.PointerPressedEventArgs e)
				{
					foreach (Contact contact in Chat.ChatcontactList)
					{
						contact.Background = Brush.Parse("#171a20");
					}
					this.Background = Brush.Parse("#425a78");
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

                    var imagePinURL = new Uri("avares://Uchat/Assets/Icons/pin.png");

                    MenuItem menuItemPinContact = new MenuItem
					{
                        Icon = new Image
                        {
                            Source = new Bitmap(AssetLoader.Open(imagePinURL)),
                            Width = 16,
                            Height = 16
                        },
                        Header = "Pin",
                    };
                    menuItemPinContact.Click += menuItemPinContact_Click;
                    contextMenu.Items.Add(menuItemPinContact);

                    MenuItem menuItemDeleteContact = new MenuItem
                    {
                        Icon = new PathIcon
                        {
                            Foreground = Brush.Parse("#c57179"),
                            Data = StreamGeometry.Parse("M24,7.25 C27.1017853,7.25 29.629937,9.70601719 29.7458479,12.7794443 L29.75,13 L37,13 C37.6903559,13 38.25,13.5596441 38.25,14.25 C38.25,14.8972087 37.7581253,15.4295339 37.1278052,15.4935464 L37,15.5 L35.909,15.5 L34.2058308,38.0698451 C34.0385226,40.2866784 32.1910211,42 29.9678833,42 L18.0321167,42 C15.8089789,42 13.9614774,40.2866784 13.7941692,38.0698451 L12.09,15.5 L11,15.5 C10.3527913,15.5 9.8204661,15.0081253 9.75645361,14.3778052 L9.75,14.25 C9.75,13.6027913 10.2418747,13.0704661 10.8721948,13.0064536 L11,13 L18.25,13 C18.25,9.82436269 20.8243627,7.25 24,7.25 Z M33.4021054,15.5 L14.5978946,15.5 L16.2870795,37.8817009 C16.3559711,38.7945146 17.116707,39.5 18.0321167,39.5 L29.9678833,39.5 C30.883293,39.5 31.6440289,38.7945146 31.7129205,37.8817009 L33.4021054,15.5 Z M27.25,20.75 C27.8972087,20.75 28.4295339,21.2418747 28.4935464,21.8721948 L28.5,22 L28.5,33 C28.5,33.6903559 27.9403559,34.25 27.25,34.25 C26.6027913,34.25 26.0704661,33.7581253 26.0064536,33.1278052 L26,33 L26,22 C26,21.3096441 26.5596441,20.75 27.25,20.75 Z M20.75,20.75 C21.3972087,20.75 21.9295339,21.2418747 21.9935464,21.8721948 L22,22 L22,33 C22,33.6903559 21.4403559,34.25 20.75,34.25 C20.1027913,34.25 19.5704661,33.7581253 19.5064536,33.1278052 L19.5,33 L19.5,22 C19.5,21.3096441 20.0596441,20.75 20.75,20.75 Z M24,9.75 C22.2669685,9.75 20.8507541,11.1064548 20.7551448,12.8155761 L20.75,13 L27.25,13 C27.25,11.2050746 25.7949254,9.75 24,9.75 Z"),
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
					Chat.ChatcontactList.Remove(contact);
					contactList.Children.Remove(contact.Box);
                }
            }
		}
	}
}