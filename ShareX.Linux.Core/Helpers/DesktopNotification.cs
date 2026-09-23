using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace ShareX.Linux.Core.Helpers;

public static class DesktopNotification
{
    public static async Task ShowAsync(string title, string message, string? imagePath = null, (string id, string text)[]? actions = null)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "notify-send",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            psi.ArgumentList.Add("-a");
            psi.ArgumentList.Add("ShareX");

            if (!string.IsNullOrWhiteSpace(imagePath) && File.Exists(imagePath))
            {
                psi.ArgumentList.Add("-i");
                psi.ArgumentList.Add(imagePath);
            }

            if (actions != null && actions.Length > 0)
            {
                foreach (var action in actions)
                {
                    psi.ArgumentList.Add("--action");
                    psi.ArgumentList.Add($"{action.id}={action.text}");
                }
            }

            psi.ArgumentList.Add(title);
            psi.ArgumentList.Add(message);

            using var process = Process.Start(psi);
            if (process != null)
            {
                await process.WaitForExitAsync().ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[DesktopNotification] Error displaying notification: {ex.Message}");
        }
    }
}
