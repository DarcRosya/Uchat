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
using System.Net.NetworkInformation;


namespace Uchat
{
	public partial class MainWindow : Window
	{
        public partial class Chat
		{
            public static string? requestListener = "";
            public static string? SelectedUserName = "";
            public static string? ReplyToUserName = "";
            public class Message
            {
                private string? serverId; // ID сообщения в LiteDB
                private string? sender;
                private string? replyToUserName;
                private string content;
                private string time;
                private bool isGuest;
                private bool isEdited;
                private bool isReply;
                private string? replyToContent;
                private string? replyToMessageId; // ID сообщения, на которое отвечают
                public DateTime SentAt { get; set; }

                private Border messageBorder = new Border();
                private StackPanel messageStackPanel = new StackPanel();
                private TextBlock userNameTextBlock = new TextBlock();
                private TextBlock contentTextBlock = new TextBlock();
                private StackPanel timeStackPanel = new StackPanel();
                private TextBlock timeTextBlock = new TextBlock();

                private Border replyToMessageBorder = new Border();
                private StackPanel replyStackPanel = new StackPanel();
                private TextBlock replyUserName = new TextBlock();
                private TextBlock replyTextBlock = new TextBlock();
                
                public Border? ReplyPreviewBorder { get; set; }

                public Message(bool isReply, string text, string timestamp, bool type,  DateTime sentAt, string? replyContent = null, string? serverId = null, bool isEdited = false, string? replyToMessageId = null, string? username = null, string? replyToUsername = null)
                {
                    this.serverId = serverId;
                    sender = username;
                    content = text;
                    replyToUserName = replyToUsername;
                    time = timestamp;
                    isGuest = type;
                    this.isEdited = isEdited;
                    this.isReply = isReply;
                    this.replyToContent = replyContent;
                    this.replyToMessageId = replyToMessageId;
                    this.SentAt = sentAt;

                    string messageBorderStyle = (isGuest) ? "guestMessageBorder" : "messageBorder";
                    messageBorder.Classes.Add(messageBorderStyle);
                    messageBorder.Child = messageStackPanel;
                    
                    messageBorder.HorizontalAlignment = isGuest ? HorizontalAlignment.Left : HorizontalAlignment.Right;

                    timeStackPanel.Classes.Add("timeStackPanel");
                    timeStackPanel.Children.Add(timeTextBlock);

                    if (isGuest)
                    { 
                        userNameTextBlock.Classes.Add("username");
                        userNameTextBlock.Text = sender ?? "Unknown";
                        messageStackPanel.Children.Add(userNameTextBlock);
                    }

                    contentTextBlock.Classes.Add("chatMessage");
                    contentTextBlock.Text = content;

                    timeTextBlock.Classes.Add("timeTextBlock");
                    string color = (isGuest) ? "#C1E1C1" : "#FFFFFF";
                    timeTextBlock.Foreground = Brush.Parse(color);
                    timeTextBlock.Text = time;
                    
                    // Добавляем метку "edited" если сообщение отредактировано
                    if (this.isEdited)
                    {
                        var editedLabel = new TextBlock
                        {
                            Text = "edited",
                            Foreground = Brush.Parse("#C1E1C1"),
                            FontSize = 10,
                            Padding = new Thickness(0, 0, 3, 0),
                            Margin = new Thickness(0, 3, 0, 0),
                            FontStyle = FontStyle.Italic,
                            HorizontalAlignment = HorizontalAlignment.Right
                        };
                        timeStackPanel.Children.Add(editedLabel);
                    }

                    if (this.isReply)
                    {
                        replyToMessageBorder.Classes.Add("replyToMessageBorder");
                        replyToMessageBorder.Name = "ReplyBorder"; 
                        replyToMessageBorder.Tag = "ReplyBorder"; 
                        replyToMessageBorder.Child = replyStackPanel;
                        
                        this.ReplyPreviewBorder = replyToMessageBorder;

                        replyUserName.Classes.Add("replyUserName");
                        replyUserName.Text = (replyToUserName == Chat.ClientName) ? "Me" : replyToUserName;
                        replyTextBlock.Classes.Add("replyTextBlock");
                        replyTextBlock.Text = replyToContent ?? "(no content)";

                        replyStackPanel.Children.Add(replyUserName);
                        replyStackPanel.Children.Add(replyTextBlock);

                        messageStackPanel.Children.Add(replyToMessageBorder);
                    }
                    messageStackPanel.Children.Add(contentTextBlock);
                    messageStackPanel.Children.Add(timeStackPanel);
                }

