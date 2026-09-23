using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.Controls.Shapes;

namespace ShareX.Linux.Recording;

public partial class RecordingToolbarWindow : Window
{
    private readonly WfRecorderProcess _recorder;
    private readonly TaskCompletionSource<bool> _tcs = new();
    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _stopwatch = new();
    private TextBlock? _timerText;
    private Ellipse? _recDot;

    public Task<bool> ResultTask => _tcs.Task;

    public RecordingToolbarWindow()
    {
        InitializeComponent();
        _recorder = new WfRecorderProcess(string.Empty);
        _timer = new DispatcherTimer();
    }

    public RecordingToolbarWindow(WfRecorderProcess recorder)
    {
        InitializeComponent();
        _recorder = recorder;

        _timerText = this.FindControl<TextBlock>("TimerText");
        _recDot = this.FindControl<Ellipse>("RecDot");

        var stopBtn = this.FindControl<Button>("StopButton");
        var cancelBtn = this.FindControl<Button>("CancelButton");

        if (stopBtn != null)
        {
            stopBtn.Click += async (_, _) =>
            {
                _timer.Stop();
                if (_recorder != null)
                {
                    await _recorder.StopAsync().ConfigureAwait(true);
                }
                _tcs.TrySetResult(true);
                Close();
            };
        }

        if (cancelBtn != null)
        {
            cancelBtn.Click += (_, _) =>
            {
                _timer.Stop();
                _recorder?.Cancel();
                _tcs.TrySetResult(false);
                Close();
            };
        }

        // Allow window dragging
        PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                BeginMoveDrag(e);
            }
        };

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _timer.Tick += OnTimerTick;

        _stopwatch.Start();
        _timer.Start();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        if (_timerText != null)
        {
            var elapsed = _stopwatch.Elapsed;
            _timerText.Text = $"{elapsed.Minutes:D2}:{elapsed.Seconds:D2}";
        }

        if (_recDot != null)
        {
            // Blink red dot
            _recDot.Opacity = _recDot.Opacity > 0.5 ? 0.2 : 1.0;
        }
    }
}
