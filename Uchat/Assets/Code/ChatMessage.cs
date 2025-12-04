using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Microsoft.AspNetCore.SignalR.Client;
using System;


namespace Uchat
{
	public partial class MainWindow : Window
	{
		public partial class Chat
		{
			static int counter = 0;
            public class Message
            {
                private string? serverId; // ID сообщения в LiteDB
                private string content;
                private string time;
                private bool isGuest;
                private bool isEdited;
                private bool isReply;
                private string? replyToContent;
                private string? replyToMessageId; // ID сообщения, на которое отвечают

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

                public Message(bool isReply, string text, string timestamp, bool type, string? replyContent = null, string? serverId = null, bool isEdited = false, string? replyToMessageId = null)
                {
                    this.serverId = serverId;
                    content = text;
                    time = timestamp;
                    isGuest = type;
                    this.isEdited = isEdited;
                    this.isReply = isReply;
                    this.replyToContent = replyContent;
                    this.replyToMessageId = replyToMessageId;

                    string messageBorderStyle = (isGuest) ? "guestMessageBorder" : "messageBorder";
                    messageBorder.Classes.Add(messageBorderStyle);
                    messageBorder.Child = messageStackPanel;

                    timeStackPanel.Classes.Add("timeStackPanel");
                    timeStackPanel.Children.Add(timeTextBlock);


                    if (isGuest)
                    { 
                        userNameTextBlock.Classes.Add("username");
                        //place for username
                        userNameTextBlock.Text = "username";
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
                        replyUserName.Text = (isGuest) ? "Guest" : "Me";
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
                public string Content { get { return content; } }
                public string Time { get { return time; } }
                public bool IsEdited { get { return isEdited; } set { isEdited = value; } }
                public bool IsAnswer { get { return isReply; } set { isReply = value; } }
                public Border Bubble { get { return messageBorder; } }
                public TextBlock ContentTextBlock { get { return contentTextBlock; } }
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

                    MenuItem menuItemReply = new MenuItem
                    {
                        Icon = new PathIcon
                        {
                            Data = StreamGeometry.Parse("M113, 1208 C112.346, 1208 109.98, 1208.02 109.98, 1208.02 L109.98, 1213.39 L102.323, 1205 L109.98, 1196.6 L109.98, 1202.01 C109.98, 1202.01 112.48, 1201.98 113, 1202 C120.062, 1202.22 124.966, 1210.26 124.998, 1214.02 C122.84, 1211.25 117.17, 1208 113, 1208 L113, 1208 Z M111.983, 1200.01 L111.983, 1194.11 C112.017, 1193.81 111.936, 1193.51 111.708, 1193.28 C111.312, 1192.89 110.67, 1192.89 110.274, 1193.28 L100.285, 1204.24 C100.074, 1204.45 99.984, 1204.72 99.998, 1205 C99.984, 1205.27 100.074, 1205.55 100.285, 1205.76 L110.219, 1216.65 C110.403, 1216.88 110.67, 1217.03 110.981, 1217.03 C111.265, 1217.03 111.518, 1216.91 111.7, 1216.72 C111.702, 1216.72 111.706, 1216.72 111.708, 1216.71 C111.936, 1216.49 112.017, 1216.18 111.983, 1215.89 C111.983, 1215.89 112, 1210.34 112, 1210 C118.6, 1210 124.569, 1214.75 125.754, 1221.01 C126.552, 1219.17 127, 1217.15 127, 1215.02 C127, 1206.73 120.276, 1200.01 111.983, 1200.01 L111.983, 1200.01 Z"),
                            Width = 16,
                            Height = 16
                        },
                        Header = "Reply"
                    };
                    menuItemReply.Click += MenuItemReply_Click;
                    contextMenu.Items.Add(menuItemReply);

                    MenuItem menuItemEdit = new MenuItem
                    {
                        Icon = new PathIcon
                        {
                            Data = StreamGeometry.Parse("M20.8477 1.87868C19.6761 0.707109 17.7766 0.707105 16.605 1.87868L2.44744 16.0363C2.02864 16.4551 1.74317 16.9885 1.62702 17.5692L1.03995 20.5046C0.760062 21.904 1.9939 23.1379 3.39334 22.858L6.32868 22.2709C6.90945 22.1548 7.44285 21.8693 7.86165 21.4505L22.0192 7.29289C23.1908 6.12132 23.1908 4.22183 22.0192 3.05025L20.8477 1.87868ZM18.0192 3.29289C18.4098 2.90237 19.0429 2.90237 19.4335 3.29289L20.605 4.46447C20.9956 4.85499 20.9956 5.48815 20.605 5.87868L17.9334 8.55027L15.3477 5.96448L18.0192 3.29289ZM13.9334 7.3787L3.86165 17.4505C3.72205 17.5901 3.6269 17.7679 3.58818 17.9615L3.00111 20.8968L5.93645 20.3097C6.13004 20.271 6.30784 20.1759 6.44744 20.0363L16.5192 9.96448L13.9334 7.3787Z"),
                            Width = 16,
                            Height = 16
                        },
                        Header = "Edit"
                    };
                    menuItemEdit.Click += MenuItemEdit_Click;
                    contextMenu.Items.Add(menuItemEdit);

                    MenuItem menuItemCopy = new MenuItem
                    {
                        Icon = new PathIcon
                        {
                            Data = StreamGeometry.Parse("M20 2H10C8.9 2 8 2.9 8 4V16C8 17.1 8.9 18 10 18H20C21.1 18 22 17.1 22 16V4C22 2.9 21.1 2 20 2ZM10 16V4H20V16H10ZM4 8H2V20C2 21.1 2.9 22 4 22H16V20H4V8Z"),
                            Width = 16,
                            Height = 16
                        },
                        Header = "Copy"
                    };
                    menuItemCopy.Click += MenuItemCopy_Click;
                    contextMenu.Items.Add(menuItemCopy);

                    MenuItem menuItemDelete = new MenuItem
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
                    menuItemDelete.Click += MenuItemDelete_Click;

                    contextMenu.Items.Add(menuItemDelete);
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
                    
                    mainWindow.replyToMessageId = chatMessage.ServerId ?? "";
                    mainWindow.replyToMessageContent = chatMessage.Content;
                    mainWindow.replyTheMessageUsername.Text = "Replying to User Name";

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
                    mainWindow.editTheMessage.Text = chatMessage.Content;
                    mainWindow.chatTextBoxForEdit.Text = chatMessage.Content;

                    mainWindow.textBlockChange = chatMessage.ContentTextBlock;
                    mainWindow.messageBeingEdited = chatMessage; 
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

                    if (!string.IsNullOrEmpty(chatMessage.ServerId))
                    {
                        try
                        {
                            await mainWindow.DeleteMessageAsync(chatMessage.ServerId);
                        }
                        catch (Exception ex)
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
	}
}