using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using System.Threading;

namespace Uchat
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private TextBlock textBlockChange = new TextBlock();
        private string tempChatTextBox = "";

        private void SendButton_Click(object? sender, RoutedEventArgs e)
        {
            int messagesCount = ChatMessagesPanel.Children.Count;
            var message = chatTextBox.Text?.Trim();
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

                var bubble = new Border
                {
                    CornerRadius = new CornerRadius(5),
                    Padding = new Thickness(8),
                    Margin = new Thickness(5, 0, 5, 0),
                    Child = textBlock,
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
                    Header = "Reply",
                };

                menuItemReply.Click += (s, e) =>
                {
                    chatTextBoxForReplyAndEdit.IsVisible = false;
                    chatTextBox.IsVisible = true;
                    editTheMessageBox.IsVisible = false;
                    editTheMessageButton.IsVisible = false;
                    replyTheMessageBox.IsVisible = true;
                    replyTheMessage.Text = textBlock.Text;
                };
                contextMenu.Items.Add(menuItemReply);

                MenuItem menuItemEdit = new MenuItem
                {
                    Header = "Edit"
                };

                menuItemEdit.Click += (s, e) =>
                {
                    tempChatTextBox = chatTextBox.Text;

                    replyTheMessageBox.IsVisible = false;
                    chatTextBox.IsVisible = false;

                    editTheMessageBox.IsVisible = true;
                    chatTextBoxForReplyAndEdit.IsVisible = true;
                    editTheMessageButton.IsVisible = true;
                    editTheMessage.Text = textBlock.Text;
                    chatTextBoxForReplyAndEdit.Text = textBlock.Text;

                    textBlockChange = textBlock;
                };
                contextMenu.Items.Add(menuItemEdit);

                MenuItem menuItemCopy = new MenuItem
                {
                    Header = "Copy"
                };

                menuItemCopy.Click += (s, e) =>
                {
                    Clipboard.SetTextAsync(textBlock.Text);
                };
                contextMenu.Items.Add(menuItemCopy);

                MenuItem menuItemDelete = new MenuItem
                {
                    Header = "Delete",
                };

                menuItemDelete.Click += (s, e) =>
                {
                    editTheMessageBox.IsVisible = false;
                    replyTheMessageBox.IsVisible = false;
                    ChatMessagesPanel.Children.Remove(grid);
                };
                contextMenu.Items.Add(menuItemDelete);
            }
        }

        private void DontReplyTheMessage_Click(object? sender, RoutedEventArgs e)
        {
            replyTheMessageBox.IsVisible = false;
        }

        private void DontEditTheMessage_Click(object? sender, RoutedEventArgs e)
        {
            CloseEditMode();
        }

        private void EditMessageButton_Click(object? sender, RoutedEventArgs e)
        {
            if (textBlockChange != null)
            {
                textBlockChange.Text = chatTextBoxForReplyAndEdit.Text;
                CloseEditMode();
                textBlockChange = null;
            }
        }

        private void CloseEditMode()
        {
            editTheMessageBox.IsVisible = false;
            chatTextBoxForReplyAndEdit.IsVisible = false;
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
    }
}