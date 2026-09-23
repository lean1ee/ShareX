using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using ShareX.ImageEditor.Integration;
using ShareX.ImageEditor.Integration.Diagnostics;
using ShareX.Linux.Core.Helpers;
using ShareX.Linux.Ipc;

namespace ShareX.Linux;

public static class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        AppLogger.Init();

        EditorServices.Clipboard = new Services.LinuxClipboardService();

        // Wire EditorServices diagnostics to our persistent AppLogger
        EditorServices.Diagnostics = new DelegateEditorDiagnosticsSink(diag =>
        {
            var level = diag.Level switch
            {
                EditorDiagnosticLevel.Error => "ERROR",
                EditorDiagnosticLevel.Warning => "WARN",
                EditorDiagnosticLevel.Information => "INFO",
                _ => "DEBUG"
            };
            AppLogger.Log(level, $"Editor:{diag.Source}", $"{diag.Message} {diag.ExceptionText}".TrimEnd());
        });

        if (args.Length > 0)
        {
            var firstArg = args[0].ToLowerInvariant();

            if (firstArg is "--help" or "-h" or "help")
            {
                PrintHelp();
                return;
            }

            if (firstArg is "ping" or "--ping")
            {
                var (success, response) = IpcClient.SendCommandWithRetryAsync("ping", maxRetries: 10, delayMs: 150).GetAwaiter().GetResult();
                if (success)
                {
                    Console.WriteLine(response);
                    return;
                }

                Console.Error.WriteLine("ShareX daemon is not running.");
                Environment.Exit(1);
                return;
            }

            if (firstArg is "exit" or "quit" or "--exit" or "--quit")
            {
                try
                {
                    Process.Start(new ProcessStartInfo("systemctl", "--user stop sharex.service") { CreateNoWindow = true })?.WaitForExit();
                }
                catch { }

                var (success, _) = IpcClient.SendCommandAsync("exit", timeoutMs: 1000).GetAwaiter().GetResult();
                Console.WriteLine("ShareX stopped.");
                return;
            }

            if (firstArg is "--background" or "-b" or "--tray")
            {
                var (success, _) = IpcClient.SendCommandAsync("ping", timeoutMs: 500).GetAwaiter().GetResult();
                if (success)
                {
                    AppLogger.LogInfo("Program", "ShareX daemon is already running. Exiting redundant background request.");
                    return;
                }
            }
            else
            {
                // Forward command to background daemon
                var (success, response) = IpcClient.SendCommandWithRetryAsync(string.Join(" ", args), maxRetries: 4, delayMs: 100).GetAwaiter().GetResult();
                if (success)
                {
                    AppLogger.LogInfo("Program", $"Forwarded command to daemon via IPC: {string.Join(" ", args)} -> {response}");
                    return;
                }
            }
        }

        AppLogger.LogInfo("Program", "Starting Avalonia application lifetime...");
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    private static void PrintHelp()
    {
        Console.WriteLine(@"ShareX for Linux (Wayland)
Usage: sharex [options]

Commands:
  --capture-region      Start interactive region screenshot
  --capture-region-edit Capture region and open directly in Image Editor
  --capture-screen      Instantly capture full screen
  --record-region       Start region video recording (MP4)
  --record-gif          Start region animated GIF recording
  --open-editor [file]  Open ShareX Image Editor
  --toggle-ui           Toggle Main Window visibility
  --background          Start ShareX minimized in system tray
  --help, -h            Show this help message

Wayland Compositor Integration:
  Bind print keys in your compositor (e.g. ~/.config/niri/config.kdl):
    Print { spawn ""sharex"" ""--capture-region""; }
    Shift+Print { spawn ""sharex"" ""--capture-region-edit""; }
    Mod+Print { spawn ""sharex"" ""--capture-screen""; }
");
    }
}
