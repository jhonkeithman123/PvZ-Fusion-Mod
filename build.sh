#!/usr/bin/env bash
#
# Builds Blooms_Mods.dll and copies it into the game's Mods folder.
#
# Usage:
#   ./build.sh                 # build + install into the game
#   ./build.sh --no-install    # build only
#
# Environment overrides:
#   DOTNET       path to dotnet.exe   (default: /mnt/c/Program Files/dotnet/dotnet.exe)
#   PVZ_GAME_DIR game's "Game Files"  (default: the known install path)
#   CONFIG       Debug|Release        (default: Release)
#
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_NAME="$(basename "$SCRIPT_DIR")"

# --- configuration ---
DOTNET="${DOTNET:-/mnt/c/Program Files/dotnet/dotnet.exe}"
GAME_DIR="${PVZ_GAME_DIR:-/mnt/c/Users/131fgh/Desktop/PvZ.Fusion.Multi-lang.4.0.5.2.Alpha.by.Blooms/Game Files}"
CONFIG="${CONFIG:-Release}"

DLL="$SCRIPT_DIR/bin/$CONFIG/net6.0/Blooms_Mods.dll"
TARGET="$GAME_DIR/Mods/Blooms_Mods.dll"

# --- build ---
echo "==> Building $PROJECT_NAME ($CONFIG)"
cd "$SCRIPT_DIR"
"$DOTNET" build -c "$CONFIG"

if [[ ! -f "$DLL" ]]; then
    echo "!! Build reported success but $DLL is missing." >&2
    exit 1
fi

# --- install ---
if [[ "${1:-}" == "--no-install" ]]; then
    echo "==> Done (not installed): $DLL"
    exit 0
fi

if [[ ! -d "$GAME_DIR/Mods" ]]; then
    echo "!! Game Mods folder not found: $GAME_DIR/Mods" >&2
    echo "   Set PVZ_GAME_DIR to your game's 'Game Files' directory." >&2
    exit 1
fi

if ! cp "$DLL" "$TARGET" 2>/dev/null; then
    echo "!! Could not install: $TARGET is locked by a running process." >&2
    echo "   Close the game (PlantsVsZombiesRH.exe) and run again." >&2
    exit 1
fi

echo "==> Built:   $DLL"
echo "==> Installed -> $TARGET"
