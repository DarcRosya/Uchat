using System;
using Avalonia;
using LoginFormAvalonia.Services;

namespace Uchat
{
    internal class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            // Парсим аргументы командной строки для получения токена
            ParseCommandLineArgs(args);
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        
        private static void ParseCommandLineArgs(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--token" && i + 1 < args.Length)
                {
                    UserSession.Instance.AccessToken = args[i + 1];
                    i++;
                }
                else if (args[i] == "--username" && i + 1 < args.Length)
                {
                    UserSession.Instance.Username = args[i + 1];
                    i++;
                }
                else if (args[i] == "--userId" && i + 1 < args.Length)
                {
                    if (int.TryParse(args[i + 1], out int userId))
                    {
                        UserSession.Instance.UserId = userId;
                    }
                    i++;
                }
            }
        }

        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();
    }
}
