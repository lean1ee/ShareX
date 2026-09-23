using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using SkiaSharp;

namespace ShareX.Linux.Capture.Backends;

public class PortalScreenCaptureBackend : IScreenCaptureBackend
{
    public string BackendName => "XDG Desktop Portal";

    public bool IsAvailable()
    {
        return File.Exists("/usr/bin/busctl") || File.Exists("/usr/bin/gdbus");
    }

    public async Task<SKBitmap?> CaptureFullScreenAsync()
    {
        return await CaptureViaPortalAsync(interactive: false).ConfigureAwait(false);
    }

    public async Task<SKBitmap?> CaptureRegionAsync(int x, int y, int width, int height)
    {
        // Portal screenshot captures full screen or interactive selection;
        // If specific coordinates are requested from a full screen, crop it:
        var full = await CaptureFullScreenAsync().ConfigureAwait(false);
        if (full == null) return null;

        var subset = new SKBitmap();
        var rect = new SKRectI(x, y, x + width, y + height);
        if (full.ExtractSubset(subset, rect))
        {
            return subset;
        }
        return full;
    }

    private static async Task<SKBitmap?> CaptureViaPortalAsync(bool interactive)
    {
        try
        {
            // Call Screenshot portal via gdbus or busctl
            var psi = new ProcessStartInfo
            {
                FileName = "gdbus",
                Arguments = $"call --session --dest org.freedesktop.portal.Desktop " +
                            $"--object-path /org/freedesktop/portal/desktop " +
                            $"--method org.freedesktop.portal.Screenshot.Screenshot " +
                            $"\"\" \"{{'interactive': <{(interactive ? "true" : "false")}>, 'modal': <false>}}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return null;

            var output = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
            await process.WaitForExitAsync().ConfigureAwait(false);

            // Response returns object path of request e.g. ('/org/freedesktop/portal/desktop/request/1_234/token',)
            // In portal flow, Response signal is emitted containing uri: "file:///path/to/screenshot.png"
            // For headless Wayland when grim is available, WlrScreenCaptureBackend is always preferred.
            return null;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[PortalScreenCaptureBackend] Exception: {ex.Message}");
            return null;
        }
    }
}
