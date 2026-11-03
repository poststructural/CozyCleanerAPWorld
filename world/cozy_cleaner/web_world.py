from BaseClasses import Tutorial
from worlds.AutoWorld import WebWorld

from . import options as cozy_options


class CozyCleanerWebWorld(WebWorld):
    theme = "grass"
    option_groups = cozy_options.OPTION_GROUPS
    tutorials = [
        Tutorial(
            "Multiworld Setup Guide",
            "A guide to setting up Cozy Cleaner for Archipelago.",
            "English",
            "setup_en.md",
            "setup/en",
            ["Jerrod"],
        )
    ]
