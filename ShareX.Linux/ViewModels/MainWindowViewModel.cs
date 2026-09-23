using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShareX.Linux.Core.Helpers;
using ShareX.Linux.Core.History;
using ShareX.Linux.Core.Settings;
using ShareX.Linux.Core.Uploaders;
using ShareX.AvaloniaUI.Theming;
using ShareX.Linux.Services;

namespace ShareX.Linux.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private string _currentTab = "Dashboard";

    [ObservableProperty]
    private ObservableCollection<HistoryEntry> _historyItems = new();

    // Theming
    public ObservableCollection<string> ThemeModes { get; } = new()
    {
        "Auto",
        "Kanagawa",
        "Catppuccin Mocha",
        "Catppuccin Latte",
        "Tokyo Night",
        "Nord",
        "ShareX Dark",
        "ShareX Light"
    };

    [ObservableProperty]
    private string _selectedThemeMode = "Auto";

    [ObservableProperty]
    private string _themeStatus = "Auto (Following Desktop)";

    // Settings proxies
    [ObservableProperty]
    private string _screenshotsDirectory = string.Empty;

    [ObservableProperty]
    private string _fileNamePattern = string.Empty;

    [ObservableProperty]
    private bool _copyImageToClipboard;

    [ObservableProperty]
    private bool _saveImageToFile;

    [ObservableProperty]
    private bool _openInEditor;

    [ObservableProperty]
    private bool _uploadToHost;

    [ObservableProperty]
    private bool _showNotification;

    [ObservableProperty]
    private string _selectedUploader = "Imgur";

    [ObservableProperty]
    private string? _customSxcuPath;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    public string NiriConfigSnippet =>
@"// Keybinds (add to ~/.config/niri/cfg/keybinds.kdl):
binds {
    Print       repeat=false hotkey-overlay-title=""ShareX: Capture Region""        { spawn ""sharex"" ""--capture-region""; }
    Mod+Shift+S repeat=false hotkey-overlay-title=""ShareX: Capture Region & Edit"" { spawn ""sharex"" ""--capture-region-edit""; }
    Shift+Print repeat=false hotkey-overlay-title=""ShareX: Capture Region & Edit"" { spawn ""sharex"" ""--capture-region-edit""; }
    Ctrl+Print  repeat=false hotkey-overlay-title=""ShareX: Capture Full Screen""   { spawn ""sharex"" ""--capture-screen""; }
    Alt+Print   repeat=false hotkey-overlay-title=""ShareX: Record Region (Video)"" { spawn ""sharex"" ""--record-region""; }
    Mod+Print   repeat=false hotkey-overlay-title=""ShareX: Record Region (GIF)""   { spawn ""sharex"" ""--record-gif""; }
}

// Window Rules (add to ~/.config/niri/cfg/rules.kdl):
window-rule {
    match app-id=r#""^([Ss]hare[Xx].*|sharex)$""# title=r#""^Pinned Image.*""#
    open-floating true
    geometry-corner-radius 0
    clip-to-geometry false
}

window-rule {
    match app-id=r#""^([Ss]hare[Xx].*|sharex)$""# title=r#""^(Screen Color Picker|Color Picker|Insert Image|New Image|Select Area).*""#
    open-floating true
}

