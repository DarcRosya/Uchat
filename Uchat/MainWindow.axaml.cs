using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System;
using System.Threading;
using static System.Net.Mime.MediaTypeNames;

namespace Uchat
{
    public partial class MainWindow : Window
    {
        private bool isWindowFocused = false;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void MainWindow_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Back)
            {
                if (!string.IsNullOrEmpty(chatTextBox.Text))
                {
                    chatTextBox.Text = chatTextBox.Text.Substring(0, chatTextBox.Text.Length - 1);
                }
            }
            else if (e.Key == Key.Enter)
            {
                //SendMessage();
            }
            else
            {
                chatTextBox.Focus();
            }
        }
    }
}