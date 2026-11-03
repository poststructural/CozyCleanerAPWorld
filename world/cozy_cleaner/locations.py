from __future__ import annotations

from BaseClasses import Location

from . import data


class CozyCleanerLocation(Location):
    game = data.GAME_NAME


GOAL_NAME = "Goal"


def milestones(total: int) -> list[int]:
    return sorted({-(-total * pct // 100) for pct in data.MILESTONE_PERCENTS})


def lawn_groups(total: int) -> list[int]:
    return sorted({*range(10, total + 1, 10), total})


def unit_name(level: dict, k: int) -> str:
    return f"{level['name']} - {level['verb']} #{k}"


def complete_name(level: dict) -> str:
    return f"{level['name']} - Complete"


def stage_name(level: dict, stage: int) -> str:
    return f"{level['name']} - Stage {stage} Done"


def shop_name(tool: str, step: int) -> str:
    return f"Shop - {tool} Upgrade {step}"


def achievement_name(title: str) -> str:
    return f"Achievement - {title}"


def _build() -> dict[str, int]:
    """Every location any option combination can create, in stable order."""
    table: dict[str, int] = {}

    def add(name: str) -> None:
        table[name] = data.BASE_ID + len(table)

    for level in data.LEVELS:
        add(complete_name(level))
        if level["kind"] in data.HOUSE_KINDS:
            add(stage_name(level, 1))
            add(stage_name(level, 2))
        for k in range(1, level["total"] + 1):
            add(unit_name(level, k))
    for tool in data.TOOLS:
        for step in (1, 2):
            add(shop_name(tool, step))
    for title in data.ACHIEVEMENTS:
        add(achievement_name(title))
    return table


LOCATION_NAME_TO_ID: dict[str, int] = _build()


def unit_ks(level: dict, cleansanity: str, minigamesanity: str, lawn_every_cut: bool) -> list[int]:
    """Which k values of a level are active locations."""
    house = level["kind"] in data.HOUSE_KINDS
    mode = cleansanity if house else minigamesanity
    total = level["total"]
    if mode == "off":
        return []
    if mode == "milestones":
        return milestones(total)
    if level["kind"] == "lawn" and not lawn_every_cut:
        return lawn_groups(total)
    return list(range(1, total + 1))
