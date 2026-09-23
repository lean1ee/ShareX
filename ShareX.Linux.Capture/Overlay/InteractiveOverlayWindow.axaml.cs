using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using ShareX.AvaloniaUI.Imaging;
using ShareX.Linux.Core.Helpers;
using SkiaSharp;

namespace ShareX.Linux.Capture.Overlay;

public enum OverlayAction
{
    Cancel,
    Copy,
    Edit,
    Save,
    Upload
}

public class OverlayResult
{
    public OverlayAction Action { get; set; } = OverlayAction.Cancel;
    public SKBitmap? CroppedBitmap { get; set; }
    public PixelRect Region { get; set; }
}

public partial class InteractiveOverlayWindow : Window
{
    private readonly TaskCompletionSource<OverlayResult> _tcs = new();
    private InteractiveOverlayCanvas? _canvas;
    private Border? _toolbar;
    private Point _startPoint;
    private bool _isDragging;
    private SKBitmap? _skBitmap;

    public Task<OverlayResult> ResultTask => _tcs.Task;

    public InteractiveOverlayWindow()
    {
        InitializeComponent();
    }

    public InteractiveOverlayWindow(SKBitmap background) : this()
    {
        _skBitmap = background;
        var avaloniaBitmap = BitmapConversionHelpers.ToAvaloniBitmap(background);

        _canvas = new InteractiveOverlayCanvas
        {
            BackgroundImage = avaloniaBitmap,
            SkiaBitmap = background,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch
        };

        var rootGrid = this.FindControl<Grid>("RootGrid");
        rootGrid?.Children.Insert(0, _canvas);

        _toolbar = this.FindControl<Border>("ActionToolbar");

        // Wire toolbar buttons
        this.FindControl<Button>("CopyButton")!.Click += (_, _) => FinishWithAction(OverlayAction.Copy);
        this.FindControl<Button>("EditButton")!.Click += (_, _) => FinishWithAction(OverlayAction.Edit);
        this.FindControl<Button>("SaveButton")!.Click += (_, _) => FinishWithAction(OverlayAction.Save);
        this.FindControl<Button>("UploadButton")!.Click += (_, _) => FinishWithAction(OverlayAction.Upload);
        this.FindControl<Button>("CancelButton")!.Click += (_, _) => FinishWithAction(OverlayAction.Cancel);

        // Window event hooks
        KeyDown += OnWindowKeyDown;
        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            FinishWithAction(OverlayAction.Cancel);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter)
        {
            FinishWithAction(OverlayAction.Copy);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.C)
        {
            // Copy pixel color under cursor
            if (_canvas?.SkiaBitmap != null)
            {
                var p = _canvas.CurrentPointerPos;
                double scaleX = _canvas.Bounds.Width > 0 ? (double)_canvas.SkiaBitmap.Width / _canvas.Bounds.Width : 1.0;
                double scaleY = _canvas.Bounds.Height > 0 ? (double)_canvas.SkiaBitmap.Height / _canvas.Bounds.Height : 1.0;

                int px = (int)Math.Clamp(Math.Round(p.X * scaleX), 0, _canvas.SkiaBitmap.Width - 1);
                int py = (int)Math.Clamp(Math.Round(p.Y * scaleY), 0, _canvas.SkiaBitmap.Height - 1);
                var c = _canvas.SkiaBitmap.GetPixel(px, py);
                var hex = $"#{c.Red:X2}{c.Green:X2}{c.Blue:X2}";
                _ = WaylandClipboard.CopyTextAsync(hex);
                _ = DesktopNotification.ShowAsync("Color Picked", $"Copied {hex} to clipboard");
            }
            e.Handled = true;
            return;
        }

