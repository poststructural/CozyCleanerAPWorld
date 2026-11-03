"""Scan Cozy Cleaner scene files and report, per build scene: name, level_controller fields,
progress-manager script present, and counts of GameObjects by tag.

Usage: python scan_scenes.py "<game>/Cozy Cleaner_Data" out.json
"""
import json
import os
import struct
import sys
from collections import Counter

import UnityPy


def read_aligned_string(b, off):
    n = struct.unpack_from("<i", b, off)[0]
    off += 4
    s = b[off:off + n].decode("utf-8", "replace")
    off += n
    off += (-off) % 4
    return s, off


def mb_fields_offset(raw):
    # m_GameObject PPtr(12) + m_Enabled(1, aligned 4) + m_Script PPtr(12) + m_Name string
    off = 12 + 4 + 12
    name, off = read_aligned_string(raw, off)
    return off


def main():
    data_dir, out_path = sys.argv[1], sys.argv[2]
    ggm = UnityPy.load(os.path.join(data_dir, "globalgamemanagers"))
    scenes, tags = [], {}
    for obj in ggm.objects:
        if obj.type.name == "BuildSettings":
            scenes = list(obj.read_typetree()["scenes"])
        elif obj.type.name == "TagManager":
            tt = obj.read_typetree()
            # builtin tags: 0 Untagged, 1 Respawn, 2 Finish, 3 EditorOnly, 5 MainCamera, 6 Player, 7 GameController
            builtin = {0: "Untagged", 1: "Respawn", 2: "Finish", 3: "EditorOnly", 5: "MainCamera", 6: "Player", 7: "GameController"}
            tags.update(builtin)
            for i, t in enumerate(tt["tags"]):
                tags[20000 + i] = t
    report = []
    for idx, scene in enumerate(scenes):
        path = os.path.join(data_dir, f"level{idx}")
        if not os.path.exists(path):
            continue
        env = UnityPy.load(path)
        tag_counts = Counter()
        scripts = Counter()
        lc = None
        dirty_names = []
        go_by_id = {}
        for obj in env.objects:
            if obj.type.name == "GameObject":
                tt = obj.read_typetree()
                tname = tags.get(tt["m_Tag"], str(tt["m_Tag"]))
                tag_counts[tname] += 1
                go_by_id[obj.path_id] = (tt["m_Name"], tname, tt["m_IsActive"])
                if tname in ("Dirty", "dirtySpriteParquet"):
                    dirty_names.append([tname, tt["m_Name"], tt["m_IsActive"]])
        # active-in-hierarchy: object and every Transform ancestor's GameObject must be active
        parent_of, go_of_tr, tr_of_go = {}, {}, {}
        for obj in env.objects:
            if obj.type.name in ("Transform", "RectTransform"):
                tt = obj.read_typetree()
                parent_of[obj.path_id] = tt["m_Father"]["m_PathID"]
                go_of_tr[obj.path_id] = tt["m_GameObject"]["m_PathID"]
                tr_of_go[tt["m_GameObject"]["m_PathID"]] = obj.path_id

        def active_in_hierarchy(go_id):
            tr = tr_of_go.get(go_id)
            while tr:
                g = go_by_id.get(go_of_tr.get(tr))
                if g is None or not g[2]:
                    return False
                tr = parent_of.get(tr)
            return True

        active_counts = Counter()
        for go_id, (gname, tname, _) in go_by_id.items():
            if tname in ("Dirty", "dirtySpriteParquet") and active_in_hierarchy(go_id):
                active_counts[tname] += 1
        for obj in env.objects:
            if obj.type.name != "MonoBehaviour":
                continue
            try:
                mb = obj.read(check_read=False)
                script = mb.m_Script.read()
                sname = script.m_ClassName
            except Exception:
                continue
            scripts[sname] += 1
            if sname == "level_controller":
                raw = obj.get_raw_data()
                off = mb_fields_offset(raw)
                lc = {"currentLevelID": struct.unpack_from("<i", raw, off)[0],
                      "isTutorialLevel": raw[off + 4] != 0}
        report.append({
            "build_index": idx, "scene": scene, "level_controller": lc,
            "tags": dict(tag_counts), "active": dict(active_counts), "dirty": dirty_names,
            "scripts": {k: v for k, v in scripts.items()},
        })
        print(idx, scene, lc, {k: tag_counts[k] for k in ("Dirty", "dirtySpriteParquet") if k in tag_counts}, dict(active_counts))
    with open(out_path, "w", encoding="utf-8") as f:
        json.dump({"tags": tags, "scenes": report}, f, indent=1)


if __name__ == "__main__":
    main()
