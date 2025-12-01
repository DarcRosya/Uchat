using Avalonia.Controls;

namespace Uchat
{

	public partial class MainWindow : Window
	{
		public class Chat
		{
			static int counter = 0;
			public class Message
			{
				private int id;
				private string content;
				private string time;
				private bool isEdited;
				private bool isAnswer;

				private Border messageBorder = new Border();
				private StackPanel messageStackPanel = new StackPanel();
				private TextBlock contentTextBlock = new TextBlock();
				private StackPanel timeStackPanel = new StackPanel();
				private TextBlock timeTextBlock = new TextBlock();

				public Message(string text, string timestamp, string type)
				{
					id = counter++;
					content = text;
					time = timestamp;
					isEdited = false;
					isAnswer = false;
					if (type == "received")
					{
						messageBorder.Classes.Add("messageBorder");
					}
					else
					{
                        messageBorder.Classes.Add("guestMessageBorder");

                    }
						messageBorder.Child = messageStackPanel;

					timeStackPanel.Classes.Add("timeStackPanel");
					timeStackPanel.Children.Add(timeTextBlock);

					timeTextBlock.Classes.Add("timeTextBlock");
					timeTextBlock.Text = time;

                    contentTextBlock.Classes.Add("chatMessage");
					contentTextBlock.Text = content;


                    messageStackPanel.Children.Add(contentTextBlock);
                    messageStackPanel.Children.Add(timeStackPanel);

                }

				public int Id { get { return id; } }
				public string Content { get { return content; } }
				public string Time { get { return time; } }
				public bool IsEdited { get { return isEdited; } set { isEdited = value; } }
				public bool IsAnswer { get { return isAnswer; } set { isAnswer = value; } }
				public Border Render { get { return messageBorder; } } 
			}
		}
	}
}