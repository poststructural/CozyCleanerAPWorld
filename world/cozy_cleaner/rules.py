"""Logic helpers. Level access is precomputed as (key, tools) so counting reachable levels stays cheap."""
from __future__ import annotations

from typing import Callable

from BaseClasses import CollectionState

from . import data

Rule = Callable[[CollectionState], bool]


def level_requirements(level: dict, toolsanity: bool) -> tuple[str | None, tuple[str, ...]]:
    key = None if level["kind"] == "tutorial" else f"{level['name']} Key"
    if level["kind"] == "tutorial":
        return key, ()
    allowed = set(data.ALWAYS_TOOLS) | (set(data.SANITY_TOOLS) if toolsanity else set())
    return key, tuple(tool for tool in level["tools"] if tool in allowed)


def level_open(state: CollectionState, player: int, req: tuple[str | None, tuple[str, ...]]) -> bool:
    key, tools = req
    if key is not None and not state.has(key, player):
        return False
    return all(state.has(tool, player) for tool in tools)


def count_open(state: CollectionState, player: int, reqs: list[tuple[str | None, tuple[str, ...]]]) -> int:
    return sum(1 for req in reqs if level_open(state, player, req))


def levels_rule(player: int, reqs: list, needed: int) -> Rule:
    def rule(state: CollectionState) -> bool:
        count = 0
        for req in reqs:
            if level_open(state, player, req):
                count += 1
                if count >= needed:
                    return True
        return False
    return rule


def all_open_rule(player: int, reqs: list) -> Rule:
    def rule(state: CollectionState) -> bool:
        return all(level_open(state, player, req) for req in reqs)
    return rule


def cleared_rule(player: int, names: list[str], needed: int) -> Rule:
    def rule(state: CollectionState) -> bool:
        return sum(1 for name in names if state.has(name, player)) >= needed
    return rule
