using System;
using Avalonia;
using Avalonia.Media;

namespace ShareX.AvaloniaUI.Theming;

public class ThemePalette
{
    public string Name { get; set; } = "Custom";
    public bool IsDark { get; set; } = true;

    // Backgrounds
    public Color BackgroundMain { get; set; }
    public Color BackgroundPanel { get; set; }
    public Color BackgroundPopup { get; set; }
    public Color BackgroundToolbar { get; set; }

    // Borders & Controls
    public Color Border { get; set; }
    public Color ControlBackground { get; set; }
    public Color ControlBackgroundHover { get; set; }
    public Color ControlBorder { get; set; }
    public Color Separator { get; set; }

    // Text
    public Color Text { get; set; }
    public Color TextSecondary { get; set; }

    // Accent
    public Color Accent { get; set; }
    public Color AccentEnd { get; set; }
    public Color AccentForeground { get; set; }

    // Status
    public Color StatusSuccess { get; set; } = Color.Parse("#79B58A");
    public Color StatusError { get; set; } = Color.Parse("#D07C7C");

    #region Predefined Palettes

    public static ThemePalette Kanagawa => new()
    {
        Name = "Kanagawa",
        IsDark = true,
        BackgroundMain = Color.Parse("#1F1F28"),
        BackgroundPanel = Color.Parse("#2A2A37"),
        BackgroundPopup = Color.Parse("#2A2A37"),
        BackgroundToolbar = Color.Parse("#16161D"),
        Border = Color.Parse("#363646"),
        ControlBackground = Color.Parse("#2A2A37"),
        ControlBackgroundHover = Color.Parse("#363646"),
        ControlBorder = Color.Parse("#363646"),
        Separator = Color.Parse("#363646"),
        Text = Color.Parse("#DCD7BA"),
        TextSecondary = Color.Parse("#727169"),
        Accent = Color.Parse("#76946A"),
        AccentEnd = Color.Parse("#66845A"),
        AccentForeground = Color.Parse("#1F1F28"),
        StatusSuccess = Color.Parse("#98BB6C"),
        StatusError = Color.Parse("#C34043")
    };

    public static ThemePalette CatppuccinMocha => new()
    {
        Name = "Catppuccin Mocha",
        IsDark = true,
        BackgroundMain = Color.Parse("#181825"), // Mantle
        BackgroundPanel = Color.Parse("#1E1E2E"), // Base
        BackgroundPopup = Color.Parse("#1E1E2E"),
        BackgroundToolbar = Color.Parse("#11111B"), // Crust
        Border = Color.Parse("#313244"), // Surface0
        ControlBackground = Color.Parse("#1E1E2E"),
        ControlBackgroundHover = Color.Parse("#313244"),
        ControlBorder = Color.Parse("#313244"),
        Separator = Color.Parse("#313244"),
        Text = Color.Parse("#CDD6F4"), // Text
        TextSecondary = Color.Parse("#A6ADC8"), // Subtext0
        Accent = Color.Parse("#89B4FA"), // Blue
        AccentEnd = Color.Parse("#74C7EC"), // Sapphire
        AccentForeground = Color.Parse("#11111B"),
        StatusSuccess = Color.Parse("#A6E3A1"), // Green
        StatusError = Color.Parse("#F38BA8") // Red
    };

    public static ThemePalette CatppuccinLatte => new()
    {
        Name = "Catppuccin Latte",
        IsDark = false,
        BackgroundMain = Color.Parse("#EFF1F5"), // Base
        BackgroundPanel = Color.Parse("#E6E9EF"), // Mantle
        BackgroundPopup = Color.Parse("#E6E9EF"),
        BackgroundToolbar = Color.Parse("#DCE0E8"), // Crust
        Border = Color.Parse("#CCD0DA"), // Surface0
        ControlBackground = Color.Parse("#FFFFFF"),
        ControlBackgroundHover = Color.Parse("#E6E9EF"),
        ControlBorder = Color.Parse("#CCD0DA"),
        Separator = Color.Parse("#CCD0DA"),
        Text = Color.Parse("#4C4F69"), // Text
        TextSecondary = Color.Parse("#6C6F85"), // Subtext0
        Accent = Color.Parse("#1E66F5"), // Blue
        AccentEnd = Color.Parse("#04A5E5"), // Sky
        AccentForeground = Color.Parse("#FFFFFF"),
        StatusSuccess = Color.Parse("#40A02B"), // Green
        StatusError = Color.Parse("#D20F39") // Red
    };

