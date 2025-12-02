using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Uchat
{
    public partial class MainWindow : Window
    {
		private void MinimizeButton_Click(object? sender, RoutedEventArgs e)
		{
			WindowState = WindowState.Minimized;
		}

		private void MaximizeButton_Click(object? sender, RoutedEventArgs e)
		{
			if (WindowState == WindowState.Maximized)
			{
				WindowState = WindowState.Normal;
			}
			else
			{
				WindowState = WindowState.Maximized;
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