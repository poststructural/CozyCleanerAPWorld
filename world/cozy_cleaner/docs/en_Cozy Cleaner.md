# Cozy Cleaner

## Where is the options page?

The [player options page for this game](../player-options) contains all the options you need to configure and
export a config file.

## What does randomization do to this game?

Every level of Cozy Cleaner is locked behind its own **Level Key**. Only the tutorial is open. Some levels also
need a tool you have to find: the **Cloth** and the **Watering Can** are always shuffled, and with `toolsanity`
so are the **Dustpan**, **Mop** and **Vacuum**. With `shopsanity`, the first and second upgrade of every tool
is a check, and the upgrades themselves (`Progressive <Tool> Upgrade`) are items. The rest of the pool is
**Money Bundles** (50 money each, times `money_multiplier`).

## What is a check?

- Completing a level.
- Finishing stage 1 or stage 2 of the tutorial and house levels (`stagesanity`).
- Cleaning objects in house levels (`cleansanity`), or progress units in the five mini-games
  (`minigamesanity`, `lawn_every_cut`).
- Buying tool upgrades in the shop (`shopsanity`).
- Unlocking the 20 achievements (`achievementsanity`).

## What is the goal?

Complete Level 36, or complete `levels_needed` different levels (the tutorial does not count).

## What happens when I receive an item?

The client hands it to the game plugin right away: keys unlock levels, tools appear in your tool belt, upgrades
raise the tool level, and money is added to your balance.

## Notes

Archipelago runs use their own save slot, so your normal Cozy Cleaner save is never touched.
