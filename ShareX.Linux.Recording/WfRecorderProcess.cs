using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace ShareX.Linux.Recording;

public class WfRecorderProcess
{
    private Process? _process;
    public string OutputPath { get; }
    public bool IsRecording => _process != null && !_process.HasExited;

    public WfRecorderProcess(string outputPath)
    {
        OutputPath = outputPath;
    }

    public static bool IsAvailable()
    {
        return File.Exists("/usr/bin/wf-recorder");
    }

    public bool Start(string? geometry = null, int fps = 30, bool audio = false)
    {
        if (IsRecording) return false;

        try
        {
            var dir = Path.GetDirectoryName(OutputPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var psi = new ProcessStartInfo
            {
                FileName = "wf-recorder",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            psi.ArgumentList.Add("-f");
            psi.ArgumentList.Add(OutputPath);

            psi.ArgumentList.Add("-r");
            psi.ArgumentList.Add(fps.ToString());

            if (!string.IsNullOrWhiteSpace(geometry))
            {
                psi.ArgumentList.Add("-g");
                psi.ArgumentList.Add(geometry);
            }

            if (audio)
            {
                psi.ArgumentList.Add("-a");
            }

            _process = Process.Start(psi);
            return _process != null && !_process.HasExited;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[WfRecorderProcess] Failed to start: {ex.Message}");
            return false;
        }
    }

    public async Task StopAsync()
    {
        if (_process == null || _process.HasExited) return;

        try
        {
            // Send SIGINT (Ctrl+C) to wf-recorder so it finishes the MP4 container cleanly
            using var killProc = Process.Start("kill", $"-2 {_process.Id}");
            killProc?.WaitForExit();

            // Wait up to 5 seconds for process to exit
            var timeoutTask = Task.Delay(5000);
            var waitTask = _process.WaitForExitAsync();

            var completed = await Task.WhenAny(waitTask, timeoutTask).ConfigureAwait(false);
            if (completed == timeoutTask && !_process.HasExited)
            {
                _process.Kill();
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[WfRecorderProcess] Failed to stop cleanly: {ex.Message}");
        }
        finally
        {
            _process?.Dispose();
            _process = null;
        }
    }

    public void Cancel()
    {
        if (_process != null && !_process.HasExited)
        {
            try
            {
                _process.Kill();
            }
            catch { }
        }

        try
        {
            if (File.Exists(OutputPath))
            {
                File.Delete(OutputPath);
            }
        }
        catch { }

        _process?.Dispose();
        _process = null;
    }
}
