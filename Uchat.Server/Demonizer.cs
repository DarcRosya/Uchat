using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Uchat.Server
{
    public static class SelfDaemon
    {
        public static void RunDetached(string[] args)
        {
            var fileName = Process.GetCurrentProcess().MainModule!.FileName!;

            var arguments = string.Join(" ", args.Where(a => a != "-start"));

            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments, // new args
                UseShellExecute = false,
                CreateNoWindow = true, // true, if window isn't needed
                WindowStyle = ProcessWindowStyle.Normal
            };

            Console.WriteLine($"Starting detached process: {fileName} {arguments}");

            var process = Process.Start(startInfo);
            if (process != null)
            {
                Console.WriteLine($"Started process with ID: {process.Id}");
            }

            Environment.Exit(0);
        }

        public static void KillExisting()
        {
            var currentProcessId = Process.GetCurrentProcess().Id;
            var processes = Process.GetProcessesByName("Uchat.Server");
            foreach (var proc in processes)
            {
                if (proc.Id == currentProcessId) continue;

                try
                {
                    proc.Kill(true);
                    Console.WriteLine($"Killed process {proc.Id}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to kill process {proc.Id}: {ex.Message}");
                }
            }
        }
    }
}