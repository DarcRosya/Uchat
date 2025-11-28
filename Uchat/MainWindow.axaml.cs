using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
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