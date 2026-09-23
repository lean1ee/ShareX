using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace ShareX.Linux.Core.Helpers;

public static class AppLogger
{
    private static readonly object _syncLock = new();
    private static string? _logFilePath;
    private static bool _initialized;

    public static string LogFilePath => _logFilePath ?? PathsHelper.LogFilePath;

    public static void Init(string? customPath = null)
    {
        lock (_syncLock)
        {
            if (_initialized) return;
            _initialized = true;

            _logFilePath = customPath ?? PathsHelper.LogFilePath;

            try
            {
                var dir = Path.GetDirectoryName(_logFilePath);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                // Check size for rotation (rotate if > 10MB)
                if (File.Exists(_logFilePath))
                {
                    var fileInfo = new FileInfo(_logFilePath);
                    if (fileInfo.Length > 10 * 1024 * 1024)
                    {
                        var oldPath = _logFilePath + ".old";
                        File.Move(_logFilePath, oldPath, overwrite: true);
                    }
                }

                // Hook global unhandled exceptions
                AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                {
                    var ex = e.ExceptionObject as Exception;
                    Log("FATAL", "AppDomain", $"Unhandled exception (IsTerminating={e.IsTerminating}): {ex?.Message}", ex);
                };

                TaskScheduler.UnobservedTaskException += (s, e) =>
                {
                    Log("ERROR", "TaskScheduler", $"Unobserved task exception: {e.Exception?.Message}", e.Exception);
                    e.SetObserved();
                };

                Log("INFO", "AppLogger", $"=== ShareX Linux Started (PID={Environment.ProcessId}) ===");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[AppLogger] Failed to initialize file logger: {ex.Message}");
            }
        }
    }

    public static void LogInfo(string tag, string message) => Log("INFO", tag, message);

    public static void LogWarn(string tag, string message, Exception? ex = null) => Log("WARN", tag, message, ex);

    public static void LogError(string tag, string message, Exception? ex = null) => Log("ERROR", tag, message, ex);

    public static void LogException(string tag, Exception ex) => Log("ERROR", tag, ex.Message, ex);

    public static void LogDebug(string tag, string message) => Log("DEBUG", tag, message);

    public static void Log(string level, string tag, string message, Exception? ex = null)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var line = $"[{timestamp}] [{level}] [{tag}] {message}";

        if (ex != null)
        {
            line += $"{Environment.NewLine}{ex}";
        }

        // Print to console
        if (level is "ERROR" or "FATAL")
        {
            Console.Error.WriteLine(line);
        }
        else
        {
            Console.WriteLine(line);
        }

        // Append to file
        var path = LogFilePath;
        if (!string.IsNullOrEmpty(path))
        {
            lock (_syncLock)
            {
                try
                {
                    File.AppendAllText(path, line + Environment.NewLine, Encoding.UTF8);
                }
                catch
                {
                    // Fallback to avoid crashing the logging thread
                }
            }
        }
    }
}
