using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Uchat.Shared;
using Avalonia.Threading;


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

                private static readonly IBrush OnlineBrush = Brush.Parse("#4da64d");
				private static readonly IBrush OfflineBrush = Brush.Parse("#c57179");
                private readonly List<int> participantIds = new();
                private bool allowPresence = true;

                private bool isGroupChat = false;
                private bool isPinned = false;
                private string chatName = "";
                private string lastMessage = "";
                private int unreadMessages = 0;
                private int chatId = 0;
                public DateTime PinnedAt { get; set; } = DateTime.MinValue;
                public DateTime LastMessageAt { get; set; } = DateTime.MinValue;

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
                
				public Contact(string newChatName, string newLastMessage, int newUnreadMessages, MainWindow window, int newChatId = 0, IEnumerable<int>? participants = null)
				{
					this.mainWindow = window;
                    chatName = newChatName;
					lastMessage = newLastMessage;
					unreadMessages = newUnreadMessages;
                    chatId = newChatId;
                    allowPresence = !string.Equals(chatName, "Notes", StringComparison.OrdinalIgnoreCase);
                    //contactStatusBorder.Background = OfflineBrush;
                    //contactStatusBorder.BorderBrush = Brush.Parse("#171a20");
                    contactStatusBorder.IsVisible = false;
                    SetParticipants(participants);

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
				public void SetPinVisual(bool pinNeeded)
				{
					isPinned = pinNeeded;
					pinIcon.IsVisible = pinNeeded;
                }

                public async Task TogglePinAsync()
                {
                    bool newState = !isPinned;

                    contactGrid.IsEnabled = false;

                    try
                    {
                        bool success = await mainWindow._chatApiService.PinChatAsync(this.chatId, newState);

                        if (success)
                        {
                            SetPinVisual(newState);
                            mainWindow.SortChatsInUI();
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Ошибка пина: {ex.Message}");
                    }
                    finally
                    {
                        contactGrid.IsEnabled = true;
                    }
                }

				public Grid Box { get { return contactGrid; } }
				public IBrush Background { get { return contactGrid.Background; } set { contactGrid.Background = value; } }

                public IBrush StatusColor { get { return contactStatusBorder.Background; } set { contactStatusBorder.Background = value; } }

                public IBrush StatusBackground { get { return contactStatusBorder.BorderBrush; } set { contactStatusBorder.BorderBrush = value; } }

                public IBrush LastMessageForeground { get { return lastMessageTextBlock.Foreground; } set { lastMessageTextBlock.Foreground = value; } }
                public bool IsVisible { get { return contactGrid.IsVisible; } set { contactGrid.IsVisible = value; } }
                public bool IsGroupChat { get { return isGroupChat; } set { isGroupChat = value; UpdateIcon(); } }
                public bool IsPinned 
                { 
                    get { return isPinned; } 
                    set 
                    { 
                        isPinned = value; 
                        if (pinIcon != null)
                        {
                            pinIcon.IsVisible = value;
                        }
                    } 
                }
                public int ChatId { get { return chatId; } set { chatId = value; } }
                public string ChatName { get { return chatName; } }
                public int UnreadCount { get { return unreadMessages; } }
                public IReadOnlyList<int> ParticipantIds => participantIds;
				public void AddMember(string name)
				{
					membersList.Add(name);
				}

                public void SetParticipants(IEnumerable<int>? ids)
                {
                    participantIds.Clear();

                    if (ids == null)
                    {
                        return;
                    }

                    participantIds.AddRange(ids.Where(id => id > 0));
                }

                public void UpdatePresence(bool isOnline)
                {
                    if (!allowPresence)
                    {
                        contactStatusBorder.IsVisible = false;
                        return;
                    }

                    contactStatusBorder.IsVisible = !isGroupChat;
                    var brush = isOnline ? OnlineBrush : OfflineBrush;
                    contactStatusBorder.Background = brush;
                    contactStatusBorder.BorderBrush = Brush.Parse("#171a20");
                }

                private void UpdateIcon()
                {
                    var uriString = isGroupChat 
                        ? "avares://Uchat/Assets/Icons/group.png" 
                        : "avares://Uchat/Assets/Icons/avatar.png";
                    string loweredName = chatName.ToLower();
                    if (loweredName.Contains("notes"))
                    {
                        uriString = "avares://Uchat/Assets/Icons/notes.png";
                    }

                    avatarIcon.Source = new Bitmap(AssetLoader.Open(new Uri(uriString)));
                }

				public void UpdateName(string newName)
				{
					chatName = newName;
					
                    Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        if (contactNameTextBlock != null)
                        {
                            contactNameTextBlock.Text = newName;
                        }
                    });
				}
				
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

                public void SetUnreadCount(int value)
                {
                    unreadMessages = Math.Max(0, value);
                    unreadMessageTextBlock.Text = unreadMessages.ToString();
                }

                public void IncrementUnread(int delta = 1)
                {
                    unreadMessages += delta;
                    unreadMessageTextBlock.Text = unreadMessages.ToString();
                }

                public void ShowUnreadMessages()
                {
                    unreadMessageBorder.IsVisible = unreadMessages > 0;
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

                private async void menuItemPinContact_Click(object? sender, RoutedEventArgs e)
                {
                    if (sender is MenuItem item)
                    {
                        item.IsEnabled = false;

                        await contact.TogglePinAsync();
                        
                        item.IsEnabled = true;
                        item.Header = contact.IsPinned ? "Unpin" : "Pin";
                    }
                }

                private async void menuItemDeleteContact_Click(object? sender, RoutedEventArgs e)
                {
                    if (contact.ChatName == "Notes") return;

                    contact.Box.IsEnabled = false;

                    try 
                    {
                        bool success = false;

                        if (contact.IsGroupChat)
                        {
                            success = await mainWindow._chatApiService.LeaveChatAsync(contact.ChatId);
                        }
                        else
                        {
                            success = await mainWindow._contactApiService.DeleteContactByChatRoomAsync(contact.ChatId);
                        }

                        if (success)
                        {
                            mainWindow.RemoveChatFromUI(contact.ChatId);
                        }
                        else
                        {
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