"""Read mini-game and level MonoBehaviour fields with the Managed assemblies as typetree source.

Usage: python scan_minigames.py "<game>/Cozy Cleaner_Data" out.json
"""
import json
import os
import sys
from collections import Counter

import UnityPy
from UnityPy.helpers.TypeTreeGenerator import TypeTreeGenerator

WANT = {
    "RecyclingSpawnManager", "CleaningProgressForRecycling", "CleaningProgressForRepair",
    "CleaningProgressForFlower", "CleaningProgressDirty", "LawnCleaningProgress", "CleaningProgress",
    "level_controller", "LevelHouse", "ShopToolManager", "ToolUpgradeButton", "RepairSetQueue",
}
SCENES = [2, 7, 13, 19, 25, 32, 4, 38]


def slim(v, depth=0):
    if isinstance(v, dict):
        if set(v) == {"m_FileID", "m_PathID"}:
            return None
        return {k: slim(x, depth + 1) for k, x in v.items() if not k.startswith("m_") or k == "m_Name"}
    if isinstance(v, list):
        return [slim(x, depth + 1) for x in v[:60]]
    return v


def main():
    data_dir, out_path = sys.argv[1], sys.argv[2]
    gen = TypeTreeGenerator("2022.3.9f1")
    gen.load_local_game(os.path.dirname(data_dir.rstrip("/\\")))
    out = {}
    for idx in SCENES:
        env = UnityPy.load(os.path.join(data_dir, f"level{idx}"), os.path.join(data_dir, "globalgamemanagers.assets"))
        found = []
        pending = []
        for obj in env.objects:
            if obj.type.name != "MonoBehaviour":
                continue
            try:
                mb = obj.read(check_read=False)
                cls = mb.m_Script.read().m_ClassName
            except Exception:
                continue
            if cls in WANT:
                pending.append((cls, obj))
        env.typetree_generator = gen
        for cls, obj in pending:
            try:
                tt = obj.read_typetree()
            except Exception as exc:
                found.append({"class": cls, "error": str(exc)})
                continue
            found.append({"class": cls, "fields": slim(tt)})
        out[idx] = found
        print(idx, Counter(f["class"] for f in found))
    with open(out_path, "w", encoding="utf-8") as f:
        json.dump(out, f, indent=1, default=str)


if __name__ == "__main__":
    main()

