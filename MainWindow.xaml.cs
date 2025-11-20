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
                    MessageBox.Show("CLIENT GOT MESSAGE");
                    User1.Text = $"{user}: {message}";
                });
            });

            ConnectToServer();

            _connection.Closed += async (error) =>
            {
                MessageBox.Show($"CONNECTION CLOSED: {error}");
            };
        }

        private async void ConnectToServer()
        {
            try
            {
                await _connection.StartAsync();
                MessageBox.Show("CONNECTED");
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
            try
            {
                await _connection.InvokeAsync("SendMessage", "Vasya", chatTextBox.Text);
                MessageBox.Show("SEND OK");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"SEND ERROR:\n{ex}");
            }
        }
    }
}