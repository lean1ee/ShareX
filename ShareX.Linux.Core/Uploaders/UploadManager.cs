using System;
using System.IO;
using System.Threading.Tasks;
using ShareX.Linux.Core.Helpers;
using ShareX.Linux.Core.History;
using ShareX.Linux.Core.Settings;
using SkiaSharp;

namespace ShareX.Linux.Core.Uploaders;

public class UploadManager
{
    private static readonly Lazy<UploadManager> _instance = new(() => new UploadManager());
    public static UploadManager Instance => _instance.Value;

    public IUploadService GetActiveUploader()
    {
        var settings = SettingsManager.Instance.Settings;
        if (!string.IsNullOrEmpty(settings.CustomUploaderConfigPath) && File.Exists(settings.CustomUploaderConfigPath))
        {
            try
            {
                return CustomUploader.FromFile(settings.CustomUploaderConfigPath);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[UploadManager] Failed to load custom uploader: {ex.Message}");
            }
        }

        return new ImgurUploader(settings.ImgurClientId);
    }

    public async Task<UploadResult> UploadImageAsync(SKBitmap bitmap, string fileName)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = data.AsStream();

        var uploader = GetActiveUploader();
        AppLogger.LogInfo("UploadManager", $"Uploading '{fileName}' ({data.Size} bytes) via {uploader.ServiceName}...");
        var result = await uploader.UploadAsync(stream, fileName).ConfigureAwait(false);

        if (result.Success && !string.IsNullOrEmpty(result.Url))
        {
            AppLogger.LogInfo("UploadManager", $"Successfully uploaded to {uploader.ServiceName}: {result.Url}");
            var settings = SettingsManager.Instance.Settings;
            if (settings.AfterUpload.HasFlag(AfterUploadTasks.CopyUrlToClipboard))
            {
                await WaylandClipboard.CopyTextAsync(result.Url).ConfigureAwait(false);
            }

            if (settings.AfterUpload.HasFlag(AfterUploadTasks.ShowNotification))
            {
                await DesktopNotification.ShowAsync(
                    "ShareX - Upload Complete",
                    $"Uploaded to {uploader.ServiceName}:\n{result.Url} (Copied to clipboard)"
                ).ConfigureAwait(false);
            }
        }
        else
        {
            var err = result.ErrorMessage ?? "Unknown error";
            AppLogger.LogError("UploadManager", $"Upload failed with {uploader.ServiceName}: {err}");

            if (err.Contains("429") || err.Contains("Too Many Requests") || err.Contains("over capacity"))
            {
                await DesktopNotification.ShowAsync(
                    "ShareX - Imgur Rate Limit Reached",
                    "Imgur anonymous quota exceeded (HTTP 429).\nConfigure a custom SXCU or client ID in Settings."
                ).ConfigureAwait(false);
            }
            else
            {
                await DesktopNotification.ShowAsync(
                    "ShareX - Upload Failed",
                    $"Failed to upload: {err}"
                ).ConfigureAwait(false);
            }
        }

        return result;
    }
}
