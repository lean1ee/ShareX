using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using SkiaSharp;

namespace ShareX.Linux.Core.Helpers;

public static class WaylandClipboard
{
    public static async Task<bool> CopyImageAsync(SKBitmap bitmap)
    {
        try
        {
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            return await CopyImageBytesAsync(data.ToArray()).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            AppLogger.LogError("WaylandClipboard", $"CopyImageAsync error: {ex.Message}", ex);
            return false;
        }
    }

    public static Task<bool> CopyImageAsync(byte[] pngBytes) => CopyImageBytesAsync(pngBytes);

    public static async Task<bool> CopyImageBytesAsync(byte[] pngBytes)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "wl-copy",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardError = true
            };
            psi.ArgumentList.Add("-t");
            psi.ArgumentList.Add("image/png");

            using var process = Process.Start(psi);
            if (process == null) return false;

            await process.StandardInput.BaseStream.WriteAsync(pngBytes).ConfigureAwait(false);
            process.StandardInput.BaseStream.Close();

            await process.WaitForExitAsync().ConfigureAwait(false);
            return process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            AppLogger.LogError("WaylandClipboard", $"CopyImageBytesAsync error: {ex.Message}", ex);
            return false;
        }
    }

    public static async Task<bool> CopyTextAsync(string text)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "wl-copy",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(psi);
            if (process == null) return false;

            await process.StandardInput.WriteAsync(text).ConfigureAwait(false);
            process.StandardInput.Close();

            await process.WaitForExitAsync().ConfigureAwait(false);
            return process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            AppLogger.LogError("WaylandClipboard", $"CopyTextAsync error: {ex.Message}", ex);
            return false;
        }
    }

    public static async Task<byte[]?> GetImageBytesAsync()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "wl-paste",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            psi.ArgumentList.Add("-t");
            psi.ArgumentList.Add("image/png");

            using var process = Process.Start(psi);
            if (process == null) return null;

            using var ms = new MemoryStream();
            await process.StandardOutput.BaseStream.CopyToAsync(ms).ConfigureAwait(false);
            await process.WaitForExitAsync().ConfigureAwait(false);

            if (process.ExitCode == 0 && ms.Length > 0)
            {
                return ms.ToArray();
            }
        }
        catch (Exception ex)
        {
            AppLogger.LogWarn("WaylandClipboard", $"GetImageBytesAsync failed: {ex.Message}");
        }
        return null;
    }

    public static async Task<SKBitmap?> GetImageBitmapAsync()
    {
        var bytes = await GetImageBytesAsync().ConfigureAwait(false);
        if (bytes == null || bytes.Length == 0) return null;
        try
        {
            return SKBitmap.Decode(bytes);
        }
        catch (Exception ex)
        {
            AppLogger.LogWarn("WaylandClipboard", $"Failed to decode clipboard image: {ex.Message}");
            return null;
        }
    }

    public static async Task<string?> GetTextAsync()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "wl-paste",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            psi.ArgumentList.Add("-t");
            psi.ArgumentList.Add("text/plain");

            using var process = Process.Start(psi);
            if (process == null) return null;

            var text = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
            await process.WaitForExitAsync().ConfigureAwait(false);

            if (process.ExitCode == 0 && !string.IsNullOrEmpty(text))
            {
                return text;
            }
        }
        catch (Exception ex)
        {
            AppLogger.LogWarn("WaylandClipboard", $"GetTextAsync error: {ex.Message}");
        }
        return null;
    }

    public static async Task<bool> HasImageAsync()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "wl-paste",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            psi.ArgumentList.Add("-l");

            using var process = Process.Start(psi);
            if (process == null) return false;

            var types = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
            await process.WaitForExitAsync().ConfigureAwait(false);

            return process.ExitCode == 0 && (types.Contains("image/png") || types.Contains("image/jpeg") || types.Contains("image/bmp"));
        }
        catch
        {
            return false;
        }
    }
}
