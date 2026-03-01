using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ScDownloader.Services
{
    public static class PlatformHelper
    {
        public static bool IsWindows { get; } = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        public static bool IsMacOS { get; } = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

        /// <summary>
        /// On macOS, sets executable permission and removes Gatekeeper quarantine attribute.
        /// No-op on Windows.
        /// </summary>
        public static void SetExecutablePermission(string filePath)
        {
            if (!IsMacOS) return;

            try
            {
                RunShell("chmod", $"+x \"{filePath}\"");
                RunShell("xattr", $"-cr \"{filePath}\"");
            }
            catch
            {
                // Best-effort — don't crash if chmod/xattr fails
            }
        }

        private static void RunShell(string command, string arguments)
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            process?.WaitForExit(5000);
        }
    }
}
