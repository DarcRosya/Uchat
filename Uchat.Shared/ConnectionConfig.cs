using System;
using System.Linq;

namespace Uchat.Shared
{
    /// <summary>
    /// Централизованная конфигурация URL сервера
    /// Измените NgrokUrl после запуска ngrok
    /// </summary>
    public static class ConnectionConfig
    {
        private const string ngrokUrl = "https://unghostly-bunglingly-elli.ngrok-free.dev";
        private const string localUrl = $"http://localhost:";

        public static bool ValidServerArgs(string[] args)
        {
            if (args.Length < 1)
            {
                return false;
            }
            if (args.Contains("-kill") && args.Contains("-start"))
            {
                return false;
            }
            if (args.Contains("-start") && args.Length < 2)
            {
                return false;
            }
            if (args.Contains("-start") && !int.TryParse(args[^1], out _))
            {
                return false;
            }
            if (args.Contains("-kill") && args.Length > 1)
            {
                return false;
            }
            return true;
        }

        public static bool ValidArgs(string[] args)
        {
            if (args.Length < 1)
                return false;

            if (args[0] != "-local" && args[0] != "-ngrok")
                return false;

            if (args[0] == "-local")
            {
                if (args.Length != 2)
                    return false;

                if (!int.TryParse(args[1], out _))
                    return false;

                return true;
            }

            if (args[0] == "-ngrok")
            {
                return args.Length == 1;
            }

            return false;
        }


        private static bool IsLocalConnection(string[] args)
        {
            return args[0] == "-local";
        }

        public static string GetServerUrl(string[] args)
        {
            if (IsLocalConnection(args))
            {
                return localUrl + args[1];
            }
            else
            {
                return ngrokUrl;
            }
        }
    }
}