                public string? ServerId { get { return serverId; } }
                public string Content { 
                    get { return content; }
                    set { content = value; }
                }
                public string Time { get { return time; } }
                public string Sender { get { return sender; } set { sender = value; } }
                public string ReplyToUserName { get { return replyToUserName; } set { replyToUserName = value; } }
                public bool IsEdited { get { return isEdited; } set { isEdited = value; } }
                public bool IsGuest { get { return isGuest; } set { isGuest = value; } }
                public bool IsAnswer { get { return isReply; } set { isReply = value; } }
                public Border Bubble { get { return messageBorder; } }
                public TextBlock ContentTextBlock { get { return contentTextBlock; } }
                public TextBlock? ReplyTextBlock { get { return replyTextBlock; } }
                public string? ReplyToMessageId { get { return replyToMessageId; } set { replyToMessageId = value; } }
            }

			public class MessageContextMenu
			{
				ContextMenu contextMenu = new ContextMenu();
                Message chatMessage;
                Grid messageGrid;
				MainWindow mainWindow;
				public MessageContextMenu(MainWindow window, Message chatMessage, Grid messageGrid)
				{
                    mainWindow = window;
                    this.messageGrid = messageGrid;
                    this.chatMessage = chatMessage;

                    var iconReplyURL = new Uri("avares://Uchat/Assets/Icons/reply.png");
                    var iconEditURL = new Uri("avares://Uchat/Assets/Icons/edit.png");
                    var iconCopyURL = new Uri("avares://Uchat/Assets/Icons/copy.png");
                    var iconDeleteURL = new Uri("avares://Uchat/Assets/Icons/delete.png");

                    MenuItem menuItemReply = new MenuItem
                    {
                        Icon = new Image
                        {
                            Source = new Bitmap(AssetLoader.Open(iconReplyURL)),
                            Width = 16,
                            Height = 16
                        },
                        Header = "Reply"
                    };
                    menuItemReply.Click += MenuItemReply_Click;
                    contextMenu.Items.Add(menuItemReply);
                    if (!chatMessage.IsGuest)
                    {
                        MenuItem menuItemEdit = new MenuItem
                        {
                            Icon = new Image
                            {
                                Source = new Bitmap(AssetLoader.Open(iconEditURL)),
                                Width = 16,
                                Height = 16
                            },
                            Header = "Edit"
                        };
                   
                        menuItemEdit.Click += MenuItemEdit_Click;
                        contextMenu.Items.Add(menuItemEdit);
                    }

                    MenuItem menuItemCopy = new MenuItem
                        {
                            Icon = new Image
                            {
                                Source = new Bitmap(AssetLoader.Open(iconCopyURL)),
                                Width = 16,
                                Height = 16
                            },
                            Header = "Copy"
                        };
                        menuItemCopy.Click += MenuItemCopy_Click;
                        contextMenu.Items.Add(menuItemCopy);

                    if (!chatMessage.IsGuest)
                    {
                        MenuItem menuItemDelete = new MenuItem
                        {
                            Icon = new Image
                            {
                                Source = new Bitmap(AssetLoader.Open(iconDeleteURL)),
                                Width = 16,
                                Height = 16
                            },
                            Header = new TextBlock
                            {
                                Text = "Delete",
                                Foreground = Brush.Parse("#c57179")
                            },
                        };
                        menuItemDelete.Click += MenuItemDelete_Click;

                        contextMenu.Items.Add(menuItemDelete);
                    }
                }

