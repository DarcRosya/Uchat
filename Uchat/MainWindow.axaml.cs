using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Threading;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Uchat
{
    public partial class MainWindow : Window
    {   
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

        private void addFriend_Click(object? sender, RoutedEventArgs e)
        {
            var contactGrid = new Grid
            {
                Background = Brush.Parse("#171a20"),
                ColumnDefinitions =
                {
                    new ColumnDefinition(new GridLength(50, GridUnitType.Pixel)),
                    new ColumnDefinition(new GridLength(1, GridUnitType.Star))
                }
            };

            var uri = new Uri("avares://Uchat/Assets/Icons/avatar.png");
            var avatarIcon = new Image
            {
                Source = new Bitmap(AssetLoader.Open(uri)),
                Stretch = Stretch.UniformToFill
            };

            var contactPanel = new StackPanel
            {
                Height = 50
            };

            var contactName = new TextBlock
            {
                Text = "John Cena",
                Foreground = Brush.Parse("#ffffff"),
                FontSize = 15,
                Margin = new Thickness(5,15,0,0),
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
            };

            var lastMessage = new TextBlock
            {
                Text = "Do you see me?",
                Foreground = Brush.Parse("#999999"),
                FontSize = 10,
                Margin = new Thickness(5,0,0,0),
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Bottom
            };
            contactPanel.Children.Add(contactName);
            contactPanel.Children.Add(lastMessage);

            contactGrid.Children.Add(avatarIcon);

            contactGrid.Children.Add(contactPanel);

            Grid.SetColumn(avatarIcon, 0);
            Grid.SetColumn(contactPanel, 1);

            contanctList.Children.Add(contactGrid);
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