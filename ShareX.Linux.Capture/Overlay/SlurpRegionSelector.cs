using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace ShareX.Linux.Capture.Overlay;

public static class SlurpRegionSelector
{
    public static bool IsAvailable() => File.Exists("/usr/bin/slurp");

    public static async Task<string?> SelectRegionAsync()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "slurp",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            psi.ArgumentList.Add("-d");               // Show dimensions
            psi.ArgumentList.Add("-b");               // Background mask
            psi.ArgumentList.Add("#00000055");
            psi.ArgumentList.Add("-c");               // Border color
            psi.ArgumentList.Add("#00A4EF");
            psi.ArgumentList.Add("-s");               // Selection box fill
            psi.ArgumentList.Add("#00A4EF22");
            psi.ArgumentList.Add("-w");               // Border weight
            psi.ArgumentList.Add("2");

            using var process = Process.Start(psi);
            if (process == null) return null;

            var output = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
            await process.WaitForExitAsync().ConfigureAwait(false);

            if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
            {
                return null; // Cancelled with Esc or right click
            }

            return output.Trim(); // e.g. "100,200 400x300"
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[SlurpRegionSelector] Exception: {ex.Message}");
            return null;
        }
    }
}
