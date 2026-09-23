using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using ShareX.AvaloniaUI.Theming;
using ShareX.Linux.Core.Helpers;
using ShareX.Linux.Core.Settings;
using ShareX.Linux.Ipc;
using ShareX.Linux.Services;
using ShareX.Linux.Views;

namespace ShareX.Linux;

public partial class App : Application
{
    private IpcServer? _ipcServer;
    private MainWindow? _mainWindow;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var settings = SettingsManager.Instance.Settings;
        if (string.Equals(settings.ThemeMode, "Auto", StringComparison.OrdinalIgnoreCase))
        {
            if (NoctaliaThemeService.TryLoadCurrentPalette(out var palette) && palette != null)
            {
                ThemeManager.ApplyPalette(palette);
                NoctaliaThemeService.StartWatching();
            }
            else
            {
                ThemeManager.Configure(new ApplicationThemeOptions
                {
                    UseSystemTheme = true,
                    UseSystemAccentColor = settings.UseSystemAccentColor
                });
            }
        }
        else
        {
            var preset = ThemeManager.GetPreset(settings.ThemeMode);
            if (preset != null)
            {
                ThemeManager.ApplyPalette(preset);
            }
            else
            {
                ThemeManager.Configure(new ApplicationThemeOptions());
            }
        }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            _mainWindow = new MainWindow();
            desktop.MainWindow = _mainWindow;

            // Start IPC server to listen for CLI commands from other processes
            _ipcServer = new IpcServer(HandleIpcCommandAsync);
            _ipcServer.Start();
            AppLogger.LogInfo("App", $"IPC Server started at {PathsHelper.IpcSocketPath}");

            desktop.Exit += (_, _) =>
            {
                AppLogger.LogInfo("App", "Application exiting, disposing IPC server...");
                NoctaliaThemeService.StopWatching();
                _ipcServer?.Dispose();
            };

            // Check arguments passed directly to this instance
            var args = desktop.Args ?? Array.Empty<string>();
            var argStr = string.Join(" ", args).ToLowerInvariant();

