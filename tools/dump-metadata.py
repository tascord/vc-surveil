#!/usr/bin/env python3
"""Parse Vampire Crawlers il2cpp global-metadata.dat strings for RE."""
import pathlib, re, sys, struct

GAME = pathlib.Path.home() / ".local/share/Steam/steamapps/common/Vampire Crawlers/Vampire Crawlers_Data/il2cpp_data/Metadata/global-metadata.dat"
if not GAME.exists():
    print(f"not found: {GAME}", file=sys.stderr)
    sys.exit(1)

data = GAME.read_bytes()
header = struct.unpack("<64i", data[:256])
string_offset = header[6]
string_size = header[7]
raw = data[string_offset:string_offset+string_size]
parts = [p.decode(errors="ignore") for p in raw.split(b"\x00") if len(p) > 2]
uniq = sorted(set(parts))
for filt in ["Nosebleed.Pancake", "Card", "Gem", "Arcana"]:
    print(f"\n== {filt} ==")
    for p in uniq:
        if filt in p:
            print(p)
    # dedup already
print(f"\nTotal unique strings: {len(uniq)}")
