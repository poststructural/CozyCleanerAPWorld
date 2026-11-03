"""Extract unstripped Mono class libraries from a Unity target-support .pkg (xar + gzip/pbzx cpio).

Usage: python -I extract_corlibs.py <module.pkg> <out_dir> <path-substring-filter>
Example filter: win64_player_nondevelopment_mono/Data/Managed/
"""
import gzip
import io
import lzma
import os
import struct
import sys
import zlib
import xml.etree.ElementTree as ET


def read_xar(path):
    f = open(path, "rb")
    magic, hsize, ver, toc_c, toc_u, cksum = struct.unpack(">4sHHQQI", f.read(28))
    assert magic == b"xar!", magic
    f.seek(hsize)
    toc = zlib.decompress(f.read(toc_c))
    heap = hsize + toc_c
    root = ET.fromstring(toc)
    files = {}

    def walk(node, prefix):
        for fe in node.findall("file"):
            name = prefix + fe.findtext("name")
            data = fe.find("data")
            if data is not None:
                files[name] = (heap + int(data.findtext("offset")), int(data.findtext("length")),
                               data.find("encoding").get("style"))
            walk(fe, name + "/")

    walk(root.find("toc"), "")
    return f, files


def pbzx(data):
    assert data[:4] == b"pbzx"
    pos, out = 12, bytearray()
    while pos < len(data):
        _, size = struct.unpack(">QQ", data[pos:pos + 16])
        pos += 16
        chunk = data[pos:pos + size]
        pos += size
        out += lzma.decompress(chunk) if chunk[:6] == b"\xfd7zXZ\x00" else chunk
    return bytes(out)


def cpio_odc(data):
    pos = 0
    while pos < len(data):
        hdr = data[pos:pos + 76]
        assert hdr[:6] == b"070707", hdr[:6]
        namesize = int(hdr[59:65], 8)
        filesize = int(hdr[65:76], 8)
        name = data[pos + 76:pos + 76 + namesize - 1].decode("utf-8", "replace")
        body = pos + 76 + namesize
        if name == "TRAILER!!!":
            return
        yield name, data[body:body + filesize]
        pos = body + filesize


def main():
    pkg, out_dir, filt = sys.argv[1], sys.argv[2], sys.argv[3]
    f, files = read_xar(pkg)
    payloads = [n for n in files if n.endswith("Payload")]
    n_out = 0
    for p in payloads:
        off, length, enc = files[p]
        f.seek(off)
        raw = f.read(length)
        if "bzip2" in enc:
            import bz2
            raw = bz2.decompress(raw)
        elif "gzip" in enc:
            raw = zlib.decompress(raw)
        if raw[:4] == b"pbzx":
            raw = pbzx(raw)
        elif raw[:2] == b"\x1f\x8b":
            raw = gzip.decompress(raw)
        for name, body in cpio_odc(raw):
            if filt in name and name.endswith(".dll"):
                dst = os.path.join(out_dir, os.path.basename(name))
                os.makedirs(out_dir, exist_ok=True)
                with open(dst, "wb") as o:
                    o.write(body)
                n_out += 1
    print(f"{n_out} dlls -> {out_dir}")


if __name__ == "__main__":
    main()