window-rule {
    match app-id=r#""^([Ss]hare[Xx].*|sharex)$""# title=r#""^(ShareX \(Wayland Linux\)|ShareX - Image editor).*""#
    open-floating false // Opens as normal tiling columns in Niri ribbon
}";

    public MainWindowViewModel()
    {
        NoctaliaThemeService.PaletteChanged += palette =>
        {
            if (string.Equals(SelectedThemeMode, "Auto", StringComparison.OrdinalIgnoreCase))
            {
                ThemeManager.ApplyPalette(palette);
                ThemeStatus = $"Auto (Noctalia: {palette.Name})";
            }
        };

        LoadSettings();
        _ = RefreshHistoryAsync();
    }

    partial void OnSelectedThemeModeChanged(string value)
    {
        ApplyTheme(value);
    }

    public void ApplyTheme(string mode)
    {
        if (string.Equals(mode, "Auto", StringComparison.OrdinalIgnoreCase))
        {
            if (NoctaliaThemeService.TryLoadCurrentPalette(out var palette) && palette != null)
            {
                ThemeManager.ApplyPalette(palette);
                NoctaliaThemeService.StartWatching();
                ThemeStatus = $"Auto (Noctalia: {palette.Name})";
            }
            else
            {
                NoctaliaThemeService.StopWatching();
                ThemeManager.Configure(new ApplicationThemeOptions
                {
                    UseSystemTheme = true,
                    UseSystemAccentColor = true
                });
                ThemeStatus = "Auto (XDG Desktop Portal)";
            }
        }
        else
        {
            NoctaliaThemeService.StopWatching();
            var preset = ThemeManager.GetPreset(mode);
            if (preset != null)
            {
                ThemeManager.ApplyPalette(preset);
                ThemeStatus = $"Preset ({preset.Name})";
            }
        }
    }

    private void LoadSettings()
    {
        var s = SettingsManager.Instance.Settings;
        ScreenshotsDirectory = s.ScreenshotsDirectory;
        FileNamePattern = s.FileNamePattern;
        SelectedUploader = s.SelectedImageUploader;
        CustomSxcuPath = s.CustomUploaderConfigPath;

        CopyImageToClipboard = s.AfterCapture.HasFlag(AfterCaptureTasks.CopyImageToClipboard);
        SaveImageToFile = s.AfterCapture.HasFlag(AfterCaptureTasks.SaveImageToFile);
        OpenInEditor = s.AfterCapture.HasFlag(AfterCaptureTasks.OpenInImageEditor);
        UploadToHost = s.AfterCapture.HasFlag(AfterCaptureTasks.UploadImageToHost);
        ShowNotification = s.AfterCapture.HasFlag(AfterCaptureTasks.ShowNotification);

        SelectedThemeMode = s.ThemeMode;
        ApplyTheme(SelectedThemeMode);
    }

    [RelayCommand]
    public void SaveSettings()
    {
        var s = SettingsManager.Instance.Settings;
        s.ScreenshotsDirectory = ScreenshotsDirectory;
        s.FileNamePattern = FileNamePattern;
        s.SelectedImageUploader = SelectedUploader;
        s.CustomUploaderConfigPath = CustomSxcuPath;
        s.ThemeMode = SelectedThemeMode;

        var tasks = AfterCaptureTasks.None;
        if (CopyImageToClipboard) tasks |= AfterCaptureTasks.CopyImageToClipboard;
        if (SaveImageToFile) tasks |= AfterCaptureTasks.SaveImageToFile;
        if (OpenInEditor) tasks |= AfterCaptureTasks.OpenInImageEditor;
        if (UploadToHost) tasks |= AfterCaptureTasks.UploadImageToHost;
        if (ShowNotification) tasks |= AfterCaptureTasks.ShowNotification;

        s.AfterCapture = tasks;
        SettingsManager.Instance.Save();
        StatusMessage = "Settings saved successfully.";
    }

    [RelayCommand]
    public async Task RefreshHistoryAsync()
    {
        var items = await HistoryManager.Instance.GetRecentEntriesAsync(50);
        HistoryItems.Clear();
        foreach (var itm in items)
        {
            HistoryItems.Add(itm);
        }
    }

    [RelayCommand]
    public async Task CaptureRegionAsync()
    {
        StatusMessage = "Capturing region...";
        await ShareXCoordinator.Instance.TriggerRegionCaptureAsync();
        await RefreshHistoryAsync();
        StatusMessage = "Ready";
    }

    [RelayCommand]
    public async Task CaptureFullScreenAsync()
    {
        StatusMessage = "Capturing full screen...";
        await ShareXCoordinator.Instance.TriggerFullScreenCaptureAsync();
        await RefreshHistoryAsync();
        StatusMessage = "Ready";
    }

    [RelayCommand]
    public async Task RecordRegionAsync()
    {
        StatusMessage = "Recording region...";
        await ShareXCoordinator.Instance.TriggerRecordRegionAsync(isGif: false);
        await RefreshHistoryAsync();
        StatusMessage = "Ready";
    }

    [RelayCommand]
    public async Task RecordGifAsync()
    {
        StatusMessage = "Recording GIF...";
        await ShareXCoordinator.Instance.TriggerRecordRegionAsync(isGif: true);
        await RefreshHistoryAsync();
        StatusMessage = "Ready";
    }

    [RelayCommand]
    public void OpenEditorBlank()
    {
        ShareXCoordinator.Instance.OpenEditorWithPath(string.Empty);
    }

    [RelayCommand]
    public void OpenFile(HistoryEntry entry)
    {
        if (File.Exists(entry.FilePath))
        {
            try { Process.Start("xdg-open", entry.FilePath); } catch { }
        }
    }

    [RelayCommand]
    public void EditHistoryEntry(HistoryEntry entry)
    {
        if (File.Exists(entry.FilePath))
        {
            ShareXCoordinator.Instance.OpenEditorWithPath(entry.FilePath);
        }
    }

    [RelayCommand]
    public async Task DeleteHistoryEntryAsync(HistoryEntry entry)
    {
        await HistoryManager.Instance.DeleteEntryAsync(entry.Id);
        HistoryItems.Remove(entry);
    }

    [RelayCommand]
    public async Task CopySnippetAsync()
    {
        await WaylandClipboard.CopyTextAsync(NiriConfigSnippet);
        StatusMessage = "Niri config snippet copied to clipboard!";
    }
}
