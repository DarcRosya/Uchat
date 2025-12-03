namespace Uchat.Server
{
    public static class Logger
    {
        private static string _logFilePath;
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
                _logFilePath = Path.Combine(appDirectory, "log.txt");

                WriteInternal($"Logger initialized at: {_logFilePath}");
                WriteInternal($"Current directory: {Directory.GetCurrentDirectory()}");
                WriteInternal($"App base directory: {appDirectory}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to initialize logger: {ex.Message}");
                _logFilePath = "log.txt"; // fallback
            }
        }

        private static void WriteInternal(string message)
        {
            try
            {
                var logEntry = $"[{DateTime.Now:HH:mm:ss}] {message}\n";
                File.AppendAllText(_logFilePath, logEntry);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to write to log: {ex.Message}");
            }
        }

        public static void Write(string message)
        {
            lock (_lock)
            {
                WriteInternal(message);
            }
        }
    }
}