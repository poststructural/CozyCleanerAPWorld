from __future__ import annotations

from collections.abc import Mapping
from typing import Any, ClassVar

from BaseClasses import ItemClassification, Region
from worlds.AutoWorld import World

from . import data, items, locations, rules, web_world
from . import options as cozy_options


SLACK = 8


class CozyCleanerWorld(World):
    """Cozy Cleaner is a relaxed first-person cleaning game: tidy houses room by room, recycle, garden,
    stock fridges, repair toys and mow lawns while upgrading your tools.

    Level keys, tools and tool upgrades are shuffled across the multiworld."""

    game = data.GAME_NAME
    web = web_world.CozyCleanerWebWorld()
    options_dataclass = cozy_options.CozyCleanerOptions
    options: cozy_options.CozyCleanerOptions

    item_name_to_id: ClassVar[dict[str, int]] = items.ITEM_NAME_TO_ID
    location_name_to_id: ClassVar[dict[str, int]] = locations.LOCATION_NAME_TO_ID
    item_name_groups = {
        "Keys": set(items.KEY_ITEMS),
        "Tools": set(items.TOOL_ITEMS),
        "Upgrades": set(items.UPGRADE_ITEMS),
    }

    def generate_early(self) -> None:
        toolsanity = bool(self.options.toolsanity)
        self.reqs: dict[int, tuple] = {lv["id"]: rules.level_requirements(lv, toolsanity) for lv in data.LEVELS}
        self.start_keys: list[str] = []
        self.pool_names: list[str] = []
        self.active: list[str] = self._active_location_names()
        self._plan_items()

    # ---- planning -----------------------------------------------------
    def _active_location_names(self) -> list[str]:
        opt = self.options
        names: list[str] = []
        for level in data.LEVELS:
            names.append(locations.complete_name(level))
            if opt.stagesanity and level["kind"] in data.HOUSE_KINDS:
                names += [locations.stage_name(level, 1), locations.stage_name(level, 2)]
            for k in locations.unit_ks(level, opt.cleansanity.current_key, opt.minigamesanity.current_key,
                                       bool(opt.lawn_every_cut)):
                names.append(locations.unit_name(level, k))
        if opt.shopsanity:
            names += [locations.shop_name(tool, step) for tool in data.TOOLS for step in (1, 2)]
        if opt.achievementsanity:
            names += [locations.achievement_name(title) for title in data.ACHIEVEMENTS]
        return names

    def _plan_items(self) -> None:
        opt = self.options
        pool = list(items.KEY_ITEMS) + ["Cloth", "Watering Can"]
        if opt.toolsanity:
            pool += ["Dustpan", "Mop", "Vacuum"]
        if opt.shopsanity:
            for name in items.UPGRADE_ITEMS:
                pool += [name, name]
        # Money Bundles are dropped first (they are only added to fill); if the progression items still do not
        # fit with some slack (a pool that fills every location can dead-end), the keys of the lowest levels go
        # to the start inventory.
        for key in items.KEY_ITEMS:
            if len(pool) <= len(self.active) - SLACK:
                break
            pool.remove(key)
            self.start_keys.append(key)
        self.pool_names = pool

    # ---- generation ---------------------------------------------------
    def create_regions(self) -> None:
        player = self.player
        menu = Region("Menu", player, self.multiworld)
        self.multiworld.regions.append(menu)
        regions: dict[int, Region] = {}
        by_kind: dict[str, list] = {}
        for level in data.LEVELS:
            region = Region(level["name"], player, self.multiworld)
            regions[level["id"]] = region
            self.multiworld.regions.append(region)
            by_kind.setdefault(level["kind"], []).append(self.reqs[level["id"]])
        active = set(self.active)

        def place(name: str, region: Region, rule=None) -> None:
            if name in active:
                loc = locations.CozyCleanerLocation(player, name, self.location_name_to_id[name], region)
                if rule is not None:
                    loc.access_rule = rule
                region.locations.append(loc)

        for level in data.LEVELS:
            region = regions[level["id"]]
            req = self.reqs[level["id"]]
            menu.connect(region, f"Menu -> {level['name']}",
                         lambda state, r=req: rules.level_open(state, player, r))
            place(locations.complete_name(level), region)
            if level["kind"] in data.HOUSE_KINDS:
                place(locations.stage_name(level, 1), region)
                place(locations.stage_name(level, 2), region)
            for k in range(1, level["total"] + 1):
                place(locations.unit_name(level, k), region)

        all_reqs = [self.reqs[lv["id"]] for lv in data.LEVELS]
        water = [self.reqs[lv["id"]] for lv in data.LEVELS if "Watering Can" in lv["tools"]]
        # the game's level counter (level_controller.TryReportLevelCompletion) skips the tutorial
        counted_reqs = [self.reqs[lv["id"]] for lv in data.LEVELS if lv["kind"] != "tutorial"]
        # Well Equipped needs every tool owned at stage 3 (ToolManager.TryUnlockWellEquipped)
        owned_tools = ["Cloth", "Watering Can"] + (["Dustpan", "Mop", "Vacuum"] if self.options.toolsanity else [])
        has_tools = lambda state: state.has_all(owned_tools, player)
        garden_or_water = rules.levels_rule(player, by_kind["garden"], 1)
        water_three = rules.levels_rule(player, water, 3)
        three, eight = rules.levels_rule(player, all_reqs, 3), rules.levels_rule(player, all_reqs, 8)
        minigames = [r for kind in data.MINIGAME_KINDS for r in by_kind[kind]]

        if self.options.shopsanity:
            for tool in data.TOOLS:
                place(locations.shop_name(tool, 1), menu, three)
                place(locations.shop_name(tool, 2), menu, eight)

        up_names = items.UPGRADE_ITEMS
        ach = {
            "First Steps": None, "Guiding Paw": None, "Gentle Touch": None, "Curious Hands": None,
            "Novice Cleaner": rules.levels_rule(player, counted_reqs, 10),
            "Master Cleaner": rules.levels_rule(player, counted_reqs, 20),
            "Clean City": rules.levels_rule(player, [self.reqs[36]], 1),
            "Jack Of All Trades": rules.all_open_rule(player, minigames),
            "Recycling Master": rules.all_open_rule(player, by_kind["recycling"]),
            "Repair Master": rules.all_open_rule(player, by_kind["repair"]),
            "Organized Fridge": rules.all_open_rule(player, by_kind["fridge"]),
            "Gardener": rules.all_open_rule(player, by_kind["garden"]),
            "Freshly Mowed": rules.all_open_rule(player, by_kind["lawn"]),
            "Trash Hunter": rules.levels_rule(player, by_kind["house"], 6),
            "Spotless Corners": rules.levels_rule(player, by_kind["house"], 6),
            "Sparkling Floors": rules.levels_rule(player, by_kind["house"], 6),
            # FlowerWatering also counts in the Garden mini-game, which uses its own can
            "Flower Friend": lambda state: garden_or_water(state) or water_three(state),
        }
        if self.options.shopsanity:
            # the plugin can only apply an upgrade to a tool the player owns (SetToolStage fires the achievement)
            gated = ["Cloth", "Watering Can"] + (["Dustpan", "Mop", "Vacuum"] if self.options.toolsanity else [])

            def upgraded(state, count):
                return any(state.has(f"Progressive {t} Upgrade", player, count) and (t not in gated or state.has(t, player))
                           for t in data.TOOLS)
            ach["First Upgrade"] = lambda state: upgraded(state, 1)
            ach["Perfect Tool"] = lambda state: upgraded(state, 2)
            ach["Well Equipped Cleaner"] = lambda state: has_tools(state) and all(state.has(n, player, 2) for n in up_names)
        else:
            ach["First Upgrade"], ach["Perfect Tool"] = three, eight
            ach["Well Equipped Cleaner"] = lambda state: has_tools(state) and eight(state)
        for title in data.ACHIEVEMENTS:
            place(locations.achievement_name(title), menu, ach[title])

        # Goal event
        if self.options.goal == cozy_options.Goal.option_levels_completed:
            goal_region = menu
            cleared = []
            for level in data.LEVELS:
                if level["kind"] == "tutorial":
                    continue
                name = f"{level['name']} Cleared"
                region = regions[level["id"]]
                loc = locations.CozyCleanerLocation(player, f"{level['name']} - Cleared", None, region)
                loc.place_locked_item(items.CozyCleanerItem(name, ItemClassification.progression, None, player))
                region.locations.append(loc)
                cleared.append(name)
            goal_rule = rules.cleared_rule(player, cleared, self.options.levels_needed.value)
        else:
            goal_region = regions[36]
            goal_rule = None
        goal_loc = locations.CozyCleanerLocation(player, locations.GOAL_NAME, None, goal_region)
        if goal_rule is not None:
            goal_loc.access_rule = goal_rule
        goal_loc.place_locked_item(items.CozyCleanerItem("Victory", ItemClassification.progression, None, player))
        goal_region.locations.append(goal_loc)

    def create_item(self, name: str) -> items.CozyCleanerItem:
        return items.CozyCleanerItem(name, items.CLASSIFICATIONS[name], self.item_name_to_id[name], self.player)

    def create_items(self) -> None:
        for name in self.start_keys:
            self.multiworld.push_precollected(self.create_item(name))
        pool = [self.create_item(name) for name in self.pool_names]
        filler = len(self.active) - len(pool)
        pool += [self.create_item(items.MONEY_BUNDLE) for _ in range(max(0, filler))]
        self.multiworld.itempool += pool

    def get_filler_item_name(self) -> str:
        return items.MONEY_BUNDLE

    def set_rules(self) -> None:
        self.multiworld.completion_condition[self.player] = lambda state: state.has("Victory", self.player)

    def fill_slot_data(self) -> Mapping[str, Any]:
        opt = self.options
        return {
            "goal": opt.goal.current_key,
            "levels_needed": opt.levels_needed.value,
            "toolsanity": bool(opt.toolsanity),
            "shopsanity": bool(opt.shopsanity),
            "money_multiplier": opt.money_multiplier.value,
            "death_link": False,
            "levels": data.LEVELS,
        }