            if (argStr.Contains("--capture-region-edit") || argStr.Contains("capture-region-edit"))
            {
                Dispatcher.UIThread.Post(async () =>
                {
                    _mainWindow.Hide();
                    await ShareXCoordinator.Instance.TriggerRegionCaptureAsync(forceEditor: true);
                });
            }
            else if (argStr.Contains("--capture-region") || argStr.Contains("capture-region"))
            {
                Dispatcher.UIThread.Post(async () =>
                {
                    _mainWindow.Hide();
                    await ShareXCoordinator.Instance.TriggerRegionCaptureAsync();
                });
            }
            else if (argStr.Contains("--capture-screen") || argStr.Contains("capture-screen"))
            {
                Dispatcher.UIThread.Post(async () =>
                {
                    _mainWindow.Hide();
                    await ShareXCoordinator.Instance.TriggerFullScreenCaptureAsync();
                });
            }
            else if (argStr.Contains("--record-region") || argStr.Contains("record-region"))
            {
                Dispatcher.UIThread.Post(async () =>
                {
                    _mainWindow.Hide();
                    await ShareXCoordinator.Instance.TriggerRecordRegionAsync(isGif: false);
                });
            }
            else if (argStr.Contains("--record-gif") || argStr.Contains("record-gif"))
            {
                Dispatcher.UIThread.Post(async () =>
                {
                    _mainWindow.Hide();
                    await ShareXCoordinator.Instance.TriggerRecordRegionAsync(isGif: true);
                });
            }
            else if (argStr.Contains("--open-editor") || argStr.Contains("open-editor"))
            {
                Dispatcher.UIThread.Post(() =>
                {
                    string file = args.Length > 1 ? args[1] : string.Empty;
                    ShareXCoordinator.Instance.OpenEditorWithPath(file);
                });
            }
            else if (argStr.Contains("--background") || argStr.Contains("--tray"))
            {
                // Start minimized to tray
                _mainWindow.Hide();
            }
            else
            {
                _mainWindow.Show();
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private async Task<string> HandleIpcCommandAsync(string command)
    {
        var cmd = command.ToLowerInvariant().Trim();

        if (cmd.StartsWith("capture-region-edit") || cmd.StartsWith("--capture-region-edit"))
        {
            await ShareXCoordinator.Instance.TriggerRegionCaptureAsync(forceEditor: true);
            return "OK";
        }

        if (cmd.StartsWith("capture-region") || cmd.StartsWith("--capture-region"))
        {
            await ShareXCoordinator.Instance.TriggerRegionCaptureAsync();
            return "OK";
        }

        if (cmd.StartsWith("capture-screen") || cmd.StartsWith("--capture-screen"))
        {
            await ShareXCoordinator.Instance.TriggerFullScreenCaptureAsync();
            return "OK";
        }

        if (cmd.StartsWith("record-region") || cmd.StartsWith("--record-region"))
        {
            await ShareXCoordinator.Instance.TriggerRecordRegionAsync(isGif: false);
            return "OK";
        }

        if (cmd.StartsWith("record-gif") || cmd.StartsWith("--record-gif"))
        {
            await ShareXCoordinator.Instance.TriggerRecordRegionAsync(isGif: true);
            return "OK";
        }

        if (cmd.StartsWith("open-editor") || cmd.StartsWith("--open-editor"))
        {
            var parts = command.Split(' ', 2);
            var file = parts.Length > 1 ? parts[1].Trim() : string.Empty;
            ShareXCoordinator.Instance.OpenEditorWithPath(file);
            return "OK";
        }

        if (cmd.StartsWith("show-window"))
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (_mainWindow != null)
                {
                    _mainWindow.Show();
                    _mainWindow.Activate();
                }
            });
            return "OK";
        }

        if (cmd.StartsWith("hide-window"))
        {
            Dispatcher.UIThread.Post(() =>
            {
                _mainWindow?.Hide();
            });
            return "OK";
        }

        if (cmd.StartsWith("toggle-window") || cmd.StartsWith("ui"))
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (_mainWindow != null)
                {
                    if (_mainWindow.IsVisible) _mainWindow.Hide();
                    else
                    {
                        _mainWindow.Show();
                        _mainWindow.Activate();
                    }
                }
            });
            return "OK";
        }

        if (cmd == "ping" || cmd == "background" || cmd == "--background" || cmd == "tray" || cmd == "--tray")
        {
            return "OK";
        }

        if (cmd == "exit" || cmd == "quit")
        {
            AppLogger.LogInfo("App", "IPC Exit command received.");
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("systemctl", "--user stop sharex.service") { CreateNoWindow = true });
            }
            catch { }

            Dispatcher.UIThread.Post(() =>
            {
                if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime d)
                {
                    d.Shutdown(0);
                }
                else
                {
                    Environment.Exit(0);
                }
            });
            return "OK";
        }

        return "UNKNOWN_COMMAND";
    }

    private void OnTrayCaptureRegion(object? sender, EventArgs e)
    {
        _ = ShareXCoordinator.Instance.TriggerRegionCaptureAsync();
    }

    private void OnTrayCaptureFullScreen(object? sender, EventArgs e)
    {
        _ = ShareXCoordinator.Instance.TriggerFullScreenCaptureAsync();
    }

    private void OnTrayRecordVideo(object? sender, EventArgs e)
    {
        _ = ShareXCoordinator.Instance.TriggerRecordRegionAsync(isGif: false);
    }

    private void OnTrayRecordGif(object? sender, EventArgs e)
    {
        _ = ShareXCoordinator.Instance.TriggerRecordRegionAsync(isGif: true);
    }

    private void OnTrayOpenEditor(object? sender, EventArgs e)
    {
        ShareXCoordinator.Instance.OpenEditorWithPath(string.Empty);
    }

    private void OnTrayShowWindow(object? sender, EventArgs e)
    {
        if (_mainWindow != null)
        {
            _mainWindow.Show();
            _mainWindow.Activate();
        }
    }

    private void OnTrayExit(object? sender, EventArgs e)
    {
        AppLogger.LogInfo("App", "Tray Exit requested by user.");
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("systemctl", "--user stop sharex.service") { CreateNoWindow = true });
        }
        catch { }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown(0);
        }
        else
        {
            Environment.Exit(0);
        }
    }
}
