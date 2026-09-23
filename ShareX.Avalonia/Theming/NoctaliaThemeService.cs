using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using Avalonia.Media;
using Avalonia.Threading;

namespace ShareX.AvaloniaUI.Theming;

public static class NoctaliaThemeService
{
    private static FileSystemWatcher? _watcher;
    private static Timer? _debounceTimer;
    public static event Action<ThemePalette>? PaletteChanged;

    public static string NoctaliaConfigDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "noctalia");

    public static string NoctaliaColorsJsonPath =>
        Path.Combine(NoctaliaConfigDir, "colors.json");

    public static bool IsNoctaliaAvailable =>
        File.Exists(NoctaliaColorsJsonPath);

    public static bool TryLoadCurrentPalette(out ThemePalette? palette)
    {
        palette = null;
        try
        {
            if (!File.Exists(NoctaliaColorsJsonPath))
            {
                return false;
            }

            var json = File.ReadAllText(NoctaliaColorsJsonPath);
            palette = ParseColorsJson(json);
            return palette != null;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[NoctaliaThemeService] Error loading colors.json: {ex.Message}");
            return false;
        }
    }

    public static void StartWatching()
    {
        if (_watcher != null) return;

        try
        {
            if (!Directory.Exists(NoctaliaConfigDir))
            {
                return;
            }

            _watcher = new FileSystemWatcher(NoctaliaConfigDir, "colors.json")
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size
            };

            _watcher.Changed += OnConfigFileChanged;
            _watcher.Created += OnConfigFileChanged;
            _watcher.Renamed += OnConfigFileChanged;
            _watcher.EnableRaisingEvents = true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[NoctaliaThemeService] Failed to start watcher: {ex.Message}");
        }
    }

    public static void StopWatching()
    {
        if (_watcher != null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
            _watcher = null;
        }

        _debounceTimer?.Dispose();
        _debounceTimer = null;
    }

    private static void OnConfigFileChanged(object sender, FileSystemEventArgs e)
    {
        // Debounce multiple rapid file events (e.g. editor saving or atomic file replace)
        _debounceTimer?.Dispose();
        _debounceTimer = new Timer(_ =>
        {
            if (TryLoadCurrentPalette(out var palette) && palette != null)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    PaletteChanged?.Invoke(palette);
                });
            }
        }, null, 150, Timeout.Infinite);
    }

    public static ThemePalette? ParseColorsJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Color GetColor(string propertyName, Color fallback)
            {
                if (root.TryGetProperty(propertyName, out var prop))
                {
                    var hex = prop.GetString();
                    if (!string.IsNullOrWhiteSpace(hex) && Color.TryParse(hex, out var c))
                    {
                        return c;
                    }
                }
                return fallback;
            }

            var surface = GetColor("mSurface", Color.Parse("#1F1F28"));
            var surfaceVariant = GetColor("mSurfaceVariant", Color.Parse("#2A2A37"));
            var outline = GetColor("mOutline", Color.Parse("#363646"));
            var primary = GetColor("mPrimary", Color.Parse("#76946A"));
            var onPrimary = GetColor("mOnPrimary", Color.Parse("#1F1F28"));
            var onSurface = GetColor("mOnSurface", Color.Parse("#DCD7BA"));
            var onSurfaceVariant = GetColor("mOnSurfaceVariant", Color.Parse("#717C7C"));
            var hover = GetColor("mHover", Color.Parse("#7E9CD8"));
            var error = GetColor("mError", Color.Parse("#C34043"));
            var shadow = GetColor("mShadow", Color.Parse("#11111B"));

            // Calculate luminance to determine Dark/Light
            double lum = (0.299 * surface.R + 0.587 * surface.G + 0.114 * surface.B) / 255.0;
            bool isDark = lum < 0.5;

            var toolbar = isDark ? Darken(surface, 0.15) : Lighten(surface, 0.05);

            return new ThemePalette
            {
                Name = "Noctalia",
                IsDark = isDark,
                BackgroundMain = surface,
                BackgroundPanel = surfaceVariant,
                BackgroundPopup = surfaceVariant,
                BackgroundToolbar = toolbar,
                Border = outline,
                ControlBackground = surfaceVariant,
                ControlBackgroundHover = isDark ? Lighten(surfaceVariant, 0.08) : Darken(surfaceVariant, 0.05),
                ControlBorder = outline,
                Separator = outline,
                Text = onSurface,
                TextSecondary = onSurfaceVariant,
                Accent = primary,
                AccentEnd = isDark ? Darken(primary, 0.12) : Lighten(primary, 0.12),
                AccentForeground = onPrimary,
                StatusSuccess = GetColor("mSecondary", Color.Parse("#98BB6C")),
                StatusError = error
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[NoctaliaThemeService] Failed to parse JSON: {ex.Message}");
            return null;
        }
    }

    private static Color Darken(Color color, double amount)
    {
        double factor = Math.Clamp(1.0 - amount, 0, 1);
        return Color.FromArgb(color.A,
            (byte)Math.Round(color.R * factor),
            (byte)Math.Round(color.G * factor),
            (byte)Math.Round(color.B * factor));
    }

    private static Color Lighten(Color color, double amount)
    {
        double factor = Math.Clamp(amount, 0, 1);
        return Color.FromArgb(color.A,
            (byte)Math.Round(color.R + (255 - color.R) * factor),
            (byte)Math.Round(color.G + (255 - color.G) * factor),
            (byte)Math.Round(color.B + (255 - color.B) * factor));
    }
}
