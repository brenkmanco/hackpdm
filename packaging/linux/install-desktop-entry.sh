#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
APP_DIR="${1:-$SCRIPT_DIR}"
DESKTOP_DIR="${XDG_DATA_HOME:-$HOME/.local/share}/applications"
ICON_DIR="${XDG_DATA_HOME:-$HOME/.local/share}/icons/hicolor/256x256/apps"

mkdir -p "$DESKTOP_DIR" "$ICON_DIR"

if [ -f "$APP_DIR/Assets/Square150x150Logo.scale-200.png" ]; then
  cp "$APP_DIR/Assets/Square150x150Logo.scale-200.png" "$ICON_DIR/hackpdm.png"
fi

sed "s|^Exec=HackPDM$|Exec=\"$APP_DIR/HackPDM\"|" "$SCRIPT_DIR/hackpdm.desktop" \
  > "$DESKTOP_DIR/hackpdm.desktop"
chmod +x "$DESKTOP_DIR/hackpdm.desktop"

if command -v update-desktop-database >/dev/null 2>&1; then
  update-desktop-database "$DESKTOP_DIR" || true
fi

echo "Installed HackPDM desktop entry at $DESKTOP_DIR/hackpdm.desktop"
