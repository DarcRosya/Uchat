using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Microsoft.AspNetCore.SignalR.Client;
using System;


namespace Uchat
{
	public partial class MainWindow : Window
	{
		public partial class Chat
		{
			//static int notificationCount = 0;

			public class FriendRequest
			{
				private string username = string.Empty;

				private Grid friendRequestGrid = new Grid();
				private TextBlock usernameTextBlock = new TextBlock();
				private Button acceptRequestButton = new Button();
				private Button rejectRequestButton = new Button();

				public FriendRequest(string newUsername)
				{
					username = newUsername;

					friendRequestGrid.Classes.Add("friendRequestBox");
					friendRequestGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
					friendRequestGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(50)));
					friendRequestGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(50)));

					usernameTextBlock.Classes.Add("friendRequestUsername");
					usernameTextBlock.Text = username;

					acceptRequestButton.Classes.Add("acceptRequestButton");
					acceptRequestButton.Content = "+";
                    acceptRequestButton.Click += AcceptRequestButton_Click;

					rejectRequestButton.Classes.Add("rejectRequestButton");
                    rejectRequestButton.Content = "-";
                    rejectRequestButton.Click += RejectRequestButton_Click;

                    friendRequestGrid.Children.Add(usernameTextBlock);
					friendRequestGrid.Children.Add(acceptRequestButton);
					friendRequestGrid.Children.Add(rejectRequestButton);

					Grid.SetColumn(usernameTextBlock, 0);
					Grid.SetColumn(acceptRequestButton, 1);
					Grid.SetColumn(rejectRequestButton, 2);
				}

				public Grid Box { get { return friendRequestGrid; } }
                public string Username { get { return usernameTextBlock.Text; } set { usernameTextBlock.Text = value; } }

                private void AcceptRequestButton_Click(object sender, EventArgs e)
				{

				}

                private void RejectRequestButton_Click(object sender, EventArgs e)
                {

                }

            }
		}
	}
}
