using System.Threading.Tasks;
using SkiaSharp;

namespace ShareX.Linux.Capture.Backends;

public interface IScreenCaptureBackend
{
    string BackendName { get; }
    bool IsAvailable();
    Task<SKBitmap?> CaptureFullScreenAsync();
    Task<SKBitmap?> CaptureRegionAsync(int x, int y, int width, int height);
}
