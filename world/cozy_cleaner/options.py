from dataclasses import dataclass

from Options import Choice, DefaultOnToggle, OptionGroup, PerGameCommonOptions, Range, Toggle


class Goal(Choice):
    """What finishes the seed.

    final_level: complete Level 36.
    levels_completed: complete `levels_needed` different levels (the tutorial does not count)."""

    display_name = "Goal"
    option_final_level = 0
    option_levels_completed = 1
    default = 0


class LevelsNeeded(Range):
    """With the levels_completed goal, how many levels (not counting the tutorial) you must complete."""

    display_name = "Levels Needed"
    range_start = 1
    range_end = 35
    default = 25


class Cleansanity(Choice):
    """Whether cleaning objects in the tutorial and house levels are checks.

    off: no object checks.
    milestones: 25%, 50%, 75% and 100% of each level.
    every_object: every single object."""

    display_name = "Cleansanity"
    option_off = 0
    option_milestones = 1
    option_every_object = 2
    default = 2


class Minigamesanity(Choice):
    """Whether progress units in the five mini-games (recycling, garden, fridge, repair, lawn) are checks.

    off: no unit checks.
    milestones: 25%, 50%, 75% and 100% of each mini-game.
    every_unit: every unit (the lawn is grouped in tens unless lawn_every_cut is on)."""

    display_name = "Minigamesanity"
    option_off = 0
    option_milestones = 1
    option_every_unit = 2
    default = 2


class LawnEveryCut(Toggle):
    """With minigamesanity every_unit, make each of the 1170 lawn cuts its own check instead of groups of ten."""

    display_name = "Lawn Every Cut"


class Stagesanity(DefaultOnToggle):
    """Finishing stage 1 and stage 2 of the tutorial and each house level is a check."""

    display_name = "Stagesanity"


class Shopsanity(DefaultOnToggle):
    """Buying the first and second upgrade of each tool in the shop is a check, and the upgrades are shuffled."""

    display_name = "Shopsanity"


class Achievementsanity(DefaultOnToggle):
    """Unlocking each of the 20 achievements is a check."""

    display_name = "Achievementsanity"


class Toolsanity(Toggle):
    """Shuffle the Dustpan, Mop and Vacuum into the item pool. Off: you own them from the start."""

    display_name = "Toolsanity"


class MoneyMultiplier(Range):
    """Each Money Bundle gives 50 times this much money."""

    display_name = "Money Multiplier"
    range_start = 1
    range_end = 10
    default = 1


OPTION_GROUPS = [
    OptionGroup("Goal", [Goal, LevelsNeeded]),
    OptionGroup("Location Checks", [Cleansanity, Minigamesanity, LawnEveryCut, Stagesanity, Shopsanity,
                                    Achievementsanity]),
    OptionGroup("Items", [Toolsanity, MoneyMultiplier]),
]


@dataclass
class CozyCleanerOptions(PerGameCommonOptions):
    goal: Goal
    levels_needed: LevelsNeeded
    cleansanity: Cleansanity
    minigamesanity: Minigamesanity
    lawn_every_cut: LawnEveryCut
    stagesanity: Stagesanity
    shopsanity: Shopsanity
    achievementsanity: Achievementsanity
    toolsanity: Toolsanity
    money_multiplier: MoneyMultiplier
