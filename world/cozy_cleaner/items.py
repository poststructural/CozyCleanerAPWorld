from __future__ import annotations

from BaseClasses import Item, ItemClassification

from . import data


class CozyCleanerItem(Item):
    game = data.GAME_NAME


MONEY_BUNDLE = "Money Bundle"
MONEY_BASE = 50

KEY_ITEMS = [f"{level['name']} Key" for level in data.LEVELS if level["kind"] != "tutorial"]
CLEARED_ITEMS = [f"{level['name']} Cleared" for level in data.LEVELS]  # events only (no id)
UPGRADE_ITEMS = [f"Progressive {tool} Upgrade" for tool in data.TOOLS]
TOOL_ITEMS = list(data.ALWAYS_TOOLS) + list(data.SANITY_TOOLS)

_ORDER = KEY_ITEMS + ["Cloth", "Watering Can", "Dustpan", "Mop", "Vacuum"] + UPGRADE_ITEMS + [MONEY_BUNDLE]
ITEM_NAME_TO_ID: dict[str, int] = {name: data.BASE_ID + index for index, name in enumerate(_ORDER)}

CLASSIFICATIONS: dict[str, ItemClassification] = {name: ItemClassification.progression for name in _ORDER}
CLASSIFICATIONS[MONEY_BUNDLE] = ItemClassification.filler


def key_name(level: dict) -> str | None:
    return None if level["kind"] == "tutorial" else f"{level['name']} Key"