        if (e.Key == Key.E)
        {
            FinishWithAction(OverlayAction.Edit);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.U)
        {
            FinishWithAction(OverlayAction.Upload);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.S && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            FinishWithAction(OverlayAction.Save);
            e.Handled = true;
            return;
        }
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_toolbar != null && _toolbar.IsVisible)
        {
            var posInToolbar = e.GetPosition(_toolbar);
            if (posInToolbar.X >= 0 && posInToolbar.X <= _toolbar.Bounds.Width &&
                posInToolbar.Y >= 0 && posInToolbar.Y <= _toolbar.Bounds.Height)
            {
                return; // Click inside toolbar
            }
        }

        var pt = e.GetPosition(this);
        _startPoint = pt;
        _isDragging = true;

        if (_canvas != null)
        {
            _canvas.IsSelecting = true;
            _canvas.HasSelection = false;
            _canvas.SelectedRect = new Rect(pt, pt);
            _canvas.InvalidateVisual();
        }

        if (_toolbar != null)
        {
            _toolbar.IsVisible = false;
        }
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        var pt = e.GetPosition(this);

        if (_canvas != null)
        {
            _canvas.CurrentPointerPos = pt;

            if (_isDragging)
            {
                var x = Math.Min(_startPoint.X, pt.X);
                var y = Math.Min(_startPoint.Y, pt.Y);
                var w = Math.Abs(pt.X - _startPoint.X);
                var h = Math.Abs(pt.Y - _startPoint.Y);
                _canvas.SelectedRect = new Rect(x, y, w, h);
            }

            _canvas.InvalidateVisual();
        }
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isDragging) return;
        _isDragging = false;

        if (_canvas != null)
        {
            _canvas.IsSelecting = false;
            var r = _canvas.SelectedRect;

            if (r.Width > 5 && r.Height > 5)
            {
                _canvas.HasSelection = true;
                PositionToolbar(r);
            }
            else
            {
                _canvas.HasSelection = false;
            }

            _canvas.InvalidateVisual();
        }
    }

    private void PositionToolbar(Rect sel)
    {
        if (_toolbar == null) return;

        double tbWidth = 360;
        double tbHeight = 44;

        double tx = sel.Right - tbWidth;
        if (tx < 10) tx = sel.Left;
        if (tx + tbWidth > Bounds.Width) tx = Bounds.Width - tbWidth - 10;

        double ty = sel.Bottom + 10;
        if (ty + tbHeight > Bounds.Height)
        {
            ty = sel.Top - tbHeight - 10;
        }

        _toolbar.Margin = new Thickness(Math.Max(10, tx), Math.Max(10, ty), 0, 0);
        _toolbar.IsVisible = true;
    }

    private void FinishWithAction(OverlayAction action)
    {
        if (action == OverlayAction.Cancel)
        {
            _tcs.TrySetResult(new OverlayResult { Action = OverlayAction.Cancel });
            Close();
            return;
        }

        SKBitmap? cropped = null;
        PixelRect pixelRegion = default;

        if (_skBitmap != null && _canvas != null)
        {
            var sel = _canvas.SelectedRect;
            if (_canvas.HasSelection && sel.Width > 2 && sel.Height > 2)
            {
                double scaleX = _canvas.Bounds.Width > 0 ? (double)_skBitmap.Width / _canvas.Bounds.Width : 1.0;
                double scaleY = _canvas.Bounds.Height > 0 ? (double)_skBitmap.Height / _canvas.Bounds.Height : 1.0;

                int ix = (int)Math.Max(0, Math.Round(sel.Left * scaleX));
                int iy = (int)Math.Max(0, Math.Round(sel.Top * scaleY));
                int iw = (int)Math.Min(_skBitmap.Width - ix, Math.Round(sel.Width * scaleX));
                int ih = (int)Math.Min(_skBitmap.Height - iy, Math.Round(sel.Height * scaleY));

                pixelRegion = new PixelRect(ix, iy, iw, ih);
                var subset = new SKBitmap();
                if (_skBitmap.ExtractSubset(subset, new SKRectI(ix, iy, ix + iw, iy + ih)))
                {
                    cropped = subset;
                }
            }
            else
            {
                // Fullscreen if no specific region was selected
                cropped = _skBitmap.Copy();
                pixelRegion = new PixelRect(0, 0, _skBitmap.Width, _skBitmap.Height);
            }
        }

        _tcs.TrySetResult(new OverlayResult
        {
            Action = action,
            CroppedBitmap = cropped,
            Region = pixelRegion
        });

        Close();
    }
}
