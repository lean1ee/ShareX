#!/usr/bin/env bash
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"

echo "=== Building ShareX for Linux (Wayland) ==="
cd "$ROOT_DIR"

dotnet publish ShareX.Linux/ShareX.Linux.csproj \
    -c Release \
    -r linux-x64 \
    --self-contained false \
    -o "$ROOT_DIR/bin/publish"

echo "=== Build successful! Binary located at $ROOT_DIR/bin/publish/ShareX.Linux ==="
echo ""
echo "To test run:"
echo "  $ROOT_DIR/bin/publish/ShareX.Linux"
echo ""
echo "To install to your local user directory (~/.local/bin):"
echo "  mkdir -p ~/.local/bin"
echo "  ln -sf $ROOT_DIR/bin/publish/ShareX.Linux ~/.local/bin/sharex"
