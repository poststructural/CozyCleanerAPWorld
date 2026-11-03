"""Static data shared by the world: levels (from data/levels.json), tools, achievements, id base."""
from __future__ import annotations

import json
import pkgutil

GAME_NAME = "Cozy Cleaner"
BASE_ID = 0xC0C10000

TOOLS = ["Hand", "Dustpan", "Cloth", "Mop", "Watering Can", "Vacuum"]
# Tools that are only ever required when toolsanity is on (otherwise owned from the start).
SANITY_TOOLS = ("Dustpan", "Mop", "Vacuum")
ALWAYS_TOOLS = ("Cloth", "Watering Can")

ACHIEVEMENTS = [
    "First Steps", "Gentle Touch", "Curious Hands", "Guiding Paw", "First Upgrade", "Perfect Tool",
    "Well Equipped Cleaner", "Novice Cleaner", "Master Cleaner", "Clean City", "Jack Of All Trades",
    "Trash Hunter", "Spotless Corners", "Sparkling Floors", "Flower Friend", "Recycling Master",
    "Repair Master", "Organized Fridge", "Gardener", "Freshly Mowed",
]

MILESTONE_PERCENTS = (25, 50, 75, 100)
MINIGAME_KINDS = ("recycling", "garden", "fridge", "repair", "lawn")
HOUSE_KINDS = ("tutorial", "house")


def _load_levels() -> list[dict]:
    raw = pkgutil.get_data(__package__, "data/levels.json")
    return json.loads(raw.decode("utf-8"))


LEVELS: list[dict] = _load_levels()
