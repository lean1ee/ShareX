using System;
using System.IO;
using System.Threading.Tasks;
using ShareX.Linux.Core.Helpers;
using ShareX.Linux.Core.History;
using ShareX.Linux.Core.Settings;
using ShareX.Linux.Core.Uploaders;
using SkiaSharp;

namespace ShareX.Linux.Core.Pipeline;

public class ProcessCaptureResult
{
    public string? SavedFilePath { get; set; }
    public string? UploadUrl { get; set; }
    public bool CopiedToClipboard { get; set; }
}

public static class CapturePipeline
{
    public static async Task<ProcessCaptureResult> ProcessAsync(SKBitmap bitmap, Action<string>? onOpenInEditor = null)
    {
        var settings = SettingsManager.Instance.Settings;
        var result = new ProcessCaptureResult();

        string? savedPath = null;
        string fileName = NameParser.GenerateFileName(settings.FileNamePattern, "png");

        AppLogger.LogInfo("CapturePipeline", $"Processing capture: size={bitmap.Width}x{bitmap.Height}, AfterCapture flags={settings.AfterCapture}");

        // 1. Save to File
        if (settings.AfterCapture.HasFlag(AfterCaptureTasks.SaveImageToFile))
        {
            try
            {
                var dir = settings.ScreenshotsDirectory;
                Directory.CreateDirectory(dir);
                savedPath = Path.Combine(dir, fileName);

                using var image = SKImage.FromBitmap(bitmap);
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                await using var stream = File.OpenWrite(savedPath);
                data.SaveTo(stream);
                result.SavedFilePath = savedPath;
                AppLogger.LogInfo("CapturePipeline", $"Saved screenshot to: {savedPath} ({new FileInfo(savedPath).Length} bytes)");
            }
            catch (Exception ex)
            {
                AppLogger.LogError("CapturePipeline", $"Failed to save screenshot: {ex.Message}");
            }
        }

        // 2. Copy to Clipboard
        if (settings.AfterCapture.HasFlag(AfterCaptureTasks.CopyImageToClipboard))
        {
            result.CopiedToClipboard = await WaylandClipboard.CopyImageAsync(bitmap).ConfigureAwait(false);
            AppLogger.LogInfo("CapturePipeline", $"Copied screenshot to Wayland clipboard: {result.CopiedToClipboard}");
        }

        // 3. Upload to Host
        string? uploadUrl = null;
        if (settings.AfterCapture.HasFlag(AfterCaptureTasks.UploadImageToHost))
        {
            AppLogger.LogInfo("CapturePipeline", $"Uploading screenshot to {settings.SelectedImageUploader}...");
            var uploadRes = await UploadManager.Instance.UploadImageAsync(bitmap, fileName).ConfigureAwait(false);
            if (uploadRes.Success)
            {
                uploadUrl = uploadRes.Url;
                result.UploadUrl = uploadUrl;
                AppLogger.LogInfo("CapturePipeline", $"Upload succeeded: {uploadUrl}");
            }
            else
            {
                AppLogger.LogWarn("CapturePipeline", $"Upload failed: {uploadRes.ErrorMessage}");
            }
        }

        // 4. Save to History
        if (!string.IsNullOrEmpty(savedPath))
        {
            var fileInfo = new FileInfo(savedPath);
            await HistoryManager.Instance.AddEntryAsync(new HistoryEntry
            {
                FileName = fileName,
                FilePath = savedPath,
                Url = uploadUrl,
                Type = "Image",
                FileSizeBytes = fileInfo.Exists ? fileInfo.Length : 0
            }).ConfigureAwait(false);
            AppLogger.LogInfo("CapturePipeline", "Recorded entry in SQLite history database.");
        }

        // 5. Notification
        if (settings.AfterCapture.HasFlag(AfterCaptureTasks.ShowNotification))
        {
            var msg = result.CopiedToClipboard ? "Captured and copied to clipboard." : "Screenshot captured.";
            if (!string.IsNullOrEmpty(savedPath))
            {
                msg += $"\nSaved: {Path.GetFileName(savedPath)}";
            }
            if (!string.IsNullOrEmpty(uploadUrl))
            {
                msg += $"\nURL: {uploadUrl}";
            }

            await DesktopNotification.ShowAsync(
                "ShareX",
                msg,
                savedPath
            ).ConfigureAwait(false);
            AppLogger.LogInfo("CapturePipeline", "Desktop notification dispatched.");
        }

        // 6. Open in Image Editor
        if (settings.AfterCapture.HasFlag(AfterCaptureTasks.OpenInImageEditor) && onOpenInEditor != null && !string.IsNullOrEmpty(savedPath))
        {
            AppLogger.LogInfo("CapturePipeline", "Opening saved screenshot in Image Editor...");
            onOpenInEditor(savedPath);
        }

        return result;
    }
}
