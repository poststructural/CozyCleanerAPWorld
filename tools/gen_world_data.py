"""Build world/cozy_cleaner/data/levels.json from research/scenes.json and research/minigames.json.

Usage: python tools/gen_world_data.py
"""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
scenes = json.loads((ROOT / "research" / "scenes.json").read_text(encoding="utf-8"))["scenes"]

# Mini-games: progress unit verb and total, from research/minigames.json and the decompiled progress classes.
MINIGAMES = {
    5: {"kind": "recycling", "name": "Recycling", "verb": "Recycle", "total": 35},    # spawnCount 10+5+10+10
    11: {"kind": "garden", "name": "Garden", "verb": "Garden Step", "total": 42},      # targetStepCount
    17: {"kind": "fridge", "name": "Fridge", "verb": "Put Away", "total": 81},         # Dirty-tagged items
    23: {"kind": "repair", "name": "Repair", "verb": "Repair", "total": 35},           # RepairSetQueue 7+8+7+7+6
    30: {"kind": "lawn", "name": "Lawn", "verb": "Mow", "total": 1170},                # 585 GrassPatch x tall+short
}
CLOTH_FROM, CAN_FROM = 5, 11   # vanilla: Recycling grants Cloth, Garden grants Watering Can

levels = []
for s in scenes:
    lc = s["level_controller"]
    if not lc or lc["currentLevelID"] == 0:
        continue
    lid = lc["currentLevelID"]
    scene = Path(s["scene"]).stem
    sc = s["scripts"]
    entry = {"id": lid, "scene": scene, "build_index": s["build_index"]}
    if lid in MINIGAMES:
        entry.update(MINIGAMES[lid])
    else:
        act = s["active"]
        entry.update({
            "kind": "tutorial" if lc["isTutorialLevel"] else "house",
            "name": "Tutorial" if lc["isTutorialLevel"] else f"Level {lid}",
            "verb": "Clean",
            "total": act.get("Dirty", 0) + act.get("dirtySpriteParquet", 0),
            "stage1": act.get("Dirty", 0),
            "stage2": act.get("dirtySpriteParquet", 0),
        })
    if lid in MINIGAMES:
        entry["name"] = f"Level {lid} {MINIGAMES[lid]['name']}"
    tools = []
    if sc.get("VacuumController"):
        tools.append("Dustpan")
    if sc.get("WaterPuddle"):
        tools.append("Mop")
    if sc.get("ParquetCleaner"):
        tools.append("Vacuum")
    # Cloth / Watering Can are only logically required where vanilla already has them.
    if sc.get("wiping") and lid > CLOTH_FROM:
        tools.append("Cloth")
    if sc.get("FlowerWatering") and lid > CAN_FROM and entry["kind"] == "house":
        tools.append("Watering Can")
    entry["tools"] = tools
    levels.append(entry)

out = ROOT / "world" / "cozy_cleaner" / "data" / "levels.json"
out.parent.mkdir(parents=True, exist_ok=True)
out.write_text(json.dumps(levels, indent=1), encoding="utf-8")
print(f"{len(levels)} levels, {sum(l['total'] for l in levels)} progress units -> {out}")
