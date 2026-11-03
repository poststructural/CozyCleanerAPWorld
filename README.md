# Cozy Cleaner Archipelago

An [Archipelago](https://archipelago.gg) world and mod for [Cozy Cleaner](https://store.steampowered.com/app/2742710/).
It runs on Windows and macOS.

Every level is locked behind a key item. Optional "sanity" settings turn almost everything into a check:
- every object you clean
- every mini-game step
- each cleaning stage
- shop upgrades
- achievements

That is 3,472 checks with the default settings and up to 4,525 with everything on.

## Download

Get the latest files from [Releases](../../releases):

- `cozy_cleaner.apworld`: the world. Install it in Archipelago.
- `CozyCleanerAP-Windows.zip` or `CozyCleanerAP-Mac.zip`: the mod. Extract it into the game folder.

Setup steps are in [setup_en.md](world/cozy_cleaner/docs/setup_en.md).

## Options

| Option | What it does |
|---|---|
| goal | Clear the final level, or clear a number of levels (`levels_needed`) |
| cleansanity | Checks for every cleaned object, for milestones only, or off |
| minigamesanity | Checks for every mini-game step, for milestones only, or off |
| lawn_every_cut | Every lawn tile in the lawn level is a check (adds about 1,100) |
| stagesanity | A check when each cleaning stage of a level is done |
| shopsanity | Shop upgrades are checks; upgrades arrive as Progressive items |
| achievementsanity | All 20 achievements are checks |
| toolsanity | Dustpan, Mop and Vacuum must be found before you can use them |
| money_multiplier | Scales the coins from Money Bundle filler items |

## Repository layout

- `world/cozy_cleaner/`: the APWorld, including the client (`client.py`).
- `plugin/CozyCleanerAP/`: the BepInEx 5 plugin.
- `plugin/CozyCleanerAP.Patcher/`: a preloader patcher. It adds engine members that BepInEx needs and the game removed.
- `tools/`: game scanners, level-data generator and release packager.
- `docs/SPEC.md`: the contract between the world, the client and the plugin.

## Building

These steps need a local copy of the game in `Cozy Cleaner/`, or `-p:GameDir=...` passed to the build.

1. Build the plugin and the patcher by running `dotnet build -c Release` in each plugin project.
2. Package the release with `python tools/package_release.py`. It needs the BepInEx 5.4.23.3 zips and Unity 2022.3 class libraries in `tools/downloads`. `tools/fetch_unity_classlibs.py` fetches the class libraries.

## Credits

The release zips bundle these third-party components under their own licenses:
- [BepInEx](https://github.com/BepInEx/BepInEx) (LGPL-2.1), including HarmonyX and Mono.Cecil (MIT).
- Unity's Mono class libraries ([Mono](https://github.com/Unity-Technologies/mono), MIT).

Cozy Cleaner is made by Payoff Games. This project is not affiliated with them.
