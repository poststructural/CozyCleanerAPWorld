# Handoff: Cozy Cleaner Archipelago (2026-11-03)

## Done this session

Nothing is committed yet; everything is in the working tree under `CozyCleanerAPWorld/`.

- **APWorld** `world/cozy_cleaner/`: 36 levels, 4,395 cleanable units. Default 3,472 locations, 4,525 max, 36 min.
  Options: goal, levels_needed, cleansanity, minigamesanity, lawn_every_cut, stagesanity, shopsanity,
  achievementsanity, toolsanity, money_multiplier. Verified by 32 tests (8,438 subtests) passing.
- **Client** `world/cozy_cleaner/client.py` (CommonContext, file bridge). Two reviews' fixes are applied:
  slot-aware item lookup, full reread of checks.jsonl on connect, and an offset that advances only after a send.
- **Plugin** `plugin/CozyCleanerAP/` and **preloader patcher** `plugin/CozyCleanerAP.Patcher/`. Fixes this session:
  - A hidden persistent `Runner` drives Update/OnGUI, because the game destroys BepInEx's manager object.
  - `Bridge.ParseLevels` is a fallback for the levels list in slot.json (it came back empty, so the level lookup
    failed).
- **Packaging** `tools/package_release.py` writes `release/cozy_cleaner.apworld`, `CozyCleanerAP-Windows.zip`
  and `CozyCleanerAP-Mac.zip`.
- **Docs**: `world/cozy_cleaner/docs/setup_en.md` rewritten for the zips; `CLAUDE.md` added.
- **Reviews**:
  - `docs/codex_review.md`: 7 blocking issues, all fixed.
  - `docs/codex_review2.md`: SHIP, scoped to the packaging, the Mac launcher, ParseLevels and Runner.

### Live Windows test

Run against a local server with seed `AP_35931773795037525048`.

Verified:
- Overlay and connect.
- Money Bundle applied, with its toast (100 coins at multiplier 2).
- Level 2 locked and the tutorial playable.
- `Tutorial - Clean #1` sent from a real pizza drop.
- Achievements sent (Curious Hands, Gentle Touch, First Upgrade).
- A Progressive Dustpan Upgrade received.
- The shop blocks a purchase and shows its price.
- `[CozyAP] totals` matches the table: tutorial game 23+5+1 = 29 against table 29. The table's s1 includes the
  "unpacking" item, which the game counts as stage 3; only the total is used.

Clean-install test: a scratch copy of the Steam game plus the Windows zip loads both patchers and the plugin, and
connects.

## State

- Works on Windows for everything listed above.
- Not live-tested:
  - A completed shop purchase (it needed 250 coins; only 200 were on hand).
  - Stage 1/2 Done.
  - Level completion and its safety net.
  - Mini-game levels.
  - The goal.
  - macOS (no Mac here).
- Build:
  - Plugin and patcher: `dotnet build -c Release` in each project.
  - Release: `python tools/package_release.py`.
  - Tests: see `CLAUDE.md`.

## Remaining

1. Austin playtests on Mac: the Rosetta path, the corlib override and the quarantine step
   (plan: `docs/plans/2026-11-02-cozy-cleaner-ap.md`).
2. A longer Windows playtest: finish the tutorial and level 2, buy one shop upgrade, and play one mini-game.
3. Decide where to publish. Nothing is pushed; that needs Austin's OK.

## Decisions & dead ends

- winhttp.dll is not loaded by this game; it has to be version.dll.
- Unix corlibs from unity.bepinex.dev fail (System.Native). Unity 6 corlibs give a WeakReference NRE. Only
  Unity 2022.3.9 unityjit libs work.
- Unstripped UnityEngine modules break the native bindings. Use the patcher stubs instead.
- The stripped mscorlib is missing `IndexOf(string, StringComparison)` and static `Regex.Matches`.
- Driving the game:
  - Drags need `SetCursorPos`-based moves.
  - Camera rotation needs relative `mouse_event` moves.
  - The screenshot origin is -7px from screen space.

## Continuation prompt

Read `CozyCleanerAPWorld/CLAUDE.md`, `docs/HANDOFF.md` and `docs/SPEC.md`. Then act on Austin's Mac and Windows
playtest feedback. Acceptance: any reported bug is reproduced or explained, fixed in the plugin or world, the
tests still pass (32), and `python tools/package_release.py` rebuilds the zips.
