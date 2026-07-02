"""Manual smoke check: extract real album-art thumbnails for one grid page.

Run:  python gui/tests/manual_check_art.py
Part of the subprocess/data manual smoke checklist in gui/README.md.
Not collected by pytest (no test_ prefix) - it touches the real library
(read-only) and real timing, which doesn't belong in the unit suite.
"""
import sys
import time
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))

from gui.art import get_thumbnail
from gui.data_loader import load_tracks

idx = load_tracks()
rows, total = idx.query(page=1, page_size=24)
start = time.time()
ok = missing = 0
for t in rows:
    p = get_thumbnail(t["id"], t.get("filePath"), t.get("hasArt", False))
    if p:
        ok += 1
    else:
        missing += 1
elapsed = time.time() - start
print(f"total tracks: {total}")
print(f"page of {len(rows)}: {ok} thumbnails extracted, {missing} placeholders")
print(f"elapsed: {elapsed:.2f}s (cold cache)")

start = time.time()
for t in rows:
    get_thumbnail(t["id"], t.get("filePath"), t.get("hasArt", False))
print(f"warm cache pass: {time.time() - start:.3f}s")
print("VERDICT:", "PASS" if ok >= len(rows) * 0.8 else "CHECK - many placeholders")
