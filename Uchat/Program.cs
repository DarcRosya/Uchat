using System;
using Uchat.Shared;
using Avalonia;

namespace Uchat
{
    internal class Program
    {
        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static void Main(string[] args)
        {
            //if (!ConnectionConfig.ValidArgs(args))
            //{
            //    Console.WriteLine("Usage:\nUchat.exe -local port (four digits)\nor\nUchat.exe -ngrok");
            //    Environment.Exit(0);
            //}
            BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
        }

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();
    }
}
