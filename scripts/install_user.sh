#!/usr/bin/env bash
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"

echo "=== 1. Building ShareX for Linux (Wayland, Release) ==="
cd "$ROOT_DIR"

dotnet publish ShareX.Linux/ShareX.Linux.csproj \
    -c Release \
    -r linux-x64 \
    --self-contained false \
    -o "$ROOT_DIR/bin/publish"

echo "=== 2. Creating User Directories ==="
mkdir -p "$HOME/.local/bin"
mkdir -p "$HOME/.local/share/applications"
mkdir -p "$HOME/.local/share/icons/hicolor/256x256/apps"
mkdir -p "$HOME/.config/systemd/user"

echo "=== 3. Installing Binaries, Desktop Entry & Icons ==="
ln -sf "$ROOT_DIR/bin/publish/ShareX.Linux" "$HOME/.local/bin/sharex"
cp -f "$ROOT_DIR/packaging/sharex.desktop" "$HOME/.local/share/applications/sharex.desktop"
cp -f "$ROOT_DIR/packaging/sharex.png" "$HOME/.local/share/icons/hicolor/256x256/apps/sharex.png"
cp -f "$ROOT_DIR/packaging/systemd/sharex.service" "$HOME/.config/systemd/user/sharex.service"

if command -v update-desktop-database >/dev/null 2>&1; then
    update-desktop-database "$HOME/.local/share/applications" 2>/dev/null || true
fi

echo "=== 4. Stopping Any Standalone Instances ==="
systemctl --user stop sharex.service 2>/dev/null || true
pkill -x sharex 2>/dev/null || true
pkill -f "ShareX.Linux" 2>/dev/null || true
sleep 1

echo "=== 5. Enabling & Starting systemd --user Service ==="
systemctl --user daemon-reload
systemctl --user enable --now sharex.service

sleep 1
if systemctl --user is-active --quiet sharex.service; then
    echo "✓ sharex.service is ACTIVE and running in background!"
else
    echo "⚠ Warning: sharex.service status:"
    systemctl --user status sharex.service --no-pager || true
fi

echo ""
echo "=== 6. Verifying IPC Socket Ping ==="
"$HOME/.local/bin/sharex" ping || true

echo ""
echo "=========================================================="
echo " ShareX successfully installed to ~/.local!"
echo " Managed via: systemctl --user status sharex.service"
echo "=========================================================="
