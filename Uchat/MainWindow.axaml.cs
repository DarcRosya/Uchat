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

        private void SendButton_Click(object? sender, RoutedEventArgs e)
        {
            int messagesCount = ChatMessagesPanel.Children.Count;
            var message = chatTextBox.Text?.Trim();
            if (!string.IsNullOrEmpty(message))
            {
                answerTheMessageBox.IsVisible = false;

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
                    Background = Brushes.Green,
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
                    bubble.Background = Brush.Parse("#FF1A9FFF");
                    bubble.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
                }

                ChatMessagesPanel.Children.Add(grid);
                chatTextBox.Text = string.Empty;
                ChatScrollViewer.ScrollToEnd();

                var contextMenu = new ContextMenu();
                bubble.ContextMenu = contextMenu;

                MenuItem menuItemAnswer = new MenuItem
                {
                    Header = "Answer",
                };

                menuItemAnswer.Click += (s, e) =>
                {
                    if(answerTheMessageBox.IsVisible)
                    {
                        chatTextBox.Text = "";
                    }    
                    changeAnswerBox.IsVisible = false;
                    answerTheMessageBox.IsVisible = true;
                    answerTheMessage.Text = textBlock.Text;
                    dontAnswerTheMessage.Click += (s, e) => answerTheMessageBox.IsVisible = false;
                };
                contextMenu.Items.Add(menuItemAnswer);

                MenuItem menuItemChange = new MenuItem
                {
                    Header = "Change the text",
                };

                menuItemChange.Click += (s, e) =>
                {
                    textBlockChange = textBlock;
                    chatTextBox.Text = textBlockChange.Text;
                    changeAnswerBox.IsVisible = true;
                    answerTheMessageBox.IsVisible = true;
                    answerTheMessage.Text = textBlockChange.Text;

                    dontAnswerTheMessage.Click += (s, e) =>
                    {
                        answerTheMessageBox.IsVisible = false;
                        chatTextBox.Text = "";
                    };
                };
                contextMenu.Items.Add(menuItemChange);

                MenuItem menuItemCopy = new MenuItem
                {
                    Header = "Copy",
                };

                menuItemCopy.Click += (s, e) => Clipboard.SetTextAsync(message);
                contextMenu.Items.Add(menuItemCopy);

                MenuItem menuItemDelete = new MenuItem
                {
                    Header = "Delete",
                };
                menuItemDelete.Click += (s, e) => ChatMessagesPanel.Children.Remove(grid);
                contextMenu.Items.Add(menuItemDelete);
            }
        }

        private void changeAnswer_Click(object? sender, RoutedEventArgs e)
        {
            if (textBlockChange != null)
            {
                textBlockChange.Text = chatTextBox.Text;
                answerTheMessageBox.IsVisible = false;
                chatTextBox.Text = "";
                textBlockChange = null;
            }

            changeAnswerBox.IsVisible = false;
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
    }
}