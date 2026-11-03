#!/bin/bash
# Cozy Cleaner Archipelago: macOS installer.
# Double-click this file (or run it in Terminal) from the folder you extracted CozyCleanerAP-Mac.zip into.
#   ./"Install Cozy Cleaner AP.command"                  install into the Steam game folder it finds
#   ./"Install Cozy Cleaner AP.command" "/path/to/game"  install into that folder
#   ./"Install Cozy Cleaner AP.command" --uninstall      remove the mod from the game folder
set -u

SRC="$(cd "$(dirname "$0")" && pwd -P)"
MOD_FILES=("BepInEx" "run_bepinex.sh" "libdoorstop.dylib" ".doorstop_version")

say() { printf '%s\n' "$*"; }
fail() { say ""; say "ERROR: $*"; say ""; read -r -p "Press Return to close." _; exit 1; }

find_game() {
    local roots=("$HOME/Library/Application Support/Steam")
    local vdf="$HOME/Library/Application Support/Steam/steamapps/libraryfolders.vdf"
    if [ -f "$vdf" ]; then
        while IFS= read -r p; do roots+=("$p"); done < <(sed -n 's/^[[:space:]]*"path"[[:space:]]*"\(.*\)"/\1/p' "$vdf")
    fi
    for r in "${roots[@]}"; do
        local d="$r/steamapps/common/Cozy Cleaner"
        if [ -d "$d" ]; then printf '%s\n' "$d"; return 0; fi
    done
    return 1
}

pick_game() {
    osascript -e 'POSIX path of (choose folder with prompt "Select the Cozy Cleaner game folder (the one that contains Cozy Cleaner.app)")' 2>/dev/null
}

UNINSTALL=0
GAME=""
for a in "$@"; do
    case "$a" in
        --uninstall) UNINSTALL=1 ;;
        *) GAME="$a" ;;
    esac
done

say "Cozy Cleaner Archipelago installer"
say "=================================="

if [ -z "$GAME" ]; then
    if ls "$SRC"/*.app >/dev/null 2>&1; then
        GAME="$SRC"   # the zip was extracted straight into the game folder
    else
        GAME="$(find_game)" || GAME="$(pick_game)"
    fi
fi
GAME="${GAME%/}"
[ -n "$GAME" ] && [ -d "$GAME" ] || fail "Could not find the Cozy Cleaner game folder. Run this again and pick it, or pass its path."

APP="$(ls -d "$GAME"/*.app 2>/dev/null | head -n 1)"
[ -n "$APP" ] || fail "No .app found in $GAME. Pick the folder that contains Cozy Cleaner.app."
say "Game folder: $GAME"

if [ "$UNINSTALL" = 1 ]; then
    for f in "${MOD_FILES[@]}"; do rm -rf "${GAME:?}/$f"; done
    say ""
    say "Removed the mod. In Steam, clear the game's Launch Options (Properties > General)."
    read -r -p "Press Return to close." _
    exit 0
fi

# Copy the mod files unless the zip was extracted into the game folder already.
if [ "$SRC" != "$(cd "$GAME" && pwd -P)" ]; then
    for f in "${MOD_FILES[@]}"; do
        [ -e "$SRC/$f" ] || fail "Missing $f next to this installer. Extract the whole zip and try again."
    done
    if [ -d "$GAME/BepInEx" ]; then
        # keep the player's BepInEx config and logs, replace everything we ship
        rm -rf "$GAME/BepInEx/core" "$GAME/BepInEx/corlib_only" "$GAME/BepInEx/patchers/CozyCleanerAP.Patcher.dll" \
               "$GAME/BepInEx/plugins/CozyCleanerAP"
    fi
    ditto "$SRC/BepInEx" "$GAME/BepInEx" || fail "Could not copy BepInEx into $GAME."
    for f in run_bepinex.sh libdoorstop.dylib .doorstop_version; do
        cp -f "$SRC/$f" "$GAME/$f" || fail "Could not copy $f into $GAME."
    done
    say "Copied the mod files."
fi

# The launcher script names the app; match whatever the game's .app is called.
APP_NAME="$(basename "$APP")"
sed -i '' "s|^executable_name=.*|executable_name=\"$APP_NAME\"|" "$GAME/run_bepinex.sh"
chmod +x "$GAME/run_bepinex.sh"

# Downloaded files are quarantined, and macOS refuses to inject a quarantined libdoorstop.dylib.
xattr -dr com.apple.quarantine "$GAME/BepInEx" "$GAME/run_bepinex.sh" "$GAME/libdoorstop.dylib" 2>/dev/null
say "Cleared the download quarantine flag on the mod files."

# BepInEx for macOS is Intel-only, so Apple Silicon Macs need Rosetta.
if [ "$(uname -m)" = "arm64" ]; then
    if arch -x86_64 /usr/bin/true 2>/dev/null; then
        say "Rosetta is installed (needed on Apple Silicon)."
    else
        say ""
        say "This Mac needs Rosetta to run the mod. Installing it now (you may be asked for your password)..."
        softwareupdate --install-rosetta --agree-to-license || say "Rosetta install failed. Install it later with: softwareupdate --install-rosetta"
    fi
fi

LAUNCH="\"$GAME/run_bepinex.sh\" %command%"
printf '%s' "$LAUNCH" | pbcopy
say ""
say "Done. One step left, in Steam:"
say "  1. Right-click Cozy Cleaner > Properties > General."
say "  2. Click in Launch Options and paste (Cmd+V). It is already on your clipboard:"
say ""
say "     $LAUNCH"
say ""
say "Then start the game from Steam. The top-left corner shows \"AP: not connected\" when the mod is running."
read -r -p "Press Return to close." _
