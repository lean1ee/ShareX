using System.IO;

namespace ShareX.Linux.Core.Helpers;

public static class PathsHelper
{
    public static string AppName => "sharex";

    public static string ConfigDir
    {
        get
        {
            var xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            var dir = string.IsNullOrWhiteSpace(xdg)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", AppName)
                : Path.Combine(xdg, AppName);
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public static string DataDir
    {
        get
        {
            var xdg = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            var dir = string.IsNullOrWhiteSpace(xdg)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", AppName)
                : Path.Combine(xdg, AppName);
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public static string RuntimeDir
    {
        get
        {
            var xdg = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
            var dir = string.IsNullOrWhiteSpace(xdg)
                ? Path.Combine(Path.GetTempPath(), AppName)
                : Path.Combine(xdg, AppName);
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public static string DefaultScreenshotsDir
    {
        get
        {
            var xdgPictures = Environment.GetEnvironmentVariable("XDG_PICTURES_DIR");
            string picturesDir;
            if (!string.IsNullOrWhiteSpace(xdgPictures) && Directory.Exists(xdgPictures))
            {
                picturesDir = xdgPictures;
            }
            else
            {
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                picturesDir = Path.Combine(home, "Pictures");
            }

            var dir = Path.Combine(picturesDir, "Screenshots");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public static string SettingsFilePath => Path.Combine(ConfigDir, "settings.json");
    public static string HistoryDbPath => Path.Combine(DataDir, "history.db");
    public static string IpcSocketPath => Path.Combine(RuntimeDir, "sharex.sock");
    public static string LogFilePath => Path.Combine(DataDir, "sharex.log");
}
