using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace ShareX.Linux.Recording;

public static class GifConverter
{
    public static async Task<bool> ConvertToGifAsync(string inputMp4Path, string outputGifPath, int fps = 15)
    {
        try
        {
            var palettePath = Path.Combine(Path.GetTempPath(), $"palette_{Guid.NewGuid():N}.png");

            // Pass 1: generate color palette
            var psiPalette = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true
            };
            psiPalette.ArgumentList.Add("-y");
            psiPalette.ArgumentList.Add("-i");
            psiPalette.ArgumentList.Add(inputMp4Path);
            psiPalette.ArgumentList.Add("-vf");
            psiPalette.ArgumentList.Add($"fps={fps},scale=flags=lanczos,palettegen");
            psiPalette.ArgumentList.Add(palettePath);

            using (var procPalette = Process.Start(psiPalette))
            {
                if (procPalette == null) return false;
                await procPalette.WaitForExitAsync().ConfigureAwait(false);
                if (procPalette.ExitCode != 0 || !File.Exists(palettePath)) return false;
            }

            // Pass 2: generate GIF using palette
            var psiGif = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true
            };
            psiGif.ArgumentList.Add("-y");
            psiGif.ArgumentList.Add("-i");
            psiGif.ArgumentList.Add(inputMp4Path);
            psiGif.ArgumentList.Add("-i");
            psiGif.ArgumentList.Add(palettePath);
            psiGif.ArgumentList.Add("-filter_complex");
            psiGif.ArgumentList.Add($"fps={fps},scale=flags=lanczos[x];[x][1:v]paletteuse=dither=bayer:bayer_scale=3");
            psiGif.ArgumentList.Add(outputGifPath);

            using (var procGif = Process.Start(psiGif))
            {
                if (procGif == null) return false;
                await procGif.WaitForExitAsync().ConfigureAwait(false);
            }

            // Clean up temporary palette
            if (File.Exists(palettePath))
            {
                File.Delete(palettePath);
            }

            return File.Exists(outputGifPath);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[GifConverter] Failed to convert to GIF: {ex.Message}");
            return false;
        }
    }
}
