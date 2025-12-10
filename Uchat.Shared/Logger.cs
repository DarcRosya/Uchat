using System;
using System.IO;
using System.Threading;

namespace Uchat.Shared
{
    public static class Logger
    {
        private static string _logDirectory = string.Empty;
        private static string _mainLogFile = string.Empty;
        private static readonly object _lock = new object();

        static Logger()
        {
            Initialize();
        }

        private static void Initialize()
        {
            try
            {
                var currentPath = AppDomain.CurrentDomain.BaseDirectory;
                var dirInfo = new DirectoryInfo(currentPath);

                // --- LOGIC OF THE SEARCH FOR THE PATH ---
                string? targetDir = null;

                // 1. Attempt to find the Uchat.Server folder by moving up (for development mode)
                if (currentPath.Contains("bin"))
                {
                    var parent = dirInfo.Parent;
                    while (parent != null)
                    {
                        var potentialPath = Path.Combine(parent.FullName, "Uchat.Server");
                        if (Directory.Exists(potentialPath))
                        {
                            targetDir = potentialPath;
                            break;
                        }
                        parent = parent.Parent;
                    }
                }

                // 2. If not found (or if it is Release), use the current folder
                if (string.IsNullOrEmpty(targetDir))
                {
                    targetDir = currentPath;
                }

                _logDirectory = targetDir;
                _mainLogFile = Path.Combine(_logDirectory, "log.txt");

                // Create a file if it does not exist (check access rights)
                if (!File.Exists(_mainLogFile))
                {
                    try
                    {
                        File.WriteAllText(_mainLogFile, $"=== SHARED LOG STARTED AT {DateTime.Now} ===\n");
                    }
                    catch { /* Ignore if file is busy */ }
                }

                System.Diagnostics.Debug.WriteLine($"[Logger] Path set to: {_mainLogFile}");
            }
            catch (Exception ex)
            {
                // Fallback: пишемо поруч з exe, якщо щось зламалось
                _logDirectory = AppDomain.CurrentDomain.BaseDirectory;
                _mainLogFile = Path.Combine(_logDirectory, "log.txt");
                System.Diagnostics.Debug.WriteLine($"Logger Init Error: {ex.Message}");
            }
        }

        // Internal method that performs the “dirty work” of writing to a file
        private static void WriteInternal(string message)
        {
            // Визначаємо джерело (Client або Server) за назвою процесу
            string source = AppDomain.CurrentDomain.FriendlyName.Contains("Server") ? "SERVER" : "CLIENT";

            var logEntry = $"[{DateTime.Now:HH:mm:ss}] [{source}] {message}\n";

            // Duplicate in the Output window of the studio
            System.Diagnostics.Debug.Write(logEntry);
            Console.WriteLine(logEntry.TrimEnd());

            // Retry logic (5 attempts to save the file if it is busy with another process)
            for (int i = 0; i < 5; i++)
            {
                try
                {
                    File.AppendAllText(_mainLogFile, logEntry);
                    return; // Success
                }
                catch (IOException)
                {
                    Thread.Sleep(20); 
                }
                catch
                {
                    return; 
                }
            }
        }

        // Method 1: Write (separate method)
        public static void Write(string message)
        {
            lock (_lock)
            {
                WriteInternal(message);
            }
        }

        // Method 2: Log (now this is a separate method, not a mirror)
        public static void Log(string message)
        {
            lock (_lock)
            {
                WriteInternal(message);
            }
        }

        public static void Error(string message, Exception? ex = null)
        {
            string source = AppDomain.CurrentDomain.FriendlyName.Contains("Server") ? "SERVER" : "CLIENT";
            var logEntry = $"[ERROR] [{source}] [{DateTime.Now}] {message}";

            if (ex != null)
            {
                logEntry += $"\n{ex.Message}\n{ex.StackTrace}";
            }

            try
            {
                var errorFile = Path.Combine(_logDirectory, "error_log.txt");
                File.AppendAllText(errorFile, logEntry + Environment.NewLine);

                Console.WriteLine($"ERROR: {message}");
            }
            catch { }
        }
    }
}