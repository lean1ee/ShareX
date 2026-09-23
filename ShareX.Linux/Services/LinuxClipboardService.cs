using System;
using ShareX.ImageEditor.Integration;
using ShareX.Linux.Core.Helpers;
using SkiaSharp;

namespace ShareX.Linux.Services;

public class LinuxClipboardService : IClipboardService
{
    public void SetImage(SKBitmap bitmap)
    {
        _ = WaylandClipboard.CopyImageAsync(bitmap);
    }

    public SKBitmap? GetImage()
    {
        return WaylandClipboard.GetImageBitmapAsync().GetAwaiter().GetResult();
    }
}
