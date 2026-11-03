import json
import math

from BaseClasses import ItemClassification

from .. import data, locations
from ..options import Goal
from . import CozyCleanerTestBase

OFF = {"cleansanity": "off", "minigamesanity": "off", "stagesanity": False, "shopsanity": False,
       "achievementsanity": False, "toolsanity": False, "lawn_every_cut": False}
MAX = {"cleansanity": "every_object", "minigamesanity": "every_unit", "stagesanity": True, "shopsanity": True,
       "achievementsanity": True, "toolsanity": True, "lawn_every_cut": True}


def real_locations(world):
    return [loc for loc in world.multiworld.get_locations(world.player) if loc.address is not None]


class TestDefaults(CozyCleanerTestBase):
    def test_slot_data_and_counts(self):
        slot = self.world.fill_slot_data()
        self.assertEqual(slot["goal"], "final_level")
        self.assertEqual(slot["levels_needed"], 25)
        self.assertEqual(len(slot["levels"]), 36)
        json.dumps(slot)
        pool = [i for i in self.multiworld.itempool if i.player == self.player]
        self.assertEqual(len(pool), len(real_locations(self.world)))

    def test_upgrades_progression(self):
        item = self.world.create_item("Progressive Hand Upgrade")
        self.assertTrue(item.classification & ItemClassification.progression)


class TestEverythingOff(CozyCleanerTestBase):
    options = OFF

    def test_still_fits(self):
        self.assertEqual(len(real_locations(self.world)), 36)
        pool = [i for i in self.multiworld.itempool if i.player == self.player]
        self.assertEqual(len(pool), 36)
        self.assertGreaterEqual(len(self.multiworld.precollected_items[self.player]), 1)


class TestEverythingOn(CozyCleanerTestBase):
    options = MAX

    def test_counts(self):
        total = sum(lv["total"] for lv in data.LEVELS)
        stages = 2 * sum(1 for lv in data.LEVELS if lv["kind"] in data.HOUSE_KINDS)
        expected = 36 + stages + total + 12 + 20
        self.assertEqual(len(real_locations(self.world)), expected)


class TestLevelsCompleted(CozyCleanerTestBase):
    options = {"goal": Goal.option_levels_completed, "levels_needed": 35, "toolsanity": True}


class TestLevelsCompletedOff(CozyCleanerTestBase):
    options = {**OFF, "goal": Goal.option_levels_completed, "levels_needed": 10}


class TestMilestones(CozyCleanerTestBase):
    options = {"cleansanity": "milestones", "minigamesanity": "milestones"}

    def test_milestone_names(self):
        names = {loc.name for loc in real_locations(self.world)}
        level = next(lv for lv in data.LEVELS if lv["name"] == "Level 9")
        for k in sorted({math.ceil(level["total"] * p) for p in (.25, .5, .75, 1.0)}):
            self.assertIn(locations.unit_name(level, k), names)
        self.assertNotIn(locations.unit_name(level, 2), names)


class TestNames(CozyCleanerTestBase):
    def test_every_plugin_name_exists(self):
        table = self.world.location_name_to_id
        for level in data.LEVELS:
            self.assertIn(f"{level['name']} - Complete", table)
            if level["kind"] in ("tutorial", "house"):
                self.assertIn(f"{level['name']} - Stage 1 Done", table)
                self.assertIn(f"{level['name']} - Stage 2 Done", table)
            for k in range(1, level["total"] + 1):
                self.assertIn(f"{level['name']} - {level['verb']} #{k}", table)
        for tool in data.TOOLS:
            for s in (1, 2):
                self.assertIn(f"Shop - {tool} Upgrade {s}", table)
        for title in data.ACHIEVEMENTS:
            self.assertIn(f"Achievement - {title}", table)

    def test_ids_unique_and_based(self):
        ids = list(self.world.location_name_to_id.values())
        self.assertEqual(len(ids), len(set(ids)))
        self.assertEqual(min(ids), 0xC0C10000)
        self.assertEqual(self.world.item_name_to_id["Level 2 Key"], 0xC0C10000)

    def test_lawn_groups(self):
        self.assertEqual(locations.lawn_groups(1170)[:2], [10, 20])
        self.assertEqual(locations.lawn_groups(1170)[-1], 1170)
        self.assertEqual(locations.lawn_groups(35), [10, 20, 30, 35])
