using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using ShareX.Linux.ViewModels;

namespace ShareX.Linux.Views;

public partial class MainWindow : Window
{
    private Carousel? _carousel;
    private Button? _activeNavButton;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
        _carousel = this.FindControl<Carousel>("ContentCarousel");
        _activeNavButton = this.FindControl<Button>("BtnNavDashboard");
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        e.Cancel = true;
        Hide();
        base.OnClosing(e);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void SetActiveNav(object? sender, int index)
    {
        if (_activeNavButton != null)
        {
            _activeNavButton.Classes.Remove("active");
        }

        if (sender is Button btn)
        {
            _activeNavButton = btn;
            btn.Classes.Add("active");
        }

        if (_carousel != null)
        {
            _carousel.SelectedIndex = index;
        }
    }

    private void OnNavDashboard(object? sender, RoutedEventArgs e)
    {
        SetActiveNav(sender, 0);
    }

    private void OnNavHistory(object? sender, RoutedEventArgs e)
    {
        SetActiveNav(sender, 1);
        if (DataContext is MainWindowViewModel vm)
        {
            _ = vm.RefreshHistoryAsync();
        }
    }

    private void OnNavWorkflows(object? sender, RoutedEventArgs e)
    {
        SetActiveNav(sender, 2);
    }

    private void OnNavDestinations(object? sender, RoutedEventArgs e)
    {
        SetActiveNav(sender, 3);
    }

    private void OnNavShortcuts(object? sender, RoutedEventArgs e)
    {
        SetActiveNav(sender, 4);
    }

    private void OnNavSettings(object? sender, RoutedEventArgs e)
    {
        SetActiveNav(sender, 5);
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void OnMinimizeWindow(object? sender, RoutedEventArgs e)
    {
        // In tiling Wayland compositors (like Niri), traditional minimization is ignored.
        // As a background tray application, ShareX standard behavior is "Minimize to tray".
        Hide();
    }

    private void OnCloseWindow(object? sender, RoutedEventArgs e)
    {
        Hide();
    }
}
