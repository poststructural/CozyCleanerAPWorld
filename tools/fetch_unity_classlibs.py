"""Stream Unity's Linux editor tarball and keep only the Mono class libraries the game's runtime needs
(unstripped mscorlib etc.), so BepInEx can boot on this stripped build.

Usage: python -I fetch_unity_classlibs.py <out_dir> [changeset] [version]
Writes <out_dir>/unityjit-win32/*.dll and <out_dir>/unityjit-macos/*.dll (whichever exist).
"""
import os
import sys
import tarfile
import urllib.request

out = sys.argv[1]
changeset = sys.argv[2] if len(sys.argv) > 2 else "ea401c316338"
url = f"https://download.unity3d.com/download_unity/{changeset}/LinuxEditorInstaller/Unity.tar.xz"
WANT = ("/MonoBleedingEdge/lib/mono/unityjit-win32/", "/MonoBleedingEdge/lib/mono/unityjit-macos/",
        "/MonoBleedingEdge/lib/mono/unityjit-linux/")

seen = set()
with urllib.request.urlopen(url) as resp, tarfile.open(fileobj=resp, mode="r|xz") as tar:
    for m in tar:
        hit = next((w for w in WANT if w in m.name), None)
        if not hit or not m.isfile() or not m.name.endswith(".dll"):
            continue
        rel = m.name.split("/MonoBleedingEdge/lib/mono/", 1)[1]
        if "/" in rel.split("/", 1)[1]:   # skip Facades/ and other subfolders
            continue
        dst = os.path.join(out, rel)
        os.makedirs(os.path.dirname(dst), exist_ok=True)
        with tar.extractfile(m) as src, open(dst, "wb") as f:
            f.write(src.read())
        seen.add(rel.split("/")[0])
        print("got", rel, flush=True)
print("profiles:", sorted(seen))
