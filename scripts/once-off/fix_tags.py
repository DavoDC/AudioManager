"""
Post-integration tag fixes based on AudioManager report.
"""
import os
import re
from mutagen.id3 import ID3, TPE1, TCMP, TIT2, TALB

AUDIO   = "C:/Users/David/Audio"
ARTISTS = f"{AUDIO}/Artists"
MISC    = f"{AUDIO}/Miscellaneous Songs"

def fix(path, **changes):
    tags = ID3(path)
    for frame, val in changes.items():
        tags[frame] = val
    tags.save(path)
    print(f"  Fixed: {os.path.basename(path)}")

def strip_feat(s):
    """Remove ' (feat. ...)' from a string."""
    return re.sub(r'\s*\(feat\.[^)]*\)', '', s).strip()

errors = []

# ─────────────────────────────────────────────
# 1. JAY-Z TPE1 fix — match folder name 'Jay-Z'
# ─────────────────────────────────────────────
print("\n[1] Fixing JAY-Z TPE1 to match folder 'Jay-Z'...")
p = f"{ARTISTS}/Jay-Z/Singles/JAY-Z;Mr Hudson - Young Forever.mp3"
if os.path.exists(p):
    fix(p, TPE1=TPE1(encoding=3, text="Jay-Z;Mr Hudson"))
else:
    print(f"  ERR: not found: {p}")
    errors.append(p)

# ─────────────────────────────────────────────
# 2. Remove 'feat.' from TIT2/TALB
# ─────────────────────────────────────────────
print("\n[2] Stripping 'feat.' from title/album tags...")

feat_tracks = [
    # (path, fix_title, fix_album)
    (f"{ARTISTS}/David Guetta/Singles/David Guetta;Kid Cudi - Memories (feat. Kid Cudi).mp3",          True, True),
    (f"{ARTISTS}/Fort Minor/The Rising Tied (Deluxe Edition)/Fort Minor;BOBO;Styles Of Beyond - Believe Me (feat. Bobo & Styles of Beyond).mp3",  True, False),
    (f"{ARTISTS}/Fort Minor/The Rising Tied (Deluxe Edition)/Fort Minor;Holly Brook;Jonah Matranga - Where'd You Go (feat. Holly Brook & Jonah Matranga).mp3", True, False),
    (f"{ARTISTS}/Fort Minor/The Rising Tied (Deluxe Edition)/Fort Minor;John Legend - High Road (feat. John Legend).mp3", True, False),
    (f"{ARTISTS}/Lupe Fiasco/Lupe Fiasco's The Cool/Lupe Fiasco;Matthew Santos - Superstar (feat. Matthew Santos).mp3", True, False),
    (f"{ARTISTS}/Lupe Fiasco/Lupe Fiasco's The Cool/Lupe Fiasco;Nikki Jean - Hip-Hop Saved My Life (feat. Nikki Jean).mp3", True, False),
    (f"{ARTISTS}/Plies/Singles/Plies;Akon - Hypnotized (feat. Akon).mp3",                              True, True),
    (f"{ARTISTS}/Wiz Khalifa/Singles/Wiz Khalifa;Akon - Let It Go (feat. Akon).mp3",                   True, False),
    (f"{MISC}/Chiddy Bang;Icona Pop - Mind Your Manners (feat. Icona Pop).mp3",                        True, True),
    (f"{MISC}/Maino;T-Pain - All the Above (feat. T-Pain).mp3",                                        True, False),
    (f"{MISC}/Mase;Total - What You Want (feat. Total).mp3",                                           True, False),
    (f"{MISC}/P-Money;Akon - Keep on Calling (feat. Akon).mp3",                                        True, False),
]

for path, fix_title, fix_album in feat_tracks:
    if not os.path.exists(path):
        print(f"  ERR not found: {os.path.basename(path)}")
        errors.append(path)
        continue
    tags = ID3(path)
    changes = {}
    if fix_title:
        old_title = str(tags.get("TIT2", ""))
        new_title = strip_feat(old_title)
        if new_title != old_title:
            changes["TIT2"] = TIT2(encoding=3, text=new_title)
    if fix_album:
        old_album = str(tags.get("TALB", ""))
        new_album = strip_feat(old_album)
        if new_album != old_album:
            changes["TALB"] = TALB(encoding=3, text=new_album)
    if changes:
        tags.update(changes)
        tags.save(path)
        print(f"  Fixed: {os.path.basename(path)}")
    else:
        print(f"  Already clean: {os.path.basename(path)}")

# ─────────────────────────────────────────────
# 3. TCMP fix — 3 pre-existing tracks
# ─────────────────────────────────────────────
print("\n[3] Fixing TCMP on pre-existing tracks...")
tcmp_tracks = [
    f"{ARTISTS}/Michael Jackson/Michael/Michael Jackson;Akon - Hold My Hand (with Akon).mp3",
    f"{MISC}/Jimmy Eat World - Hear You Me.mp3",
    f"{MISC}/Savage Garden - Truly Madly Deeply.mp3",
]
for path in tcmp_tracks:
    if os.path.exists(path):
        fix(path, TCMP=TCMP(encoding=3, text="1"))
    else:
        print(f"  ERR not found: {os.path.basename(path)}")
        errors.append(path)

# ─────────────────────────────────────────────
# 4. David Fleming — fix album tag
# ─────────────────────────────────────────────
print("\n[4] Fixing David Fleming album tag...")
p = f"{MISC}/David Fleming - Last Son.mp3"
if os.path.exists(p):
    fix(p, TALB=TALB(encoding=3, text="Superman"))
else:
    print(f"  ERR not found: {p}")
    errors.append(p)

# ─────────────────────────────────────────────
print(f"\n{'='*50}")
print(f"Errors: {len(errors)}")
for e in errors:
    print(f"  - {e}")
print("="*50)
