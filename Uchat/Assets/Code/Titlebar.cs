using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Uchat
{
    public partial class MainWindow : Window
    {
        private void Options_Click(object sender, RoutedEventArgs e)
		{
			var newFriendRequest = new Chat.FriendRequest("Vetal");
            requestList.Children.Add(newFriendRequest.Box);

        }

        private void NotificationButton_Click(object sender, RoutedEventArgs e)
		{
			NotificationBox.IsVisible = !NotificationBox.IsVisible;

        }

        private void MinimizeButton_Click(object? sender, RoutedEventArgs e)
		{
			WindowState = WindowState.Minimized;
		}

		private void MaximizeButton_Click(object? sender, RoutedEventArgs e)
		{
			if (WindowState == WindowState.Maximized)
			{
				WindowState = WindowState.Normal;
				maximizeButton.Content = "\uE922";

            }
			else
			{
				WindowState = WindowState.Maximized;
				maximizeButton.Content = "\uE923";

            }
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