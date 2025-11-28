using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Microsoft.AspNetCore.SignalR.Client;
using System;

namespace Uchat
{
    public partial class MainWindow : Window
    {
        private HubConnection _connection;

        private TextBlock textBlockChange = new TextBlock();
        private string tempChatTextBox = "";
        
        private string currentChatId = "TestChat"; // FOR DEBUG PURPOSE ONLY!!!
        private string name = "Vadim";             // FOR DEBUG PURPOSE ONLY!!!
        public MainWindow()
        {
            InitializeComponent();
            ConnectToServer();
        }

        private async void ConnectToServer()
        {
            // Server connection
            _connection = new HubConnectionBuilder()
            .WithUrl("https://unghostly-bunglingly-elli.ngrok-free.dev/chatHub")
            //.WithAutomaticReconnect()
            .Build();

            _connection.On<string, string, string>("ReceiveMessage", (chatId, user, message) =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (chatId != currentChatId || string.IsNullOrEmpty(message))
                        return;

                    replyTheMessageBox.IsVisible = false;

                    var textBlock = new TextBlock
                    {
                        Name = "MessageTextBlock",
                        Text = $"{user}: {message}" ,
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

                    // Received message from other user
                    if (user != name)
                    {
                        bubble.Background = Brush.Parse("#264c6f");
                        bubble.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
                    }

                    ChatMessagesPanel.Children.Add(grid);
                    chatTextBox.Text = string.Empty;
                    ChatScrollViewer.ScrollToEnd();

                    // Create context menu
                    var contextMenu = new ContextMenu();
                    bubble.ContextMenu = contextMenu;

                    // Reply
                    var menuItemReply = new MenuItem { Header = "Reply" };
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

                    // Edit
                    var menuItemEdit = new MenuItem { Header = "Edit" };
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

                    // Copy
                    var menuItemCopy = new MenuItem { Header = "Copy" };
                    menuItemCopy.Click += (s, e) =>
                    {
                        Clipboard.SetTextAsync(textBlock.Text);
                    };
                    contextMenu.Items.Add(menuItemCopy);

                    // Delete
                    var menuItemDelete = new MenuItem { Header = "Delete" };
                    menuItemDelete.Click += (s, e) =>
                    {
                        editTheMessageBox.IsVisible = false;
                        replyTheMessageBox.IsVisible = false;

                        ChatMessagesPanel.Children.Remove(grid);
                    };
                    contextMenu.Items.Add(menuItemDelete);
                });
            });

            try
            {
                await _connection.StartAsync();
                await _connection.InvokeAsync("JoinGroup", currentChatId); //in futute use actual chat id
                await _connection.InvokeAsync("NewUserNotification", currentChatId, name); //in futute use actual chat id
            }
            catch (Exception ex)
            {
                Console.WriteLine($"CONNECTION ERROR: {ex.Message}");
            }
        }

        private async void SendButton_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                await _connection.InvokeAsync("SendMessage", currentChatId, name, chatTextBox.Text);
                chatTextBox.Text = "";
            }
            catch (Exception ex)
            {
                //MessageBox.Show($"SENDING ERROR");
                //ConnectToServer();
            }
        }
    }
}
