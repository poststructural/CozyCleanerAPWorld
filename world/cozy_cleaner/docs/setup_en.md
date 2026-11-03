# Cozy Cleaner setup guide

## Requirements

- Cozy Cleaner on Steam (Windows or macOS).
- [Archipelago](https://github.com/ArchipelagoMW/Archipelago/releases) with `cozy_cleaner.apworld` installed.
- The mod zip for your system: `CozyCleanerAP-Windows.zip` or `CozyCleanerAP-Mac.zip`. Each one already holds
  BepInEx 5.4.23.3, the Cozy Cleaner plugin, and the extra files this game needs (see "What the mod zip
  contains" below), so you don't need to download BepInEx yourself.

To find the game folder, right-click Cozy Cleaner in Steam and choose **Manage > Browse local files**.

## Install on Windows

1. Extract `CozyCleanerAP-Windows.zip` into the game folder, so that `version.dll` and the `BepInEx` folder
   sit next to `Cozy Cleaner.exe`.
2. That's it. Start the game from Steam as usual. The top-left corner shows `AP: not connected` when the mod
   is running.

To remove the mod, delete `version.dll`, `doorstop_config.ini`, `.doorstop_version`, `changelog.txt` and the
`BepInEx` folder.

## Install on macOS

1. Extract `CozyCleanerAP-Mac.zip` into the game folder, so that `run_bepinex.sh` and the `BepInEx` folder sit
   next to `Cozy Cleaner.app`.
2. Open Terminal in that folder and run:
   ```
   chmod +x run_bepinex.sh
   xattr -dr com.apple.quarantine .
   ```
   The second line stops macOS from blocking the downloaded `libdoorstop.dylib`.
3. In Steam, open the game's **Properties > General > Launch Options** and enter
   `"/full/path/to/run_bepinex.sh" %command%`, using the real path to the script (you can drag the file into
   Terminal to see it).
4. Start the game from Steam. The top-left corner shows `AP: not connected` when the mod is running.

BepInEx for macOS is Intel (x86_64) only. On Apple Silicon Macs the script starts the game under Rosetta
automatically; install Rosetta first if macOS asks for it.

## Play

1. Install the `.apworld` by double-clicking it (or place it in the Archipelago `custom_worlds` folder), then
   generate a seed with `Cozy Cleaner` as the game (a template yaml is in the Archipelago Launcher's
   **Generate Template Options**).
2. Open the **Archipelago Launcher**, start **Cozy Cleaner Client**, and connect to the server with your slot
   name. Do this **before** starting the game.
3. Start the game. The overlay changes to `AP: connected to <seed>`. Press **New Game** (or **Continue** on
   later sessions). The mod keeps a separate save for each seed and slot, so your normal save is untouched.
4. Level keys, tools, upgrades and money arrive as items, with a short message under the overlay. A locked level
   shows a greyed-out Play button until its key arrives.

If you connect the client after the game is already running, or switch to another seed, the overlay asks you to
restart the game. That keeps one seed's progress from leaking into another's save.

## What the mod zip contains

Cozy Cleaner ships trimmed-down copies of .NET and Unity libraries that stock BepInEx can't start with, so the
zip adds:

- `BepInEx/corlib_only/`: Unity 2022.3's full Mono class libraries, loaded in place of the game's trimmed ones.
  `doorstop_config.ini` (Windows) or `run_bepinex.sh` (Mac) points at this folder.
- `BepInEx/patchers/CozyCleanerAP.Patcher.dll`: adds two small engine members that BepInEx expects and the
  game removed.
- `BepInEx/config/BepInEx.cfg`: turns off BepInEx's Unity log listener, which also needs a removed member.
- On Windows the BepInEx loader is named `version.dll` instead of `winhttp.dll`, because this game doesn't load
  `winhttp.dll`.

## Bridge folder

The client and plugin exchange files in `~/CozyCleanerAP/<seed>_<slot>/` (`current.txt` in `~/CozyCleanerAP/`
names the active folder): `slot.json`, `items.jsonl`, `checks.jsonl`, `checked.json`, `status.json`, plus the
seed's own save in `save/`. Deleting a seed's folder after the game is finished is safe.
