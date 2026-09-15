#!/usr/bin/env python3
"""Try to locate and pretty-print Addressables catalog (may be binary)."""
import pathlib, json, sys
paths = [
    pathlib.Path.home() / ".local/share/Steam/steamapps/common/Vampire Crawlers/Vampire Crawlers_Data/StreamingAssets/aa/catalog.json",
    pathlib.Path.home() / ".local/share/Steam/steamapps/common/Vampire Crawlers/Vampire Crawlers_Data/StreamingAssets/aa/catalog.bin",
    pathlib.Path.home() / ".local/share/Steam/steamapps/common/Vampire Crawlers/Vampire Crawlers_Data/StreamingAssets/aa/settings.json",
]
for p in paths:
    if p.exists():
        print(f"== {p} ({p.stat().st_size} bytes)")
        try:
            txt = p.read_text()[:2000]
            print(txt[:1500])
        except:
            data = p.read_bytes()[:512]
            print(data[:200].hex())
        print()
    else:
        print(f"missing {p}")
