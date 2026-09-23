using System;
using System.Threading.Tasks;
using ShareX.Linux.Capture.Backends;
using ShareX.Linux.Core.Settings;
using SkiaSharp;

namespace ShareX.Linux.Capture;

public class ScreenCaptureService
{
    private static readonly Lazy<ScreenCaptureService> _instance = new(() => new ScreenCaptureService());
    public static ScreenCaptureService Instance => _instance.Value;

    private readonly IScreenCaptureBackend _wlrBackend = new WlrScreenCaptureBackend();
    private readonly IScreenCaptureBackend _portalBackend = new PortalScreenCaptureBackend();

    public IScreenCaptureBackend GetBackend()
    {
        var settings = SettingsManager.Instance.Settings;
        if (settings.UseWlrScreencopyFirst && _wlrBackend.IsAvailable())
        {
            return _wlrBackend;
        }

        if (_portalBackend.IsAvailable())
        {
            return _portalBackend;
        }

        return _wlrBackend;
    }

    public async Task<SKBitmap?> CaptureFullScreenAsync()
    {
        var backend = GetBackend();
        return await backend.CaptureFullScreenAsync().ConfigureAwait(false);
    }

    public async Task<SKBitmap?> CaptureRegionAsync(int x, int y, int width, int height)
    {
        var backend = GetBackend();
        return await backend.CaptureRegionAsync(x, y, width, height).ConfigureAwait(false);
    }
}
