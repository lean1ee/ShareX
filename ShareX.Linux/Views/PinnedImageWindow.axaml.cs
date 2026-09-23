using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using ShareX.Linux.Core.Helpers;
using SkiaSharp;

namespace ShareX.Linux.Views;

public partial class PinnedImageWindow : Window
{
    private readonly SKBitmap _skBitmap;
    private readonly byte[] _pngBytes;
    private readonly int _originalWidth;
    private readonly int _originalHeight;
    private double _scale = 1.0;

    public PinnedImageWindow() : this(new SKBitmap(400, 300))
    {
    }

    public PinnedImageWindow(SKBitmap skBitmap)
    {
        InitializeComponent();

        _skBitmap = skBitmap ?? throw new ArgumentNullException(nameof(skBitmap));
        _originalWidth = Math.Max(50, skBitmap.Width);
        _originalHeight = Math.Max(50, skBitmap.Height);

        // Convert SKBitmap to PNG bytes for clipboard, saving, and Avalonia display
        using (var image = SKImage.FromBitmap(_skBitmap))
        using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
        {
            _pngBytes = data.ToArray();
        }

        using var ms = new MemoryStream(_pngBytes);
        var avaloniaBitmap = new Bitmap(ms);
        PinnedImage.Source = avaloniaBitmap;

        // Size the window proportionally
        Width = _originalWidth;
        Height = _originalHeight;

        // Cap initial size so it doesn't overflow screen
        if (Width > 1280 || Height > 800)
        {
            double scaleW = 1280.0 / Width;
            double scaleH = 800.0 / Height;
            double factor = Math.Min(scaleW, scaleH);
            Width = Math.Round(Width * factor);
            Height = Math.Round(Height * factor);
            _scale = factor;
        }

        SetupEvents();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void SetupEvents()
    {
        CloseButton.Click += (_, _) => Close();
        CloseButton.PointerEntered += (_, _) => CloseButton.Opacity = 1.0;
        CloseButton.PointerExited += (_, _) => CloseButton.Opacity = 0.4;

        ContainerBorder.PointerPressed += OnContainerPointerPressed;
        ContainerBorder.PointerWheelChanged += OnContainerPointerWheelChanged;

        KeyDown += OnWindowKeyDown;

        // Context Menu
        MenuCopy.Click += async (_, _) => await CopyToClipboardAsync();
        MenuSaveAs.Click += async (_, _) => await SaveAsAsync();
        MenuResetScale.Click += (_, _) => ResetScale();
        MenuClose.Click += (_, _) => Close();

        MenuOpacity100.Click += (_, _) => Opacity = 1.0;
        MenuOpacity80.Click += (_, _) => Opacity = 0.8;
        MenuOpacity60.Click += (_, _) => Opacity = 0.6;
        MenuOpacity40.Click += (_, _) => Opacity = 0.4;

        MenuTopmost.Click += (_, _) =>
        {
            Topmost = !Topmost;
            MenuTopmost.Header = Topmost ? "Always on Top ✓" : "Always on Top";
        };
        MenuTopmost.Header = Topmost ? "Always on Top ✓" : "Always on Top";
    }

    private void OnContainerPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);

        // Middle click closes
        if (point.Properties.IsMiddleButtonPressed)
        {
            Close();
            e.Handled = true;
            return;
        }

        // Double click resets scale
        if (e.ClickCount == 2)
        {
            ResetScale();
            e.Handled = true;
            return;
        }

        // Left button drags window
        if (point.Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
            e.Handled = true;
        }
    }

    private void OnContainerPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            // Adjust Opacity with Ctrl/Shift + Wheel
            double delta = e.Delta.Y > 0 ? 0.1 : -0.1;
            Opacity = Math.Clamp(Math.Round(Opacity + delta, 1), 0.2, 1.0);
            e.Handled = true;
        }
        else
        {
            // Adjust Zoom / Window Size
            double factor = e.Delta.Y > 0 ? 1.1 : 0.9;
            double newWidth = Math.Round(Width * factor);
            double newHeight = Math.Round(Height * factor);

            // Minimum 60px, Maximum 3840px
            if (newWidth >= 60 && newWidth <= 3840 && newHeight >= 60 && newHeight <= 2160)
            {
                Width = newWidth;
                Height = newHeight;
                _scale = Width / _originalWidth;
            }
            e.Handled = true;
        }
    }

    private async void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
        else if (e.Key == Key.C && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            await CopyToClipboardAsync();
            e.Handled = true;
        }
        else if (e.Key == Key.S && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            await SaveAsAsync();
            e.Handled = true;
        }
        else if (e.Key == Key.D0 && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            ResetScale();
            e.Handled = true;
        }
    }

    private void ResetScale()
    {
        _scale = 1.0;
        Width = _originalWidth;
        Height = _originalHeight;
    }

    private async Task CopyToClipboardAsync()
    {
        try
        {
            await WaylandClipboard.CopyImageAsync(_pngBytes);
            await DesktopNotification.ShowAsync("ShareX", "Pinned image copied to clipboard.");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[PinnedImageWindow] Failed to copy to clipboard: {ex.Message}");
        }
    }

    private async Task SaveAsAsync()
    {
        try
        {
            var topLevel = GetTopLevel(this);
            if (topLevel?.StorageProvider == null) return;

            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save Pinned Image As",
                SuggestedFileName = $"Pinned_{DateTime.Now:yyyyMMdd_HHmmss}.png",
                FileTypeChoices =
                [
                    new FilePickerFileType("PNG Image") { Patterns = ["*.png"] },
                    new FilePickerFileType("JPEG Image") { Patterns = ["*.jpg", "*.jpeg"] }
                ]
            });

            if (file != null)
            {
                string path = file.Path.LocalPath;
                await File.WriteAllBytesAsync(path, _pngBytes);
                await DesktopNotification.ShowAsync("ShareX", $"Pinned image saved to {Path.GetFileName(path)}", path);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[PinnedImageWindow] Failed to save image: {ex.Message}");
        }
    }
}
