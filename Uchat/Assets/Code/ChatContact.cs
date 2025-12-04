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
					Chat.ChatcontactList.Remove(contact);
					contactList.Children.Remove(contact.Box);
                }
            }
		}
	}
}