"""Build the Cozy Cleaner Archipelago release files into release/.

  cozy_cleaner.apworld        the world (drop into Archipelago's custom_worlds)
  CozyCleanerAP-Windows.zip   extract into the game folder (next to "Cozy Cleaner.exe")
  CozyCleanerAP-Mac.zip       extract into the game folder (next to "Cozy Cleaner.app")

Inputs (fetched once by fetch_unity_classlibs.py and by hand into tools/downloads):
  BepInEx_win_x64_5.4.23.3.zip, BepInEx_macos_x64_5.4.23.3.zip, classlibs/unityjit-{win32,macos}
and the built plugin and patcher in release/ (dotnet build -c Release in each plugin project).

Why the extra pieces beyond stock BepInEx:
  - The game ships a stripped mscorlib, so Doorstop must load Unity's full class libraries
    from BepInEx/corlib_only first (dll_search_path_override).
  - The game ignores winhttp.dll, so on Windows the Doorstop proxy is renamed to version.dll.
  - BepInEx's Unity log listener and headless check touch stripped engine members; the config
    turns the listener off and the CozyCleanerAP.Patcher preloader stubs the rest.
"""
from __future__ import annotations

import json
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
DL = ROOT / "tools" / "downloads"
REL = ROOT / "release"
WORLD = ROOT / "world" / "cozy_cleaner"

# Unity class libraries the stripped game needs in place of its own (same set on both platforms).
CORLIBS = [
    "mscorlib.dll", "System.dll", "System.Core.dll", "System.Configuration.dll", "System.Data.dll",
    "System.Net.Http.dll", "System.Numerics.dll", "System.Runtime.Serialization.dll",
    "System.Security.dll", "System.Xml.dll", "System.Xml.Linq.dll", "Mono.Security.dll",
    "Mono.Posix.dll", "Microsoft.CSharp.dll",
]

MAC_INSTALLER = "Install Cozy Cleaner AP.command"

BEPINEX_CFG = """[Logging]

## Cozy Cleaner's stripped engine cannot run BepInEx's Unity log listener, so it stays off.
# Setting type: Boolean
# Default value: true
UnityLogListening = false
"""


def build_apworld() -> Path:
    out = REL / "cozy_cleaner.apworld"
    with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED) as z:
        for f in sorted(WORLD.rglob("*")):
            rel = f.relative_to(WORLD)
            if f.is_dir() or "__pycache__" in rel.parts or rel.parts[0] == "test" or f.suffix == ".pyc":
                continue
            if rel.as_posix() == "archipelago.json":
                # The source manifest must not carry container versions (AP's manifest test), but the
                # .apworld container needs them, as AP's own "Build APWorlds" adds.
                manifest = json.loads(f.read_text(encoding="utf-8"))
                manifest.update(version=7, compatible_version=7)
                z.writestr("cozy_cleaner/archipelago.json", json.dumps(manifest, indent="\t"))
                continue
            z.write(f, "cozy_cleaner/" + rel.as_posix())
    return out


def add_common(z: zipfile.ZipFile, classlibs: Path) -> None:
    for name in CORLIBS:
        z.write(classlibs / name, "BepInEx/corlib_only/" + name)
    z.write(REL / "CozyCleanerAP.Patcher.dll", "BepInEx/patchers/CozyCleanerAP.Patcher.dll")
    z.write(REL / "CozyCleanerAP.dll", "BepInEx/plugins/CozyCleanerAP/CozyCleanerAP.dll")
    z.writestr("BepInEx/config/BepInEx.cfg", BEPINEX_CFG)


def build_windows() -> Path:
    out = REL / "CozyCleanerAP-Windows.zip"
    src = zipfile.ZipFile(DL / "BepInEx_win_x64_5.4.23.3.zip")
    with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED) as z:
        for info in src.infolist():
            if info.is_dir():
                continue
            data = src.read(info)
            name = info.filename
            if name == "winhttp.dll":
                name = "version.dll"
            elif name == "doorstop_config.ini":
                text = data.decode("utf-8")
                text = text.replace("dll_search_path_override =", r"dll_search_path_override = BepInEx\corlib_only", 1)
                assert r"BepInEx\corlib_only" in text, "doorstop_config.ini layout changed"
                data = text.encode("utf-8")
            z.writestr(name, data)
        add_common(z, DL / "classlibs" / "unityjit-win32")
    return out


def build_mac() -> Path:
    out = REL / "CozyCleanerAP-Mac.zip"
    src = zipfile.ZipFile(DL / "BepInEx_macos_x64_5.4.23.3.zip")
    with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED) as z:
        for info in src.infolist():
            if info.is_dir():
                continue
            data = src.read(info)
            if info.filename == "run_bepinex.sh":
                data = patch_mac_script(data.decode("utf-8")).encode("utf-8")
            # keep the unix exec bit on the launcher
            zi = zipfile.ZipInfo(info.filename, date_time=info.date_time)
            zi.external_attr = (0o755 if info.filename.endswith(".sh") else 0o644) << 16
            zi.compress_type = zipfile.ZIP_DEFLATED
            z.writestr(zi, data)
        add_common(z, DL / "classlibs" / "unityjit-macos")
        # Double-clickable installer: copies the files into the Steam game folder, clears quarantine,
        # checks Rosetta and puts the Steam launch option on the clipboard.
        installer = ROOT / "tools" / "mac" / MAC_INSTALLER
        zi = zipfile.ZipInfo(MAC_INSTALLER, date_time=(2026, 11, 3, 0, 0, 0))
        zi.external_attr = 0o755 << 16
        zi.compress_type = zipfile.ZIP_DEFLATED
        z.writestr(zi, installer.read_bytes().replace(b"\r\n", b"\n"))
    return out


def patch_mac_script(s: str) -> str:
    def sub(old: str, new: str) -> None:
        nonlocal s
        assert s.count(old) == 1, f"run_bepinex.sh layout changed near: {old!r}"
        s = s.replace(old, new)

    sub('executable_name=""', 'executable_name="Cozy Cleaner.app"')
    sub('dll_search_path_override=""', 'dll_search_path_override="BepInEx/corlib_only"')
    # Doorstop resolves the override against the working directory; make it absolute.
    sub('export DOORSTOP_MONO_DLL_SEARCH_PATH_OVERRIDE="$dll_search_path_override"',
        'case "$dll_search_path_override" in\n'
        '    ""|/*) ;;\n'
        '    *) dll_search_path_override="${BASEDIR}/${dll_search_path_override}" ;;\n'
        'esac\n'
        'export DOORSTOP_MONO_DLL_SEARCH_PATH_OVERRIDE="$dll_search_path_override"')
    # libdoorstop.dylib is x86_64 only. On Apple Silicon run the game's Intel slice under Rosetta.
    # arch is SIP-protected and drops DYLD_* from its environment, so pass them with -e instead.
    sub('exec "$executable_path" $rest_args',
        'if [ "$(uname -s)" = "Darwin" ] && [ "$(uname -m)" = "arm64" ]; then\n'
        '    exec arch -x86_64 -e DYLD_LIBRARY_PATH="$DYLD_LIBRARY_PATH" '
        '-e DYLD_INSERT_LIBRARIES="$DYLD_INSERT_LIBRARIES" "$executable_path" $rest_args\n'
        'fi\n'
        'exec "$executable_path" $rest_args')
    return s


def main() -> None:
    for f in (build_apworld(), build_windows(), build_mac()):
        with zipfile.ZipFile(f) as z:
            print(f"{f.name}: {len(z.namelist())} files, {f.stat().st_size // 1024} KB")


if __name__ == "__main__":
    main()
