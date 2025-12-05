using System;
using System.IO;

namespace Uchat
{
    public static class Logger
    {
#if DEBUG
        private static string _logFilePath = string.Empty;
        private static readonly object _lock = new object();

        static Logger()
        {
            Initialize();
        }

        private static void Initialize()
        {
            try
            {
                var appDirectory = AppDomain.CurrentDomain.BaseDirectory;
                _logFilePath = Path.Combine(appDirectory, "client_log.txt");
                File.WriteAllText(_logFilePath, "");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to initialize logger: {ex.Message}");
                _logFilePath = "client_log.txt";
            }
        }

        private static void WriteInternal(string message)
        {
            try
            {
                var logEntry = $"[{DateTime.Now:HH:mm:ss.fff}] {message}\n";
                File.AppendAllText(_logFilePath, logEntry);
                Console.WriteLine(logEntry.TrimEnd());
            }
            catch { }
        }
#endif

        public static void Log(string message)
        {
#if DEBUG
            lock (_lock)
            {
                WriteInternal(message);
            }
#endif
        }
        
        public static void Error(string message, Exception? ex = null)
        {
            var logEntry = $"[ERROR] [{DateTime.Now}] {message}";
            if (ex != null)
            {
                logEntry += $"\n{ex.Message}\n{ex.StackTrace}";
            }
            
            try
            {
                File.AppendAllText("error_log.txt", logEntry + Environment.NewLine);
            }
            catch { }
        }
    }
}
