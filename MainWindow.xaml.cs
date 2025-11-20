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

        public MainWindow()
        {
            InitializeComponent();

            _connection = new HubConnectionBuilder()
            .WithUrl("http://192.168.1.4:5191/chatHub")
            .WithAutomaticReconnect()
            .Build();

            _connection.On<string, string>("ReceiveMessage", (user, message) =>
            {
                Dispatcher.Invoke(() =>
                {
                    User2.Text = $"{user}: {message}";
                });
            });

            ConnectToServer();
        }

        private async void ConnectToServer()
        {
            try
            {
                await _connection.StartAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка подключения: {ex.Message}");
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
            await _connection.InvokeAsync("SendMessage",
                "Vasya",
                chatTextBox.Text);
        }
    }
}