using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Uchat.Server
{
    public static class SelfDaemon
    {
        public static void RunDetached(string[] args)
        {
            var fileName = Process.GetCurrentProcess().MainModule!.FileName!;
            var workingDir = Path.GetDirectoryName(fileName);

            var arguments = string.Join(" ", args.Where(a => a != "-start"));

            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments, // new args
                UseShellExecute = false,
                CreateNoWindow = true, // true, if window isn't needed
                WindowStyle = ProcessWindowStyle.Normal,

                WorkingDirectory = workingDir
            };

            Logger.Write($"[Daemon] Starting detached process: {fileName} {arguments}");

            try
            {
                var process = Process.Start(startInfo);
                if (process != null)
                {
                    Logger.Write($"[Daemon] Started process with ID: {process.Id}");
                }
                else
                {
                    Logger.Write($"[Daemon] ERROR: Process.Start returned null.");
                }
            }
            catch (Exception ex)
            {
                Logger.Write($"[Daemon] CRITICAL ERROR starting process: {ex.Message}");
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
                    Logger.Write($"[Daemon] Killed process {proc.Id}");
                }
                catch (Exception ex)
                {
                    Logger.Write($"[Daemon] Failed to kill process {proc.Id}: {ex.Message}");
                }
            }
        }
    }
}