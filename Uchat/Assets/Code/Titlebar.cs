using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using System;
using Uchat.Shared;

namespace Uchat
{
    public partial class MainWindow : Window
    {
        private void LogOutButton_Click(object sender, RoutedEventArgs e)
		{
			// UNCOMMENT TO ADD LOGOUT BUTTON:
			LogoutAsync();
			NotificationBox.IsVisible = false;
			LoginProgram.IsVisible = true;
        }
        
        private async void LogoutAsync()
        {
			try
			{
				var authService = new Services.AuthApiService(systemArgs);
				await authService.LogoutAsync(
					Services.UserSession.Instance.AccessToken, 
					Services.UserSession.Instance.RefreshToken
				);
			}
			catch (Exception ex)
			{
				Logger.Error("Logout API call failed", ex);
			}
			
			Services.UserSession.Instance.Clear();
			SwitchToLoginView();
        }
        
        private void SwitchToLoginView()
        {
			MainProgram.IsVisible = false;
			loginForm.IsVisible = true;
			
			// Clear input fields
			usernameTextBox.Text = string.Empty;
			passwordTextBox.Text = string.Empty;
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