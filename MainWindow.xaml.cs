using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using Microsoft.AspNetCore.SignalR.Client;

namespace Uchat
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private HubConnection _connection;
        private string currentChatId = "TestChat"; // FOR DEBUG PURPOSE ONLY!!!
        private string name = "Vadim"; // FOR DEBUG PURPOSE ONLY!!!

        public MainWindow()
        {
            InitializeComponent();
            
            ConnectToServer();
        }

        private async void ConnectToServer()
        {
            // Server connection
            _connection = new HubConnectionBuilder()
                .WithUrl($"https://unghostly-bunglingly-elli.ngrok-free.dev/chatHub")
                //.WithAutomaticReconnect()
                .Build();

            _connection.On<string, string, string>("ReceiveMessage", (chatId, user, message) =>
            {
                Dispatcher.Invoke(() =>
                {
                    if (chatId == currentChatId)
                        User1.Text += $"{user}: {message}\n";
                });
            });

            try
            {   
                await _connection.StartAsync();
                // successful connection
                MessageBox.Show("CONNECTED");

                // joining chat
                await _connection.InvokeAsync("JoinGroup", currentChatId);

                // New user notification
                await _connection.InvokeAsync("NewUserNotification", currentChatId, name);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"CONNECTION ERROR: {ex.Message}");
            }
        }

        private void closeButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }


        private void minimizeButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void maximizeButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.WindowState == WindowState.Maximized)
            {
                this.WindowState = WindowState.Normal;
                //restore logo
                maximizeButton.Content = "\uE922";
            }
            else
            {
                this.WindowState = WindowState.Maximized;
                //maximize logo
                maximizeButton.Content = "\uE923";
            }
        }

        private async void sendButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await _connection.InvokeAsync("SendMessage", currentChatId, name, chatTextBox.Text);
                chatTextBox.Text = "";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"SENDING ERROR");
                ConnectToServer();
            }
        }
    }
}