using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Uchat.Server
{
    public static class SelfDaemon
    {
        public static void RunDetached(string[] args)
        {
            var fileName = Process.GetCurrentProcess().MainModule.FileName;
            var port = args.Last();

            Console.WriteLine($"Starting server on port {port}...");

            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = port,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            try
            {
                var process = Process.Start(startInfo);
                Console.WriteLine($"Server started with PID: {process.Id}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        public static void KillExisting()
        {
            var myId = Process.GetCurrentProcess().Id;
            var processes = Process.GetProcessesByName("Uchat.Server");

            foreach (var proc in processes)
            {
                if (proc.Id == myId)
                {
                    continue;
                }

                try
                {
                    proc.Kill();
                    Console.WriteLine($"Killed PID: {proc.Id}");
                }
                catch
                {
                    Console.WriteLine($"Failed to kill PID: {proc.Id}");
                }
            }

            Console.WriteLine("Killed all server instances");
        }
    }
}