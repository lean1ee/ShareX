using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using ShareX.ImageEditor.Integration;
using ShareX.ImageEditor.Presentation.ViewModels;
using ShareX.ImageEditor.Presentation.Views;
using ShareX.Linux.Capture;
using ShareX.Linux.Capture.Backends;
using ShareX.Linux.Capture.Overlay;
using ShareX.Linux.Core.Helpers;
using ShareX.Linux.Core.History;
using ShareX.Linux.Core.Pipeline;
using ShareX.Linux.Core.Settings;
using ShareX.Linux.Recording;
using Avalonia.Platform.Storage;
using ShareX.Linux.Core.Uploaders;
using ShareX.Linux.Views;
using SkiaSharp;

namespace ShareX.Linux.Services;

public class ShareXCoordinator
{
    private static readonly Lazy<ShareXCoordinator> _instance = new(() => new ShareXCoordinator());
    public static ShareXCoordinator Instance => _instance.Value;

    private int _isCapturing;
    private DateTime _lastCaptureTime = DateTime.MinValue;

    public async Task<bool> TriggerRegionCaptureAsync(bool forceEditor = false)
    {
        if (DateTime.UtcNow - _lastCaptureTime < TimeSpan.FromMilliseconds(400))
        {
            AppLogger.LogWarn("Coordinator", "Region capture request dropped (debounced).");
            return false;
        }

        if (System.Threading.Interlocked.CompareExchange(ref _isCapturing, 1, 0) != 0)
        {
            AppLogger.LogWarn("Coordinator", "Region capture request dropped: another capture/recording is already in progress.");
            return false;
        }

        _lastCaptureTime = DateTime.UtcNow;

        return await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            try
            {
                AppLogger.LogInfo("Coordinator", $"TriggerRegionCaptureAsync invoked: forceEditor={forceEditor}");
                SKBitmap? bitmap = null;

                // 1. Prefer native Wayland layer-shell selector (slurp)
                if (SlurpRegionSelector.IsAvailable())
                {
                    var geometry = await SlurpRegionSelector.SelectRegionAsync();
                    if (string.IsNullOrEmpty(geometry))
                    {
                        AppLogger.LogInfo("Coordinator", "Region selection cancelled by user.");
                        return false;
                    }

                    AppLogger.LogInfo("Coordinator", $"Region selected: '{geometry}'. Executing grim capture...");
                    bitmap = await WlrScreenCaptureBackend.ExecuteGrimCaptureAsync(geometry);
                }
                else
                {
                    // Fallback to Avalonia interactive overlay window
                    var bg = await ScreenCaptureService.Instance.CaptureFullScreenAsync();
                    if (bg == null)
                    {
                        AppLogger.LogError("Coordinator", "Failed to capture screen background for overlay.");
                        await DesktopNotification.ShowAsync("ShareX Error", "Failed to capture screen.");
                        return false;
                    }

                    var overlay = new InteractiveOverlayWindow(bg);
                    overlay.Show();

                    var res = await overlay.ResultTask;
                    if (res.Action == OverlayAction.Cancel || res.CroppedBitmap == null)
                    {
                        AppLogger.LogInfo("Coordinator", "Interactive overlay cancelled by user.");
                        return false;
                    }

                    bitmap = res.CroppedBitmap;

                    if (res.Action == OverlayAction.Edit)
                    {
                        AppLogger.LogInfo("Coordinator", "Overlay action: Edit requested.");
                        OpenEditorWithBitmap(bitmap);
                        return true;
                    }

                    if (res.Action == OverlayAction.Copy)
                    {
                        AppLogger.LogInfo("Coordinator", "Overlay action: Copy requested.");
                        await WaylandClipboard.CopyImageAsync(bitmap);
                        await DesktopNotification.ShowAsync("ShareX", "Screenshot copied to clipboard.");
                        return true;
                    }
                }

                if (bitmap == null)
                {
                    AppLogger.LogError("Coordinator", "Capture result bitmap was null.");
                    await DesktopNotification.ShowAsync("ShareX Error", "Failed to capture selected region.");
                    return false;
                }

                AppLogger.LogInfo("Coordinator", $"Captured bitmap: {bitmap.Width}x{bitmap.Height}. forceEditor={forceEditor}");

                if (forceEditor)
                {
                    OpenEditorWithBitmap(bitmap);
                    return true;
                }

                // Process through AfterCapture pipeline (Clipboard, File, History, Upload, Notification)
                await CapturePipeline.ProcessAsync(bitmap, path => OpenEditorWithPath(path));
                return true;
            }
            catch (Exception ex)
            {
                AppLogger.LogException("Coordinator", ex);
                return false;
            }
            finally
            {
                _lastCaptureTime = DateTime.UtcNow;
                System.Threading.Interlocked.Exchange(ref _isCapturing, 0);
            }
        });
    }

    public async Task<bool> TriggerFullScreenCaptureAsync()
    {
        if (DateTime.UtcNow - _lastCaptureTime < TimeSpan.FromMilliseconds(400))
        {
            AppLogger.LogWarn("Coordinator", "Full screen capture request dropped (debounced).");
            return false;
        }

        if (System.Threading.Interlocked.CompareExchange(ref _isCapturing, 1, 0) != 0)
        {
            AppLogger.LogWarn("Coordinator", "Full screen capture request dropped: another capture/recording is already in progress.");
            return false;
        }

        _lastCaptureTime = DateTime.UtcNow;

        return await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            try
            {
                var bg = await ScreenCaptureService.Instance.CaptureFullScreenAsync();
                if (bg == null)
                {
                    await DesktopNotification.ShowAsync("ShareX Error", "Failed to capture screen.");
                    return false;
                }

                await CapturePipeline.ProcessAsync(bg, path => OpenEditorWithPath(path));
                return true;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[ShareXCoordinator] FullScreenCapture error: {ex.Message}");
                return false;
            }
            finally
            {
                _lastCaptureTime = DateTime.UtcNow;
                System.Threading.Interlocked.Exchange(ref _isCapturing, 0);
            }
        });
    }

    public async Task<bool> TriggerRecordRegionAsync(bool isGif = false)
    {
        if (DateTime.UtcNow - _lastCaptureTime < TimeSpan.FromMilliseconds(400))
        {
            AppLogger.LogWarn("Coordinator", "Record region request dropped (debounced).");
            return false;
        }

        if (System.Threading.Interlocked.CompareExchange(ref _isCapturing, 1, 0) != 0)
        {
            AppLogger.LogWarn("Coordinator", "Record region request dropped: another capture/recording is already in progress.");
            return false;
        }

        _lastCaptureTime = DateTime.UtcNow;

        return await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            try
            {
                if (!WfRecorderProcess.IsAvailable())
                {
                    await DesktopNotification.ShowAsync("ShareX Error", "wf-recorder is not installed.");
                    return false;
                }

                string? geometry = null;
                if (SlurpRegionSelector.IsAvailable())
                {
                    geometry = await SlurpRegionSelector.SelectRegionAsync();
                    if (string.IsNullOrEmpty(geometry)) return false;
                }
                else
                {
                    var bg = await ScreenCaptureService.Instance.CaptureFullScreenAsync();
                    if (bg == null) return false;

                    var overlay = new InteractiveOverlayWindow(bg);
                    overlay.Show();

                    var res = await overlay.ResultTask;
                    if (res.Action == OverlayAction.Cancel) return false;

                    var reg = res.Region;
                    geometry = $"{reg.X},{reg.Y} {reg.Width}x{reg.Height}";
                }

                var settings = SettingsManager.Instance.Settings;
                var fileName = NameParser.GenerateFileName(settings.FileNamePattern, isGif ? "gif" : "mp4");
                var dir = settings.ScreenshotsDirectory;
                Directory.CreateDirectory(dir);

                var tempMp4Path = Path.Combine(Path.GetTempPath(), $"rec_{Guid.NewGuid():N}.mp4");
                var finalPath = Path.Combine(dir, fileName);

                var recorder = new WfRecorderProcess(tempMp4Path);
                if (!recorder.Start(geometry, settings.RecordingFps, settings.RecordAudio))
                {
                    await DesktopNotification.ShowAsync("ShareX Error", "Failed to start wf-recorder process.");
                    return false;
                }

                // Show recording floating toolbar
                int gx = 100, gy = 100;
                if (!string.IsNullOrEmpty(geometry))
                {
                    try
                    {
                        var parts = geometry.Split(' ')[0].Split(',');
                        if (parts.Length == 2)
                        {
                            gx = int.Parse(parts[0]);
                            gy = int.Parse(parts[1]);
                        }
                    }
                    catch { }
                }

                var toolbar = new RecordingToolbarWindow(recorder);
                toolbar.Position = new Avalonia.PixelPoint(Math.Max(20, gx), Math.Max(20, gy - 60));
                toolbar.Show();

                var recordedSuccessfully = await toolbar.ResultTask;
                if (!recordedSuccessfully || !File.Exists(tempMp4Path))
                {
                    return false;
                }

                if (isGif)
                {
                    await DesktopNotification.ShowAsync("ShareX", "Generating optimized GIF, please wait...");
                    var ok = await GifConverter.ConvertToGifAsync(tempMp4Path, finalPath);
                    try { File.Delete(tempMp4Path); } catch { }

                    if (ok)
                    {
                        await DesktopNotification.ShowAsync("ShareX", $"GIF saved: {fileName}", finalPath);
                        await HistoryManager.Instance.AddEntryAsync(new HistoryEntry
                        {
                            FileName = fileName,
                            FilePath = finalPath,
                            Type = "GIF",
                            FileSizeBytes = new FileInfo(finalPath).Length
                        });
                    }
                }
                else
                {
                    File.Move(tempMp4Path, finalPath, overwrite: true);
                    await DesktopNotification.ShowAsync("ShareX", $"Video saved: {fileName}", finalPath);
                    await HistoryManager.Instance.AddEntryAsync(new HistoryEntry
                    {
                        FileName = fileName,
                        FilePath = finalPath,
                        Type = "Video",
                        FileSizeBytes = new FileInfo(finalPath).Length
                    });
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[ShareXCoordinator] Record error: {ex.Message}");
                return false;
            }
            finally
            {
                _lastCaptureTime = DateTime.UtcNow;
                System.Threading.Interlocked.Exchange(ref _isCapturing, 0);
            }
        });
    }

    public void OpenEditorWithBitmap(SKBitmap bitmap)
    {
        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                var options = new ImageEditorOptions
                {
                    ShowExitConfirmation = false
                };
                var editor = new EditorWindow(options);
                if (editor.DataContext is MainViewModel vm)
                {
                    ConfigureEditor(editor, vm);
                    vm.ShowStartScreen = false;
                }
                editor.Show();
                editor.LoadImage(bitmap);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[ShareXCoordinator] Failed to open ImageEditor: {ex.Message}");
            }
        });
    }

    public void OpenEditorWithPath(string filePath)
    {
        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                var options = new ImageEditorOptions
                {
                    ShowExitConfirmation = false
                };
                var editor = new EditorWindow(options);
                if (editor.DataContext is MainViewModel vm)
                {
                    ConfigureEditor(editor, vm);
                    vm.ShowStartScreen = string.IsNullOrEmpty(filePath);
                }
                editor.Show();
                if (!string.IsNullOrEmpty(filePath))
                {
                    editor.LoadImage(filePath);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[ShareXCoordinator] Failed to open ImageEditor with file: {ex.Message}");
            }
        });
    }

    private void ConfigureEditor(EditorWindow editor, MainViewModel vm)
    {
        AppLogger.LogInfo("Coordinator", "Configuring editor toolbar and workflow handlers...");
        try
        {
            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "sharex.ico");
            if (File.Exists(iconPath))
            {
                editor.Icon = new WindowIcon(iconPath);
            }
        }
        catch { }

        vm.ShowFileMenu = true;
        vm.ShowTaskButtons = true;
        vm.ShowBottomToolbar = true;

        vm.HasHostCopyHandler = true;
        vm.HasHostSaveHandler = true;
        vm.HasHostSaveAsHandler = true;

        // 1. Copy (Ctrl+C / Copy button)
        vm.CopyRequested += async () =>
        {
            AppLogger.LogInfo("Coordinator", "Button Click: Copy requested.");
            byte[]? png = editor.GetResultPngBytes();
            if (png != null && png.Length > 0)
            {
                var copied = await WaylandClipboard.CopyImageAsync(png);
                AppLogger.LogInfo("Coordinator", $"Copied {png.Length} bytes to Wayland clipboard: success={copied}");
                await DesktopNotification.ShowAsync("ShareX", "Image copied to clipboard.");
            }
            else
            {
                AppLogger.LogWarn("Coordinator", "Copy failed: No image data returned from editor.");
            }
        };

        // 2. Save (Ctrl+S / Save button)
        vm.SaveRequested += async () =>
        {
            AppLogger.LogInfo("Coordinator", "Button Click: Save requested.");
            byte[]? png = editor.GetResultPngBytes();
            if (png == null || png.Length == 0)
            {
                AppLogger.LogWarn("Coordinator", "Save failed: No image data.");
                return null;
            }

            string savePath;
            if (!string.IsNullOrEmpty(vm.ImageFilePath))
            {
                savePath = vm.ImageFilePath;
                await File.WriteAllBytesAsync(savePath, png);
                AppLogger.LogInfo("Coordinator", $"Overwrote existing image: {savePath}");
            }
            else
            {
                var settings = SettingsManager.Instance.Settings;
                string dir = settings.ScreenshotsDirectory;
                Directory.CreateDirectory(dir);
                string fileName = NameParser.GenerateFileName(settings.FileNamePattern, "png");
                savePath = Path.Combine(dir, fileName);
                await File.WriteAllBytesAsync(savePath, png);
                vm.ImageFilePath = savePath;
                AppLogger.LogInfo("Coordinator", $"Saved new screenshot: {savePath}");
            }

            vm.IsDirty = false;
            await DesktopNotification.ShowAsync("ShareX", $"Saved to {Path.GetFileName(savePath)}", savePath);
            return savePath;
        };

        // 3. Save As (Ctrl+Shift+S / Save As button)
        vm.SaveAsRequested += async () =>
        {
            AppLogger.LogInfo("Coordinator", "Button Click: Save As requested.");
            byte[]? png = editor.GetResultPngBytes();
            if (png == null || png.Length == 0)
            {
                AppLogger.LogWarn("Coordinator", "Save As failed: No image data.");
                return null;
            }

            var topLevel = Avalonia.Controls.TopLevel.GetTopLevel(editor);
            if (topLevel?.StorageProvider == null)
            {
                AppLogger.LogWarn("Coordinator", "Save As failed: StorageProvider is null.");
                return null;
            }

            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save Image As",
                SuggestedFileName = !string.IsNullOrEmpty(vm.ImageFilePath)
                    ? Path.GetFileName(vm.ImageFilePath)
                    : $"Screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png",
                FileTypeChoices =
                [
                    new FilePickerFileType("PNG Image") { Patterns = ["*.png"], MimeTypes = ["image/png"] },
                    new FilePickerFileType("JPEG Image") { Patterns = ["*.jpg", "*.jpeg"], MimeTypes = ["image/jpeg"] },
                    new FilePickerFileType("WebP Image") { Patterns = ["*.webp"], MimeTypes = ["image/webp"] }
                ]
            });

            if (file != null)
            {
                string path = file.Path.LocalPath;
                await File.WriteAllBytesAsync(path, png);
                vm.ImageFilePath = path;
                vm.IsDirty = false;
                AppLogger.LogInfo("Coordinator", $"Save As saved to: {path}");
                await DesktopNotification.ShowAsync("ShareX", $"Saved to {Path.GetFileName(path)}", path);
                return path;
            }

            AppLogger.LogInfo("Coordinator", "Save As cancelled by user.");
            return null;
        };

        // 4. Pin to Screen (Ctrl+P / Pin button)
        vm.PinRequested += () =>
        {
            AppLogger.LogInfo("Coordinator", "Button Click: Pin to Screen requested.");
            var bitmap = editor.GetResultBitmap();
            if (bitmap != null)
            {
                var pinned = new PinnedImageWindow(bitmap);
                pinned.Show();
                vm.IsDirty = false;
                editor.Close();
                AppLogger.LogInfo("Coordinator", "Opened PinnedImageWindow and closed editor.");
            }
            else
            {
                AppLogger.LogWarn("Coordinator", "Pin failed: No bitmap available.");
            }
        };

        // 5. Print (Ctrl+Shift+P / Print button)
        vm.PrintRequested += () =>
        {
            AppLogger.LogInfo("Coordinator", "Button Click: Print requested.");
            var bitmap = editor.GetResultBitmap();
            if (bitmap == null)
            {
                AppLogger.LogWarn("Coordinator", "Print failed: No bitmap available.");
                return;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    await LinuxPrintHelper.PrintBitmapAsync(bitmap);
                }
                catch (Exception ex)
                {
                    AppLogger.LogError("Coordinator", $"Print error: {ex.Message}", ex);
                }
            });
        };

        // 6. Upload (Ctrl+U / Upload button)
        vm.UploadRequested += () =>
        {
            AppLogger.LogInfo("Coordinator", "Button Click: Upload requested.");
            var bitmap = editor.GetResultBitmap();
            if (bitmap == null)
            {
                AppLogger.LogWarn("Coordinator", "Upload failed: No bitmap available.");
                return;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    await DesktopNotification.ShowAsync("ShareX", "Uploading image...");
                    var settings = SettingsManager.Instance.Settings;
                    string fileName = NameParser.GenerateFileName(settings.FileNamePattern, "png");
                    var uploadResult = await UploadManager.Instance.UploadImageAsync(bitmap, fileName);
                    AppLogger.LogInfo("Coordinator", $"Upload completed: success={uploadResult.Success}, url={uploadResult.Url}, err={uploadResult.ErrorMessage}");
                    if (uploadResult.Success && !string.IsNullOrEmpty(uploadResult.Url))
                    {
                        await WaylandClipboard.CopyTextAsync(uploadResult.Url);
                    }
                }
                catch (Exception ex)
                {
                    AppLogger.LogError("Coordinator", $"Upload error: {ex.Message}", ex);
                    await DesktopNotification.ShowAsync("ShareX Upload Error", ex.Message);
                }
            });
        };

        // 7. Continue (Enter / Green checkmark)
        vm.ContinueRequested += async () =>
        {
            AppLogger.LogInfo("Coordinator", "Button Click: Continue requested.");
            byte[]? png = editor.GetResultPngBytes();
            if (png == null || png.Length == 0) return;

            string savePath;
            if (!string.IsNullOrEmpty(vm.ImageFilePath))
            {
                savePath = vm.ImageFilePath;
                await File.WriteAllBytesAsync(savePath, png);
            }
            else
            {
                var settings = SettingsManager.Instance.Settings;
                string dir = settings.ScreenshotsDirectory;
                Directory.CreateDirectory(dir);
                string fileName = NameParser.GenerateFileName(settings.FileNamePattern, "png");
                savePath = Path.Combine(dir, fileName);
                await File.WriteAllBytesAsync(savePath, png);
                vm.ImageFilePath = savePath;
            }

            vm.IsDirty = false;
            await WaylandClipboard.CopyImageAsync(png);
            AppLogger.LogInfo("Coordinator", $"Continue complete: Saved to {savePath} and copied to clipboard.");
            await DesktopNotification.ShowAsync("ShareX", $"Saved to {Path.GetFileName(savePath)} and copied to clipboard.", savePath);
        };
    }
}
