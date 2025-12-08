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

                // --- ЛОГІКА ПОШУКУ ШЛЯХУ ---
                string? targetDir = null;

                // 1. Спроба знайти папку Uchat.Server, піднімаючись вгору (для режиму розробки)
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

                // 2. Якщо не знайшли (або це Release), використовуємо поточну папку
                if (string.IsNullOrEmpty(targetDir))
                {
                    targetDir = currentPath;
                }

                _logDirectory = targetDir;
                _mainLogFile = Path.Combine(_logDirectory, "log.txt");

                // Створюємо файл, якщо немає (перевірка прав доступу)
                if (!File.Exists(_mainLogFile))
                {
                    try
                    {
                        File.WriteAllText(_mainLogFile, $"=== SHARED LOG STARTED AT {DateTime.Now} ===\n");
                    }
                    catch { /* Ігноруємо, якщо файл зайнятий */ }
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

        // Внутрішній метод, який виконує "брудну роботу" із запису у файл
        private static void WriteInternal(string message)
        {
            // Визначаємо джерело (Client або Server) за назвою процесу
            string source = AppDomain.CurrentDomain.FriendlyName.Contains("Server") ? "SERVER" : "CLIENT";

            var logEntry = $"[{DateTime.Now:HH:mm:ss}] [{source}] {message}\n";

            // Дублюємо в Output вікно студії
            System.Diagnostics.Debug.Write(logEntry);
            Console.WriteLine(logEntry.TrimEnd());

            // Retry logic (5 спроб записати файл, якщо він зайнятий іншим процесом)
            for (int i = 0; i < 5; i++)
            {
                try
                {
                    File.AppendAllText(_mainLogFile, logEntry);
                    return; // Успіх
                }
                catch (IOException)
                {
                    Thread.Sleep(20); // Чекаємо і пробуємо знову
                }
                catch
                {
                    return; // Інша помилка - виходимо
                }
            }
        }

        // ==========================================
        // ПУБЛІЧНІ МЕТОДИ
        // ==========================================

        // Метод 1: Write (окремий метод)
        public static void Write(string message)
        {
            lock (_lock)
            {
                WriteInternal(message);
            }
        }

        // Метод 2: Log (тепер це окремий метод, не дзеркало)
        public static void Log(string message)
        {
            lock (_lock)
            {
                // Викликаємо ту саму внутрішню логіку запису, але метод незалежний.
                // Сюди можна додати, наприклад, префікс [INFO], якщо захочете в майбутньому.
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