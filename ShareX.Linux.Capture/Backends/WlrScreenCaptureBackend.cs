using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using SkiaSharp;

namespace ShareX.Linux.Capture.Backends;

public class WlrScreenCaptureBackend : IScreenCaptureBackend
{
    public string BackendName => "wlr-screencopy (grim)";

    public bool IsAvailable()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "which",
                Arguments = "grim",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            proc?.WaitForExit();
            return proc?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public async Task<SKBitmap?> CaptureFullScreenAsync()
    {
        return await ExecuteGrimCaptureAsync(null).ConfigureAwait(false);
    }

    public async Task<SKBitmap?> CaptureRegionAsync(int x, int y, int width, int height)
    {
        var geometry = $"{x},{y} {width}x{height}";
        return await ExecuteGrimCaptureAsync(geometry).ConfigureAwait(false);
    }

    public static async Task<SKBitmap?> ExecuteGrimCaptureAsync(string? geometry)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "grim",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            psi.ArgumentList.Add("-t");
            psi.ArgumentList.Add("png");

            if (!string.IsNullOrWhiteSpace(geometry))
            {
                psi.ArgumentList.Add("-g");
                psi.ArgumentList.Add(geometry);
            }

            // Output to stdout
            psi.ArgumentList.Add("-");

            using var process = Process.Start(psi);
            if (process == null) return null;

            using var ms = new MemoryStream();
            await process.StandardOutput.BaseStream.CopyToAsync(ms).ConfigureAwait(false);
            await process.WaitForExitAsync().ConfigureAwait(false);

            if (process.ExitCode != 0 || ms.Length == 0)
            {
                var error = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
                Console.Error.WriteLine($"[WlrScreenCaptureBackend] grim failed ({process.ExitCode}): {error}");
                return null;
            }

            ms.Position = 0;
            return SKBitmap.Decode(ms);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[WlrScreenCaptureBackend] grim exception: {ex.Message}");
            return null;
        }
    }
}
