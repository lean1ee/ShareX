using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using SkiaSharp;

namespace ShareX.Linux.Core.Helpers;

public static class LinuxPrintHelper
{
    public static async Task<bool> PrintBitmapAsync(SKBitmap bitmap)
    {
        try
        {
            var pdfBytes = CreatePdfFromBitmap(bitmap);
            var tempPdf = Path.Combine(Path.GetTempPath(), $"sharex_print_{Guid.NewGuid():N}.pdf");
            await File.WriteAllBytesAsync(tempPdf, pdfBytes).ConfigureAwait(false);

            AppLogger.LogInfo("LinuxPrintHelper", $"Generated print PDF: {tempPdf} ({pdfBytes.Length} bytes)");

            // Try XDG Desktop Print Portal first via python Gio
            bool portalSuccess = await TryPrintViaPortalAsync(tempPdf).ConfigureAwait(false);
            if (portalSuccess)
            {
                AppLogger.LogInfo("LinuxPrintHelper", "Print dialog launched successfully via Desktop Portal.");
                await DesktopNotification.ShowAsync("ShareX", "Opened system print dialog.");
                return true;
            }

            // Fallback: system print/viewer via xdg-open
            AppLogger.LogWarn("LinuxPrintHelper", "Portal print failed or unavailable. Falling back to xdg-open.");
            var psi = new ProcessStartInfo
            {
                FileName = "xdg-open",
                Arguments = $"\"{tempPdf}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            await DesktopNotification.ShowAsync("ShareX", "Opened print document in viewer.");
            return true;
        }
        catch (Exception ex)
        {
            AppLogger.LogError("LinuxPrintHelper", $"PrintBitmapAsync error: {ex.Message}", ex);
            await DesktopNotification.ShowAsync("ShareX Print Error", ex.Message);
            return false;
        }
    }

    public static byte[] CreatePdfFromBitmap(SKBitmap bitmap)
    {
        using var ms = new MemoryStream();
        using var doc = SKDocument.CreatePdf(ms);
        using var page = doc.BeginPage(bitmap.Width, bitmap.Height);
        page.DrawBitmap(bitmap, 0, 0);
        doc.EndPage();
        doc.Close();
        return ms.ToArray();
    }

    private static async Task<bool> TryPrintViaPortalAsync(string pdfPath)
    {
        try
        {
            var pythonScript = @"
import gi, sys, os
from gi.repository import Gio, GLib

pdf_path = sys.argv[1]
if not os.path.exists(pdf_path):
    sys.exit(1)

bus = Gio.bus_get_sync(Gio.BusType.SESSION, None)
fd = os.open(pdf_path, os.O_RDONLY)
fd_list = Gio.UnixFDList.new()
fd_idx = fd_list.append(fd)
os.close(fd)

params = GLib.Variant('(ssha{sv})', ('', 'ShareX Print', fd_idx, {}))
msg = Gio.DBusMessage.new_method_call(
    'org.freedesktop.portal.Desktop',
    '/org/freedesktop/portal/desktop',
    'org.freedesktop.portal.Print',
    'Print'
)
msg.set_body(params)
msg.set_unix_fd_list(fd_list)

res, _ = bus.send_message_with_reply_sync(msg, Gio.DBusSendMessageFlags.NONE, 5000, None)
if res.get_message_type() == Gio.DBusMessageType.METHOD_RETURN:
    sys.exit(0)
else:
    sys.exit(2)
";
            var psi = new ProcessStartInfo
            {
                FileName = "python3",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true
            };
            psi.ArgumentList.Add("-c");
            psi.ArgumentList.Add(pythonScript);
            psi.ArgumentList.Add(pdfPath);

            using var process = Process.Start(psi);
            if (process == null) return false;

            await process.WaitForExitAsync().ConfigureAwait(false);
            return process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            AppLogger.LogWarn("LinuxPrintHelper", $"TryPrintViaPortalAsync error: {ex.Message}");
            return false;
        }
    }
}
