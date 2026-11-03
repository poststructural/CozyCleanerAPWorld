# Cozy Cleaner Archipelago: plan (2026-11-02)

Goal: an APWorld plus a game mod/client for Cozy Cleaner (Payoff Games) with as many checks as the game
allows, exposed through `-sanity` options (as in the TOTK world), running on Windows and Mac.

## What the game is (from the decompiled code and a scan of all 40 scenes)

- Unity **Mono** build (`Managed/Assembly-CSharp.dll`, not IL2CPP). BepInEx 5 + Harmony works on
  Windows and macOS (the Mac build is also Mono), so one plugin DLL serves both.
- 36 playable level IDs: tutorial (1), 30 house levels, 5 mini-games
  (Recycling = 5, Flower garden = 11, Fridge/placement = 17, Repair = 23, Lawn = 30). Final level is 36.
  Scene to ID map: `research/scenes.json` (from `tools/scan_scenes.py`).
- Level gating is PlayerPrefs `Level_N_Unlocked`; `LevelHouse.CheckLevelStatus` locks any level above the
  highest unlocked one. `level_controller.ShowCompletionUI` fires on completion.
- House levels have three stages in `CleaningProgress.ObjectCleaned(GameObject)`:
  stage 1 = tagged `Dirty` objects (trash, cobwebs, puddles), stage 2 = `dirtySpriteParquet` floor
  patches, stage 3 = unpacking (`DraggableObject.isUnpacking`). Static counts: about 3,300 `Dirty` and
  370 floor patches across the house levels (22 to 269 per level).
- Mini-games have their own progress classes (recycling items, 34 repair pieces, 83 placement items,
  flower spots, 585 lawn grass patches).
- Shop: 6 tools (Hand, Dustpan, Cloth, Mop, Watering Can, Vacuum), each upgrades Stage1 -> 2 -> 3
  (speed x1.2 / x1.4). All six tools are owned from the start.
- 20 achievements, all routed through `AchievementManager.Unlock(id)`.
- Save: `persistentDataPath/save.json` (money, tool stages) plus PlayerPrefs for level unlocks.

## Checks (locations)

| Option | Checks | Approx. count |
|---|---|---|
| always | Complete each level | 36 |
| `stagesanity` | Finish stage 1/2/3 of each house level | ~90 |
| `cleansanity` | `off` / `milestones` (every 10%) / `every_object`: each object cleaned, counted per level ("Level 12 - Item 17") | up to ~3,700 |
| `minigamesanity` | each item sorted / repaired / placed / planted / watered; lawn by 5% chunks (or per patch with `lawn_every_patch`) | ~150 to ~730 |
| `shopsanity` | each tool upgrade bought (6 x 2) | 12 |
| `achievementsanity` | each achievement (tracked by the mod, Steam not required) | 20 |

Maximal seed: roughly 4,600 locations. Object checks are counted per level ("the 17th thing you
cleaned in level 12"), not tied to object names, so duplicate names in scenes do not matter.

## Items

- **Level keys**: one per level (36), or `progressive_levels` (Progressive House) as an option.
  Level 1 (tutorial) and the store are open from the start.
- **Progressive tool upgrades**: 6 tools x 2. Bought upgrades become checks instead.
- `toolsanity` (off by default until verified): Dustpan, Cloth, Mop, Watering Can and Vacuum start
  locked and are items; levels require the tools their scenes use (needs a per-level tool scan).
- Filler: money bundles (scaled by `money_multiplier`), plus optional cozy traps later (e.g. "Dust Storm").
- Goal: `final_level` (finish level 36) or `levels_completed` N.

## Mod and client architecture

Same pattern as Moon Sweepers, which is proven in this project:

- **Plugin** `CozyCleaner.AP` (BepInEx 5, net46/netstandard2.0, Harmony patches):
  - `LevelHouse.CheckLevelStatus` postfix: lock/unlock houses from received level keys.
  - `level_controller.ShowCompletionUI` prefix: send "Complete level N"; stop the vanilla next-level unlock.
  - `CleaningProgress.ObjectCleaned`, stage finish events, mini-game progress classes: send object/stage checks.
  - `ShopToolManager.TryPurchase` / `ToolManager.SetToolStage`: shop checks, and apply received upgrades.
  - `AchievementManager.Unlock` prefix: achievement checks.
  - Small in-game overlay: connection status and "received X from Y" toasts.
  - Logs real per-level object counts so the static counts can be verified.
- **Bridge**: JSON-lines files in a bridge folder (`checks.jsonl`, `items.jsonl`, `slot.json`).
- **Client**: Python `CommonContext` client inside the APWorld ("Cozy Cleaner Client" in the
  Archipelago launcher). Python runs the same on Windows and Mac, so the only per-OS part is the
  BepInEx install (Windows: winhttp proxy; Mac: `run_bepinex.sh` launch option in Steam).
- AP save isolation: the mod points the game at a per-seed save folder so a run does not touch the normal save.

## Layout

```
CozyCleanerAPWorld/
  Cozy Cleaner/            game copy (git-ignored)
  research/                decomp (git-ignored), scenes.json
  tools/                   scan_scenes.py, gen_world_data.py
  plugin/CozyCleaner.AP/   BepInEx 5 plugin (C#)
  world/cozy_cleaner/      the .apworld (options, items, locations, rules, client.py, docs)
  docs/                    plans, HANDOFF.md, INSTALL.md (Windows + Mac)
```

## Work split (after approval)

1. Data: extend `scan_scenes.py` for mini-game item counts and per-level tool needs, then
   `gen_world_data.py` writes `data.py`. (Sonnet subagent; agy sweeps the decomp for any missed hook.)
2. APWorld: options, items, locations, rules, web_world, docs, template YAML; generate 50 seeds
   with all options on and off. (Sonnet subagent.)
3. Plugin: Harmony patches + bridge + overlay. (Sonnet subagent; Opus review of the patches.)
4. Client: adapt the Moon Sweepers client. (Sonnet subagent.)
5. Review: Codex read-only review of the plugin and the world; deepcode critiques option design.
6. Test on Windows: install BepInEx 5 into the game copy, play the tutorial and level 2 against a
   local server, confirm checks and items both ways. Mac: written install steps; Austin tests.

## Risks and open points

- Static object counts may differ from runtime counts (inactive objects, spawned trash). The plugin
  logs runtime counts; any per-level mismatch is fixed in `data.py` before release.
- Apple Silicon Macs: BepInEx 5 needs the game to run as x86_64 (Rosetta) if the Mac build is
  Intel-only. To be checked on Austin's Mac.
- `toolsanity` changes level logic, so it ships off by default until tested.
- Nothing is pushed to GitHub without Austin's OK.
