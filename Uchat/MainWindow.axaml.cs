using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace Uchat
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void SendButton_Click(object? sender, RoutedEventArgs e)
        {
            int messagesCount = ChatMessagesPanel.Children.Count;
            var message = chatTextBox.Text?.Trim();
            if (!string.IsNullOrEmpty(message))
            {
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
            }
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
    }
}