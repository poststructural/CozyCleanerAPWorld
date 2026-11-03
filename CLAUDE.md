# Cozy Cleaner Archipelago

APWorld (`world/cozy_cleaner`, Python) plus a BepInEx 5 plugin (`plugin/CozyCleanerAP`, C#) for Cozy Cleaner
(Steam app 2742710, Unity 2022.3.9f1 Mono). The binding contract between them is `docs/SPEC.md`. Current state
is in `docs/HANDOFF.md`.

## Rules

- Never modify the real Steam install (`C:\Steam\steamapps\common\Cozy Cleaner`). Test in the git-ignored copy
  `Cozy Cleaner/` here, or in a fresh scratch copy.
- The game's managed libraries are stripped. The plugin compiles against the game's own `Managed` dlls, so a
  missing API is a build error, not a runtime surprise. Common gaps: `string.IndexOf(string, StringComparison)`,
  static `Regex.Matches`, Newtonsoft deserialization. Use `JsonUtility` and plain string code.
- Never override UnityEngine modules with unstripped copies: the native engine is stripped to match, so the
  bindings break. Only the Mono class libraries go in `BepInEx/corlib_only`.
- The game destroys BepInEx's manager object, so plugin `Update`/`OnGUI` never run. Per-frame work goes through
  the hidden `Runner` object in `Plugin.cs`.
- Don't commit or push without Austin's OK.

## Build and test

- Plugin: `dotnet build -c Release` in `plugin/CozyCleanerAP` and `plugin/CozyCleanerAP.Patcher` (outputs to
  `release/`). Close the game first or the copy fails.
- Release zips and apworld: `python tools/package_release.py` (needs `tools/downloads`, see its docstring).
- World tests: copy the AP core (`vendor/Archipelago`) to `%TEMP%\cozy-ap-tests`, junction
  `worlds\cozy_cleaner` to `world/cozy_cleaner`, then
  `AP_TEST_WORLDS=cozy_cleaner python -m pytest worlds/cozy_cleaner/test -q` with the vendor venv.
- Level data: `python tools/gen_world_data.py` regenerates `world/cozy_cleaner/data/levels.json` from
  `research/scenes.json` and `research/minigames.json` (UnityPy scans of the game).
