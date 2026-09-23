using System;
using System.IO;
using Newtonsoft.Json;
using ShareX.Linux.Core.Helpers;

namespace ShareX.Linux.Core.Settings;

[Flags]
public enum AfterCaptureTasks
{
    None = 0,
    CopyImageToClipboard = 1 << 0,
    SaveImageToFile = 1 << 1,
    OpenInImageEditor = 1 << 2,
    UploadImageToHost = 1 << 3,
    ShowNotification = 1 << 4,
    PlaySound = 1 << 5
}

[Flags]
public enum AfterUploadTasks
{
    None = 0,
    CopyUrlToClipboard = 1 << 0,
    ShowNotification = 1 << 1,
    OpenUrlInBrowser = 1 << 2
}

public enum ImageFormat
{
    PNG,
    JPEG,
    WebP
}

public class ShareXSettings
{
    public string ScreenshotsDirectory { get; set; } = PathsHelper.DefaultScreenshotsDir;
    public string FileNamePattern { get; set; } = "%y-%mo-%d_%h-%mi-%s";
    public ImageFormat ImageFormat { get; set; } = ImageFormat.PNG;
    public int JpegQuality { get; set; } = 90;

    public AfterCaptureTasks AfterCapture { get; set; } =
        AfterCaptureTasks.CopyImageToClipboard |
        AfterCaptureTasks.SaveImageToFile |
        AfterCaptureTasks.ShowNotification;

    public AfterUploadTasks AfterUpload { get; set; } =
        AfterUploadTasks.CopyUrlToClipboard |
        AfterUploadTasks.ShowNotification;

    public string SelectedImageUploader { get; set; } = "Imgur";
    public string? ImgurClientId { get; set; } = null; // null uses default ShareX / anonymous client id

    public string? CustomUploaderConfigPath { get; set; } = null;

    // Recording settings
    public int RecordingFps { get; set; } = 30;
    public bool RecordAudio { get; set; } = false;
    public string VideoFormat { get; set; } = "mp4"; // mp4, gif, webm

    // Wayland capture preferences
    public bool UseWlrScreencopyFirst { get; set; } = true;
    public bool ShowMagnifierOnOverlay { get; set; } = true;
    public bool ShowColorPickerOnOverlay { get; set; } = true;
    public bool FreezeScreenOnRegionCapture { get; set; } = true;

    // Theming preferences
    public string ThemeMode { get; set; } = "Auto"; // "Auto", "Kanagawa", "Catppuccin Mocha", "Catppuccin Latte", "Tokyo Night", "Nord", "ShareX Dark", "ShareX Light"
    public bool UseSystemAccentColor { get; set; } = true;
}

public class SettingsManager
{
    private static readonly Lazy<SettingsManager> _instance = new(() => new SettingsManager());
    public static SettingsManager Instance => _instance.Value;

    public ShareXSettings Settings { get; private set; }

    public SettingsManager()
    {
        Settings = Load();
    }

    public ShareXSettings Load()
    {
        try
        {
            var path = PathsHelper.SettingsFilePath;
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var loaded = JsonConvert.DeserializeObject<ShareXSettings>(json);
                if (loaded != null) return loaded;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[SettingsManager] Failed to load settings: {ex.Message}");
        }

        return new ShareXSettings();
    }

    public void Save()
    {
        try
        {
            var path = PathsHelper.SettingsFilePath;
            var json = JsonConvert.SerializeObject(Settings, Formatting.Indented);
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[SettingsManager] Failed to save settings: {ex.Message}");
        }
    }
}
