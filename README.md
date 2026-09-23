# ShareX for Linux (Wayland Port)

<div align="center">
  <img src="packaging/sharex.png" alt="ShareX Linux Logo" width="128" height="128" />
  <h3>Native Screen Capture, Annotation, Recording & Productivity Tool for Wayland</h3>

  [![Platform](https://img.shields.io/badge/Platform-Linux%20%7C%20Wayland-blue.svg)](#system-requirements)
  [![Runtime](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com/)
  [![Compositors](https://img.shields.io/badge/Supports-Niri%20%7C%20Hyprland%20%7C%20Sway%20%7C%20GNOME%20%7C%20KDE-green.svg)](#compositor-configuration)
  [![License](https://img.shields.io/badge/License-GPLv3-brightgreen.svg)](./LICENSE.txt)
</div>

---

**ShareX for Linux** is an advanced, lightweight screen capture, recording, annotation, and sharing application tailored natively for modern Linux Wayland environments (**Niri**, **Hyprland**, **Sway**, **GNOME**, **KDE Plasma**). 

It brings the rich productivity workflow of the legendary Windows ShareX to Linux without compromises: instant freeze-frame region captures, rich vector annotations, built-in image editing, GIF/MP4 recording, dynamic theme synchronization, and modular after-capture task automation.

---

## Key Features

- **Blazing-Fast Wayland Capture**:
  - Direct screencopy integration via `wlr-screencopy-v1` and `grim`.
  - Interactive freeze-frame region capture with magnifying loupe and live color picker.
  - Active monitor, full desktop, and custom crop area captures.

- **Built-in Annotation & Image Editor**:
  - Full-featured canvas with undo/redo history.
  - Shapes, arrows, numbered step badges, freehand brush, and speech bubbles.
  - Privacy tools: blur, pixelation, magnification, and highlighter.
  - Native color emoji sticker support powered by system fonts (`Noto Color Emoji`).
  - Image effects: borders, shadow drop, color adjust, and filters.

- **Screen & Animated GIF Recording**:
  - Hardware-accelerated region and screen video recording via `wf-recorder` and `ffmpeg`.
  - High-quality GIF generation with palette optimization.

- **Adaptive Theming Engine**:
  - **Noctalia-shell Integration**: Automatically synchronizes in real time with `~/.config/noctalia/colors.json` (e.g. *Kanagawa*, *Catppuccin*, *Tokyo Night*).
  - **XDG Desktop Portal**: Listens to system-wide Dark/Light preference and accent colors.
  - **Presets**: Includes built-in palettes (*Kanagawa*, *Catppuccin Mocha/Latte*, *Tokyo Night*, *Nord*, *Classic*).

- **Productivity & Sharing**:
  - Seamless Wayland clipboard integration (`wl-clipboard`) supporting both raw PNG buffers and text.
  - Pin images directly to screen as floating, borderless reference windows.
  - Built-in history viewer with quick preview, re-editing, and deletion.
  - Custom uploader support (`.sxcu` format) and cloud destinations (Imgur, S3, custom APIs).

- **Daemon & Background Service**:
  - Runs efficiently in the background via a systemd user service (`sharex.service`).
  - Lightning-fast CLI commands via low-latency UNIX domain socket IPC.
  - StatusNotifierItem tray menu for quick actions.

---

## System Requirements

### Runtime Dependencies

| Package | Purpose | Arch / CachyOS | Fedora | Ubuntu / Debian |
| :--- | :--- | :--- | :--- | :--- |
| **.NET Runtime** | Core application runtime | `dotnet-runtime>=10.0` | `dotnet-runtime-10.0` | `dotnet-runtime-10.0` |
| **grim** | Fast Wayland capture backend | `grim` | `grim` | `grim` |
| **wf-recorder** | Screen video & GIF recorder | `wf-recorder` | `wf-recorder` | `wf-recorder` |
| **ffmpeg** | Video encoding & GIF palette gen | `ffmpeg` | `ffmpeg` | `ffmpeg` |
| **wl-clipboard** | Wayland clipboard manager | `wl-clipboard` | `wl-clipboard` | `wl-clipboard` |
| **libnotify** | Desktop notification popups | `libnotify` | `libnotify` | `libnotify-bin` |
| **noto-fonts-emoji** | Full-color emoji annotations | `noto-fonts-emoji` | `google-noto-emoji-fonts` | `fonts-noto-color-emoji` |
| **slurp** *(optional)* | Fallback CLI region selector | `slurp` | `slurp` | `slurp` |

---

## Linux Installation Guide

### Method 1: Automated Local User Install (Recommended)

This method builds the application from source in Release mode and installs it into `~/.local` with a persistent `systemd --user` background service.

```bash
# 1. Install prerequisites (Arch / CachyOS example)
sudo pacman -S --needed dotnet-sdk grim wf-recorder ffmpeg wl-clipboard libnotify noto-fonts-emoji

# 2. Clone the repository
git clone https://github.com/lean1ee/ShareX.git ~/projects/sharex_port
cd ~/projects/sharex_port

# 3. Run the automated installer
./scripts/install_user.sh
```

#### What `install_user.sh` does:
1. Compiles `ShareX.Linux` in Release mode (`linux-x64`) into `./bin/publish`.
2. Creates a symlink at `~/.local/bin/sharex`.
3. Installs desktop entry to `~/.local/share/applications/sharex.desktop`.
4. Installs application icons to `~/.local/share/icons/hicolor/256x256/apps/sharex.png`.
5. Configures and starts the systemd user service `sharex.service` (`systemctl --user enable --now sharex.service`).
6. Verifies daemon connectivity using `sharex ping`.

---

### Method 2: Arch Linux PKGBUILD (System-wide Install)

If you use Arch Linux, EndeavourOS, or CachyOS, you can install ShareX system-wide using the provided `PKGBUILD`:

```bash
cd packaging
makepkg -si
```

This installs the binary to `/usr/bin/sharex`, registers the desktop file in `/usr/share/applications`, and provisions the systemd user service in `/usr/lib/systemd/user/sharex.service`.

Enable the background service:
```bash
systemctl --user daemon-reload
systemctl --user enable --now sharex.service
```

---

### Method 3: Manual Build and Installation

If you prefer full control over file paths:

```bash
# 1. Publish Release binary
dotnet publish ShareX.Linux/ShareX.Linux.csproj \
    -c Release \
    -r linux-x64 \
    --self-contained false \
    -o ./bin/publish

# 2. Symlink binary to PATH
mkdir -p ~/.local/bin
ln -sf "$(pwd)/bin/publish/ShareX.Linux" ~/.local/bin/sharex

# 3. Install Desktop Shortcut & Icon
mkdir -p ~/.local/share/applications ~/.local/share/icons/hicolor/256x256/apps
cp packaging/sharex.desktop ~/.local/share/applications/
cp packaging/sharex.png ~/.local/share/icons/hicolor/256x256/apps/
update-desktop-database ~/.local/share/applications 2>/dev/null || true

# 4. Install & Start Systemd Service
mkdir -p ~/.config/systemd/user
cp packaging/systemd/sharex.service ~/.config/systemd/user/
systemctl --user daemon-reload
systemctl --user enable --now sharex.service
```

---

## Daemon & CLI Commands

ShareX runs as an event-driven background service listening to a UNIX socket (`$XDG_RUNTIME_DIR/sharex/sharex.sock`). Trigger captures or windows by calling the `sharex` command from your terminal, scripts, or hotkey manager:

```bash
# Capture commands
sharex --capture-region         # Interactive region selection -> execute workflows
sharex --capture-region-edit    # Region selection -> opens directly in Image Editor
sharex --capture-screen         # Instant full screen capture

# Screen recording
sharex --record-region          # Record selected area to MP4 (hardware accelerated)
sharex --record-gif             # Record selected area to animated GIF

# Windows & UI
sharex --open-editor            # Open blank Image Editor
sharex --open-editor /path/img  # Open existing image in Image Editor
sharex show-window              # Show main ShareX dashboard
sharex hide-window              # Minimize main dashboard to tray
sharex toggle-window            # Toggle main dashboard visibility

# Service management
sharex ping                     # Test if daemon is running (returns "OK")
sharex exit                     # Cleanly shut down ShareX service
```

---

## Compositor Configuration

### 1. Niri

Add hotkeys to `~/.config/niri/cfg/keybinds.kdl`:
*(Note: `repeat=false` is essential to prevent multiple captures when holding a key down)*

```kdl
binds {
    Print       repeat=false hotkey-overlay-title="ShareX: Capture Region"        { spawn "sharex" "--capture-region"; }
    Mod+Shift+S repeat=false hotkey-overlay-title="ShareX: Capture Region & Edit" { spawn "sharex" "--capture-region-edit"; }
    Shift+Print repeat=false hotkey-overlay-title="ShareX: Capture Region & Edit" { spawn "sharex" "--capture-region-edit"; }
    Ctrl+Print  repeat=false hotkey-overlay-title="ShareX: Capture Full Screen"   { spawn "sharex" "--capture-screen"; }
    Alt+Print   repeat=false hotkey-overlay-title="ShareX: Record Region (Video)" { spawn "sharex" "--record-region"; }
    Mod+Print   repeat=false hotkey-overlay-title="ShareX: Record Region (GIF)"   { spawn "sharex" "--record-gif"; }
}
```

Add window rules to `~/.config/niri/cfg/rules.kdl`:

```kdl
// Pinned image annotations: floating, borderless
window-rule {
    match app-id=r#"^([Ss]hare[Xx].*|sharex)$"# title=r#"^Pinned Image.*"#
    open-floating true
    geometry-corner-radius 0
    clip-to-geometry false
}

// Dialogs and color pickers: floating
window-rule {
    match app-id=r#"^([Ss]hare[Xx].*|sharex)$"# title=r#"^(Screen Color Picker|Color Picker|Insert Image|New Image|Select Area).*"#
    open-floating true
}

// Main Window and Image Editor: open as tiling columns in the Niri ribbon
window-rule {
    match app-id=r#"^([Ss]hare[Xx].*|sharex)$"# title=r#"^(ShareX \(Wayland Linux\)|ShareX - Image editor).*"#
    open-floating false
}
```

---

### 2. Hyprland

Add keybinds and rules to `~/.config/hypr/hyprland.conf`:

```ini
# Keybindings
bind = , Print, exec, sharex --capture-region
bind = $mainMod SHIFT, S, exec, sharex --capture-region-edit
bind = SHIFT, Print, exec, sharex --capture-region-edit
bind = CTRL, Print, exec, sharex --capture-screen
bind = ALT, Print, exec, sharex --record-region
bind = $mainMod, Print, exec, sharex --record-gif

# Window Rules
windowrule = float, class:^(ShareX.*)$
windowrule = float, class:^(sharex.*)$
windowrule = center, class:^(ShareX.*)$
windowrule = pin, title:^(Pinned Image.*)$
windowrule = noborder, title:^(Pinned Image.*)$
```

---

### 3. Sway

Add to `~/.config/sway/config`:

```ini
# Keybindings
bindsym Print exec sharex --capture-region
bindsym Mod4+Shift+s exec sharex --capture-region-edit
bindsym Shift+Print exec sharex --capture-region-edit
bindsym Ctrl+Print exec sharex --capture-screen
bindsym Mod1+Print exec sharex --record-region
bindsym Mod4+Print exec sharex --record-gif

# Rules
for_window [app_id="ShareX.*"] floating enable
for_window [title="Pinned Image.*"] floating enable, border none, sticky enable
```

---

## Configuration & Data Paths

ShareX stores configuration and runtime files following the XDG Base Directory specification:

| Item | Path |
| :--- | :--- |
| **Settings File** | `~/.config/ShareX/sharex_settings.json` |
| **History Database** | `~/.config/ShareX/history.json` |
| **Default Screenshots** | `~/Pictures/Screenshots/` |
| **Custom Uploaders** | `~/.config/ShareX/Uploaders/*.sxcu` |
| **UNIX IPC Socket** | `/run/user/<UID>/sharex/sharex.sock` |
| **Log Files** | `~/.config/ShareX/Logs/` |

---

## Service Management

ShareX is managed via standard systemd user commands:

```bash
# Check service health and logs
systemctl --user status sharex.service

# View live application logs
journalctl --user -u sharex.service -f

# Restart daemon
systemctl --user restart sharex.service

# Stop daemon
systemctl --user stop sharex.service
```

---

## Troubleshooting

### 1. `sharex` command fails with "Cannot connect to daemon"
Make sure the user service is running:
```bash
systemctl --user status sharex.service
```
If it is not active, start it:
```bash
systemctl --user start sharex.service
```

### 2. Wayland Region Capture fails
Ensure `grim` is installed and available in `$PATH`:
```bash
which grim
```
In Wayland compositors with security restrictions, verify that `wlr-screencopy` or `xdg-desktop-portal` has screen capture permissions enabled.

### 3. Emojis render as blank boxes
Install the Noto Color Emoji font package:
```bash
# Arch / CachyOS
sudo pacman -S noto-fonts-emoji

# Fedora
sudo dnf install google-noto-emoji-fonts

# Ubuntu / Debian
sudo apt install fonts-noto-color-emoji
```

---

## License

ShareX for Linux is licensed under the [GNU General Public License v3.0 (GPLv3)](./LICENSE.txt).
Original ShareX is copyright (c) 2007-2026 ShareX Team.
Linux / Wayland port and integrations maintained by the ShareX Linux contributors.