                private void MenuItemReply_Click(object sender, RoutedEventArgs e)
                {
                    mainWindow.chatTextBoxForEdit.IsVisible = false;
                    mainWindow.chatTextBox.IsVisible = false;
                    mainWindow.editTheMessageBox.IsVisible = false;
                    mainWindow.editTheMessageButton.IsVisible = false;

                    mainWindow.chatTextBoxForReply.IsVisible = true;
                    mainWindow.replyTheMessageBox.IsVisible = true;
                    mainWindow.replyTheMessage.Text = chatMessage.Content;
                    mainWindow.ScrollToBottomButton.Margin = new Thickness(0, 0, 12, 60);

                    mainWindow.replyToMessageId = chatMessage.ServerId ?? "";
                    mainWindow.replyToMessageContent = chatMessage.Content;

                    string userName = (chatMessage.Sender == Chat.ClientName) ? "yourself" : chatMessage.Sender;
                    mainWindow.replyTheMessageUsername.Text = $"Replying to {userName}";

                    Chat.ReplyToUserName = chatMessage.Sender;

                    if (!mainWindow.isReplied)
					{
                        mainWindow.chatTextBoxForReply.Text = mainWindow.chatTextBox.Text;
					}

                    mainWindow.isReplied = true;
                }

                private void MenuItemEdit_Click(object sender, RoutedEventArgs e)
                {
                    mainWindow.tempChatTextBox = mainWindow.chatTextBox.Text;

                    mainWindow.replyTheMessageBox.IsVisible = false;
                    mainWindow.chatTextBox.IsVisible = false;
                    mainWindow.chatTextBoxForReply.IsVisible = false;

                    mainWindow.editTheMessageBox.IsVisible = true;
                    mainWindow.chatTextBoxForEdit.IsVisible = true;
                    mainWindow.editTheMessageButton.IsVisible = true;
                    mainWindow.ScrollToBottomButton.Margin = new Thickness(0, 0, 12, 60);
                    // Use current text from ContentTextBlock instead of original Content field
                    mainWindow.editTheMessage.Text = chatMessage.ContentTextBlock.Text ?? chatMessage.Content;
                    mainWindow.chatTextBoxForEdit.Text = chatMessage.ContentTextBlock.Text ?? chatMessage.Content;

                    mainWindow.textBlockChange = chatMessage.ContentTextBlock;
                    mainWindow.messageBeingEdited = chatMessage; // Сохраняем ссылку на Message
                }
                
                private void MenuItemCopy_Click(object sender, RoutedEventArgs e)
                {
                    mainWindow.Clipboard.SetTextAsync(chatMessage.Content);
                }

				private async void MenuItemDelete_Click(object sender, RoutedEventArgs e)
                {
                    mainWindow.editTheMessageBox.IsVisible = false;
                    mainWindow.replyTheMessageBox.IsVisible = false;
                    mainWindow.editTheMessageButton.IsVisible = false;
                    mainWindow.chatTextBoxForEdit.IsVisible = false;
                    mainWindow.chatTextBoxForReply.IsVisible = false;
                    mainWindow.ScrollToBottomButton.Margin = new Thickness(0, 0, 12, 20);

                    if (!string.IsNullOrEmpty(chatMessage.ServerId))
                    {
                        try
                        {
                            await mainWindow.DeleteMessageAsync(chatMessage.ServerId);
                        }
                        catch
                        {
                            // Failed to delete message
                        }
                    }
                    else
                    {
                        mainWindow.ChatMessagesPanel.Children.Remove(messageGrid);
                    }
                    
                    mainWindow.chatTextBox.IsVisible = true;
				}

                public ContextMenu Result()
                {
                    return contextMenu;
                }
			}
		}

        public Control CreateUnreadSeparator()
        {
            var grid = new Grid
            {
                RowDefinitions = RowDefinitions.Parse("Auto,Auto"),
                Margin = new Thickness(0, 20, 0, 20), // Отступы побольше, чтобы заметно было
                Tag = "UnreadSeparator" // Тег, чтобы отличать от сообщений
            };

            var textBlock = new TextBlock
            {
                Text = "Unread messages",
                FontSize = 12,
                FontWeight = FontWeight.SemiBold,
                Foreground = Brush.Parse("#c57179"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0,0,0,5)
            };
            Grid.SetRow(textBlock, 0);

            var border = new Border
            {
                Height = 1, // Тонкая линия
                Background = Brush.Parse("#c57179"),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Opacity = 0.5
            };
            Grid.SetRow(border, 1);

            grid.Children.Add(textBlock);
            grid.Children.Add(border);

            return grid;
        }
	}
}