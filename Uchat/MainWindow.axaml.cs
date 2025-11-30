using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using System;
using System.Threading;
using static System.Net.Mime.MediaTypeNames;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Uchat
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private TextBlock textBlockChange = new TextBlock();
        private bool isReplied = false;
        private string tempChatTextBox = "";
        private string tempReplyTextBox = "";

        private void SendButton_Click(object? sender, RoutedEventArgs e)
        {
            int messagesCount = ChatMessagesPanel.Children.Count;
            string message;

            if (isReplied)
            {
                message = chatTextBoxForReply.Text?.Trim() ?? "";
            }
            else
            {
                message = chatTextBox.Text?.Trim() ?? "";
            }

            if (!string.IsNullOrEmpty(message))
            {
                replyTheMessageBox.IsVisible = false;

                var textBlock = new TextBlock
                {
                    Text = message,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                    FontSize = 16,
                    Background = Brushes.Transparent,
                    Foreground = Brushes.White,
                    MaxWidth = 500,
                    TextWrapping = TextWrapping.Wrap
                };

                var timeTextBlock = new TextBlock
                {
                    Text = DateTime.Now.ToString("HH:mm"),
                    Foreground = Brush.Parse("#C1E1C1"),
                    FontSize = 10,
                    Margin = new Thickness(0, 3, 0, 0),
                    Padding = new Thickness(0, 0, 3, 0),
                };

                var editedTextBlock = new StackPanel();
                var timeAndEditTextBlock = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right
                };

                if (isReplied)
                {
                    if (string.IsNullOrEmpty(chatTextBoxForReply.Text?.Trim()))
                    {
                        return;
                    }

                    var replyToMessageInfo = new StackPanel();
                    var replyToMessageBorder = new Border
                    {
                        Background = Brush.Parse("#20000000"),
                        BorderBrush = Brush.Parse("#8FBC8F"),
                        BorderThickness = Thickness.Parse("4,0,0,0"),
                        CornerRadius = new CornerRadius(5),
                        Padding = new Thickness(8, 5),
                        Margin = new Thickness(5, 0, 5, 5),
                        Child = replyToMessageInfo,
                        Name = "ReplyToMessage",
                        MinWidth = 120,
                        MaxWidth = 500
                    };

                    replyToMessageBorder.Bind(Border.WidthProperty, new Binding("Bounds.Width") { Source = textBlock });

                    var replyUserName = new TextBlock
                    {
                        Text = "User Name",
                        Foreground = Brush.Parse("#D0F0C0"),
                        FontWeight = FontWeight.SemiBold,
                        FontSize = 12
                    };

                    var replyText = new TextBlock
                    {
                        Text = tempReplyTextBox,
                        Foreground = Brush.Parse("#E0E0E0"),
                        FontWeight = FontWeight.SemiBold,
                        FontSize = 12,
                        MaxLines = 1,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                    };

                    replyToMessageInfo.Children.Add(replyUserName);
                    replyToMessageInfo.Children.Add(replyText);

                    editedTextBlock.Children.Add(replyToMessageBorder);
                }

                timeAndEditTextBlock.Children.Add(timeTextBlock);
                editedTextBlock.Children.Add(textBlock);
                editedTextBlock.Children.Add(timeAndEditTextBlock);

                var bubble = new Border
                {
                    CornerRadius = new CornerRadius(5),
                    Padding = new Thickness(8),
                    Margin = new Thickness(5, 0, 5, 0),
                    Child = editedTextBlock,
                    Background = Brush.Parse("#358c8f"),
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right
                };

                var grid = new Grid
                {
                    ColumnDefinitions =
                    {
                        new ColumnDefinition(new GridLength(1, GridUnitType.Star))
                    }
                };

                Grid.SetColumn(bubble, 0);
                grid.Children.Add(bubble);

                //Recieve message
                if (messagesCount % 2 == 1)
                {
                    bubble.Background = Brush.Parse("#264c6f");
                    bubble.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
                }

                ChatMessagesPanel.Children.Add(grid);
                chatTextBox.Text = string.Empty;
                ChatScrollViewer.ScrollToEnd();

                var contextMenu = new ContextMenu();
                bubble.ContextMenu = contextMenu;

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

                menuItemReply.Click += (s, e) =>
                {
                    chatTextBoxForEdit.IsVisible = false;
                    chatTextBox.IsVisible = false;
                    editTheMessageBox.IsVisible = false;
                    editTheMessageButton.IsVisible = false;

                    chatTextBoxForReply.IsVisible = true;
                    replyTheMessageBox.IsVisible = true;
                    replyTheMessage.Text = textBlock.Text;
                    tempReplyTextBox = textBlock.Text;
                    replyTheMessageUsername.Text = "Replying to User Name";

                    if (!isReplied)
                    {
                        chatTextBoxForReply.Text = chatTextBox.Text;
                    }

                    isReplied = true;
                };
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

                menuItemEdit.Click += (s, e) =>
                {
                    tempChatTextBox = chatTextBox.Text;

                    replyTheMessageBox.IsVisible = false;
                    chatTextBox.IsVisible = false;
                    chatTextBoxForReply.IsVisible = false;

                    editTheMessageBox.IsVisible = true;
                    chatTextBoxForEdit.IsVisible = true;
                    editTheMessageButton.IsVisible = true;
                    editTheMessage.Text = textBlock.Text;
                    chatTextBoxForEdit.Text = textBlock.Text;

                    textBlockChange = textBlock;
                };
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

                menuItemCopy.Click += (s, e) =>
                {
                    Clipboard.SetTextAsync(textBlock.Text);
                };
                contextMenu.Items.Add(menuItemCopy);

                MenuItem menuItemDelete = new MenuItem
                {
                    Icon = new PathIcon
                    {
                        Data = StreamGeometry.Parse("M24,7.25 C27.1017853,7.25 29.629937,9.70601719 29.7458479,12.7794443 L29.75,13 L37,13 C37.6903559,13 38.25,13.5596441 38.25,14.25 C38.25,14.8972087 37.7581253,15.4295339 37.1278052,15.4935464 L37,15.5 L35.909,15.5 L34.2058308,38.0698451 C34.0385226,40.2866784 32.1910211,42 29.9678833,42 L18.0321167,42 C15.8089789,42 13.9614774,40.2866784 13.7941692,38.0698451 L12.09,15.5 L11,15.5 C10.3527913,15.5 9.8204661,15.0081253 9.75645361,14.3778052 L9.75,14.25 C9.75,13.6027913 10.2418747,13.0704661 10.8721948,13.0064536 L11,13 L18.25,13 C18.25,9.82436269 20.8243627,7.25 24,7.25 Z M33.4021054,15.5 L14.5978946,15.5 L16.2870795,37.8817009 C16.3559711,38.7945146 17.116707,39.5 18.0321167,39.5 L29.9678833,39.5 C30.883293,39.5 31.6440289,38.7945146 31.7129205,37.8817009 L33.4021054,15.5 Z M27.25,20.75 C27.8972087,20.75 28.4295339,21.2418747 28.4935464,21.8721948 L28.5,22 L28.5,33 C28.5,33.6903559 27.9403559,34.25 27.25,34.25 C26.6027913,34.25 26.0704661,33.7581253 26.0064536,33.1278052 L26,33 L26,22 C26,21.3096441 26.5596441,20.75 27.25,20.75 Z M20.75,20.75 C21.3972087,20.75 21.9295339,21.2418747 21.9935464,21.8721948 L22,22 L22,33 C22,33.6903559 21.4403559,34.25 20.75,34.25 C20.1027913,34.25 19.5704661,33.7581253 19.5064536,33.1278052 L19.5,33 L19.5,22 C19.5,21.3096441 20.0596441,20.75 20.75,20.75 Z M24,9.75 C22.2669685,9.75 20.8507541,11.1064548 20.7551448,12.8155761 L20.75,13 L27.25,13 C27.25,11.2050746 25.7949254,9.75 24,9.75 Z"),
                        Width = 16,
                        Height = 16
                    },
                    Header = "Delete"
                };

                menuItemDelete.Click += (s, e) =>
                {
                    editTheMessageBox.IsVisible = false;
                    replyTheMessageBox.IsVisible = false;

                    editTheMessageButton.IsVisible = false;
                    chatTextBoxForEdit.IsVisible = false;

                    chatTextBoxForReply.IsVisible = false;

                    ChatMessagesPanel.Children.Remove(grid);
                    chatTextBox.IsVisible = true;
                };
                contextMenu.Items.Add(menuItemDelete);

                chatTextBox.Text = "";
                chatTextBoxForReply.Text = "";
                chatTextBox.IsVisible = true;
                chatTextBoxForReply.IsVisible = false;
                isReplied = false;
            }
        }

        private void DontReplyTheMessage_Click(object? sender, RoutedEventArgs e)
        {
            replyTheMessageBox.IsVisible = false;
            chatTextBoxForReply.IsVisible = false;
            chatTextBox.Text = chatTextBoxForReply.Text;
            chatTextBox.IsVisible = true;
            isReplied = false;
            chatTextBoxForReply.Text = "";
        }


        private void DontEditTheMessage_Click(object? sender, RoutedEventArgs e)
        {
            CloseEditMode();
        }

        private void EditMessageButton_Click(object? sender, RoutedEventArgs e)
        {
            if ((textBlockChange.Text == chatTextBoxForEdit.Text)
                || string.IsNullOrEmpty(chatTextBoxForEdit.Text))
            {
                CloseEditMode();
                textBlockChange = null;
                return;
            }

            if (textBlockChange != null)
            {
                var editedText = new TextBlock
                {
                    Text = "edited",
                    Foreground = Brush.Parse("#C1E1C1"),
                    FontSize = 10,
                    Padding = new Thickness(0, 0, 3, 0),
                    Margin = new Thickness(0, 3, 0, 0),
                    FontStyle = FontStyle.Italic,
                    HorizontalAlignment = HorizontalAlignment.Right,
                };


                textBlockChange.Text = chatTextBoxForEdit.Text;

                if (textBlockChange.Parent is StackPanel textBlockAndTime)
                {
                    int lastIndex = textBlockAndTime.Children.Count - 1;
                    var time = textBlockAndTime.Children[lastIndex];

                    if (time is StackPanel timeAndEdit)
                    {
                        if (timeAndEdit.Children.Count == 1)
                        {
                            int childrenIndex = timeAndEdit.Children.Count - 1;
                            var saveTime = timeAndEdit.Children[childrenIndex];
                            timeAndEdit.Children.Remove(timeAndEdit.Children[childrenIndex]);

                            timeAndEdit.Children.Add(editedText);
                            timeAndEdit.Children.Add(saveTime);
                        }
                    }
                }

                CloseEditMode();
                textBlockChange = null;
            }
        }

        private void CloseEditMode()
        {
            editTheMessageBox.IsVisible = false;
            chatTextBoxForEdit.IsVisible = false;
            editTheMessageButton.IsVisible = false;
            chatTextBox.Text = tempChatTextBox;
            chatTextBox.IsVisible = true;
        }

        private void MinimizeButton_Click(object? sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeButton_Click(object? sender, RoutedEventArgs e)
        {
            if (WindowState == WindowState.Maximized)
                WindowState = WindowState.Normal;
            else
                WindowState = WindowState.Maximized;
        }

        private void CloseButton_Click(object? sender, RoutedEventArgs e)
        {
            Close();
        }

        private void EmptyBar_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                BeginMoveDrag(e);
            }
        }

        private void answerTheMessage_ActualThemeVariantChanged(object? sender, System.EventArgs e)
        {
        }

        private void answerTheMessage_ActualThemeVariantChanged_1(object? sender, System.EventArgs e)
        {
        }

        private void EditMessageButton_ActualThemeVariantChanged(object? sender, System.EventArgs e)
        {
        }

        private void editTheMessageBox_ActualThemeVariantChanged(object? sender, System.EventArgs e)
        {
        }

        private void TextBlock_ActualThemeVariantChanged(object? sender, EventArgs e)
        {
        }
    }
}