    public static ThemePalette TokyoNight => new()
    {
        Name = "Tokyo Night",
        IsDark = true,
        BackgroundMain = Color.Parse("#1A1B26"),
        BackgroundPanel = Color.Parse("#16161E"),
        BackgroundPopup = Color.Parse("#16161E"),
        BackgroundToolbar = Color.Parse("#13141C"),
        Border = Color.Parse("#292E42"),
        ControlBackground = Color.Parse("#1F2335"),
        ControlBackgroundHover = Color.Parse("#292E42"),
        ControlBorder = Color.Parse("#292E42"),
        Separator = Color.Parse("#292E42"),
        Text = Color.Parse("#C0CAF5"),
        TextSecondary = Color.Parse("#7AA2F7"),
        Accent = Color.Parse("#7AA2F7"),
        AccentEnd = Color.Parse("#628AE0"),
        AccentForeground = Color.Parse("#1A1B26"),
        StatusSuccess = Color.Parse("#9ECE6A"),
        StatusError = Color.Parse("#F7768E")
    };

    public static ThemePalette Nord => new()
    {
        Name = "Nord",
        IsDark = true,
        BackgroundMain = Color.Parse("#2E3440"), // Polar Night 0
        BackgroundPanel = Color.Parse("#3B4252"), // Polar Night 1
        BackgroundPopup = Color.Parse("#3B4252"),
        BackgroundToolbar = Color.Parse("#242933"),
        Border = Color.Parse("#4C566A"), // Polar Night 3
        ControlBackground = Color.Parse("#3B4252"),
        ControlBackgroundHover = Color.Parse("#434C5E"), // Polar Night 2
        ControlBorder = Color.Parse("#4C566A"),
        Separator = Color.Parse("#4C566A"),
        Text = Color.Parse("#ECEFF4"), // Snow Storm 2
        TextSecondary = Color.Parse("#D8DEE9"), // Snow Storm 0
        Accent = Color.Parse("#88C0D0"), // Frost 1
        AccentEnd = Color.Parse("#81A1C1"), // Frost 2
        AccentForeground = Color.Parse("#2E3440"),
        StatusSuccess = Color.Parse("#A3BE8C"),
        StatusError = Color.Parse("#BF616A")
    };

    public static ThemePalette ClassicDark => new()
    {
        Name = "ShareX Classic Dark",
        IsDark = true,
        BackgroundMain = Color.Parse("#272727"),
        BackgroundPanel = Color.Parse("#242424"),
        BackgroundPopup = Color.Parse("#2E2E2E"),
        BackgroundToolbar = Color.Parse("#222222"),
        Border = Color.Parse("#1F1F1F"),
        ControlBackground = Color.Parse("#2E2E2E"),
        ControlBackgroundHover = Color.Parse("#3A3A3A"),
        ControlBorder = Color.Parse("#1F1F1F"),
        Separator = Color.Parse("#333333"),
        Text = Color.Parse("#D8DADB"),
        TextSecondary = Color.Parse("#A0A0A0"),
        Accent = Color.Parse("#3E83F2"),
        AccentEnd = Color.Parse("#3975D5"),
        AccentForeground = Color.Parse("#FFFFFF"),
        StatusSuccess = Color.Parse("#79B58A"),
        StatusError = Color.Parse("#D07C7C")
    };

    public static ThemePalette ClassicLight => new()
    {
        Name = "ShareX Classic Light",
        IsDark = false,
        BackgroundMain = Color.Parse("#F5F5F7"),
        BackgroundPanel = Color.Parse("#FDFDFD"),
        BackgroundPopup = Color.Parse("#FDFDFD"),
        BackgroundToolbar = Color.Parse("#ECECEE"),
        Border = Color.Parse("#E5E5E8"),
        ControlBackground = Color.Parse("#F5F5F7"),
        ControlBackgroundHover = Color.Parse("#EBEBEF"),
        ControlBorder = Color.Parse("#DBDBDF"),
        Separator = Color.Parse("#E3E3E3"),
        Text = Color.Parse("#4E4E4E"),
        TextSecondary = Color.Parse("#7E7E7E"),
        Accent = Color.Parse("#3E83F2"),
        AccentEnd = Color.Parse("#3975D5"),
        AccentForeground = Color.Parse("#FFFFFF"),
        StatusSuccess = Color.Parse("#4F8A63"),
        StatusError = Color.Parse("#B85F66")
    };

    #endregion
}
