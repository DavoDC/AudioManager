"""
NewMusic Integration Script
Runs all tag fixes, renames, and moves per the approved plan.
"""

import os
import shutil
from mutagen.id3 import ID3, TPE1, TCON, TCMP, ID3NoHeaderError

NEWMUSIC = "C:/Users/David/Downloads/NewMusic"
AUDIO    = "C:/Users/David/Audio"
ARTISTS  = f"{AUDIO}/Artists"
MISC     = f"{AUDIO}/Miscellaneous Songs"
MUSIV    = f"{AUDIO}/Musivation"

def ensure(path):
    os.makedirs(path, exist_ok=True)

def set_tcmp(tags):
    tags["TCMP"] = TCMP(encoding=3, text="1")

def move(src_dir, src_name, dest_dir, dest_name=None):
    if dest_name is None:
        dest_name = src_name
    src  = os.path.join(src_dir, src_name)
    dest = os.path.join(dest_dir, dest_name)
    if not os.path.exists(src):
        print(f"  MISSING SRC: {src}")
        return False
    if os.path.exists(dest):
        print(f"  ALREADY EXISTS: {dest}")
        return False
    ensure(dest_dir)
    shutil.move(src, dest)
    print(f"  OK  {src_name} -> .../{os.path.relpath(dest_dir, AUDIO)}/")
    return True

errors = []

# ─────────────────────────────────────────────
# 1. TCMP = 1 on all NewMusic files
# ─────────────────────────────────────────────
print("\n[1] Setting TCMP=1 on all NewMusic files...")
for f in sorted(os.listdir(NEWMUSIC)):
    if not f.endswith(".mp3"):
        continue
    path = os.path.join(NEWMUSIC, f)
    try:
        tags = ID3(path)
        set_tcmp(tags)
        tags.save(path)
    except Exception as e:
        print(f"  ERR {f}: {e}")
        errors.append(f)
print(f"  Done.")

# ─────────────────────────────────────────────
# 2. JAY-Z: fix tag + rename
# ─────────────────────────────────────────────
print("\n[2] Fixing JAY-Z tag + rename...")
jayz_old = None
for f in os.listdir(NEWMUSIC):
    if "Young Forever" in f:
        jayz_old = f
        break
if jayz_old:
    path = os.path.join(NEWMUSIC, jayz_old)
    tags = ID3(path)
    tags["TPE1"] = TPE1(encoding=3, text="JAY-Z;Mr Hudson")
    set_tcmp(tags)
    tags.save(path)
    jayz_new = "JAY-Z;Mr Hudson - Young Forever.mp3"
    os.rename(path, os.path.join(NEWMUSIC, jayz_new))
    print(f"  Renamed: {jayz_old} -> {jayz_new}")
else:
    print("  ERR: Young Forever not found")
    errors.append("JAY-Z - Young Forever")

# ─────────────────────────────────────────────
# 3. Akira The Don: fix tag + rename
# ─────────────────────────────────────────────
print("\n[3] Fixing Akira The Don tag + rename...")
atd_old = "Akira The Don - NO SMARTER THAN YOU.mp3"
path = os.path.join(NEWMUSIC, atd_old)
if os.path.exists(path):
    tags = ID3(path)
    tags["TPE1"] = TPE1(encoding=3, text="Akira The Don;Steve Jobs")
    tags["TCON"] = TCON(encoding=3, text="Musivation")
    set_tcmp(tags)
    tags.save(path)
    atd_new = "Akira The Don;Steve Jobs - NO SMARTER THAN YOU.mp3"
    os.rename(path, os.path.join(NEWMUSIC, atd_new))
    print(f"  Renamed: {atd_old} -> {atd_new}")
else:
    print(f"  ERR: {atd_old} not found")
    errors.append(atd_old)

# ─────────────────────────────────────────────
# 4. Loot Bryon Smith: fix genre
# ─────────────────────────────────────────────
print("\n[4] Fixing Loot Bryon Smith genre...")
for f in ["Loot Bryon Smith - On Point.mp3", "Loot Bryon Smith - Polarity.mp3"]:
    path = os.path.join(NEWMUSIC, f)
    if os.path.exists(path):
        tags = ID3(path)
        tags["TCON"] = TCON(encoding=3, text="Musivation")
        set_tcmp(tags)
        tags.save(path)
        print(f"  Fixed genre: {f}")
    else:
        print(f"  ERR: {f} not found")
        errors.append(f)

# ─────────────────────────────────────────────
# 5. Rename Misc files that need all-artist filenames
# ─────────────────────────────────────────────
print("\n[5] Renaming files in NewMusic (add featured artists to filename)...")
renames = {
    "A Touch Of Class - Around the World (La La La La La).mp3":
        "A Touch Of Class;Pete Konemann - Around the World (La La La La La).mp3",
    "Bone Thugs-N-Harmony - Never Forget Me (Album Version Explicit).mp3":
        "Bone Thugs-N-Harmony;Akon - Never Forget Me (Album Version Explicit).mp3",
    "Chiddy Bang - Mind Your Manners (feat. Icona Pop).mp3":
        "Chiddy Bang;Icona Pop - Mind Your Manners (feat. Icona Pop).mp3",
    "Colby O'Donis - What You Got.mp3":
        "Colby O'Donis;Akon - What You Got.mp3",
    "Dan + Shay - 10,000 Hours.mp3":
        "Dan + Shay;Justin Bieber - 10,000 Hours.mp3",
    "David Guetta - Memories (feat. Kid Cudi).mp3":
        "David Guetta;Kid Cudi - Memories (feat. Kid Cudi).mp3",
    "Fort Minor - Believe Me (feat. Bobo & Styles of Beyond).mp3":
        "Fort Minor;BOBO;Styles Of Beyond - Believe Me (feat. Bobo & Styles of Beyond).mp3",
    "Fort Minor - High Road (feat. John Legend).mp3":
        "Fort Minor;John Legend - High Road (feat. John Legend).mp3",
    "Fort Minor - Where'd You Go (feat. Holly Brook & Jonah Matranga).mp3":
        "Fort Minor;Holly Brook;Jonah Matranga - Where'd You Go (feat. Holly Brook & Jonah Matranga).mp3",
    "Kevin Rudolf - I Made It (Cash Money Heroes).mp3":
        "Kevin Rudolf;Birdman;Jay Sean;Lil Wayne - I Made It (Cash Money Heroes).mp3",
    "k-os - I Wish I Knew Natalie Portman.mp3":
        "k-os;Saukrates;Nelly Furtado - I Wish I Knew Natalie Portman.mp3",
    "Maino - All the Above (feat. T-Pain).mp3":
        "Maino;T-Pain - All the Above (feat. T-Pain).mp3",
    "Mase - What You Want (feat. Total).mp3":
        "Mase;Total - What You Want (feat. Total).mp3",
    "Nic D - Daytona.mp3":
        "Nic D;Vwillz - Daytona.mp3",
    "P-Money - Keep on Calling (feat. Akon).mp3":
        "P-Money;Akon - Keep on Calling (feat. Akon).mp3",
    "Plies - Hypnotized (feat. Akon).mp3":
        "Plies;Akon - Hypnotized (feat. Akon).mp3",
    "Wiz Khalifa - Let It Go (feat. Akon).mp3":
        "Wiz Khalifa;Akon - Let It Go (feat. Akon).mp3",
    "Lupe Fiasco - Hip-Hop Saved My Life (feat. Nikki Jean).mp3":
        "Lupe Fiasco;Nikki Jean - Hip-Hop Saved My Life (feat. Nikki Jean).mp3",
    "Lupe Fiasco - Superstar (feat. Matthew Santos).mp3":
        "Lupe Fiasco;Matthew Santos - Superstar (feat. Matthew Santos).mp3",
}
for old, new in renames.items():
    src = os.path.join(NEWMUSIC, old)
    dst = os.path.join(NEWMUSIC, new)
    if os.path.exists(src):
        os.rename(src, dst)
        print(f"  {old} -> {new}")
    elif os.path.exists(dst):
        print(f"  Already renamed: {new}")
    else:
        print(f"  ERR not found: {old}")
        errors.append(old)

# ─────────────────────────────────────────────
# 6. MOVE: Musivation
# ─────────────────────────────────────────────
print("\n[6] Moving to Musivation...")
move(NEWMUSIC, "Akira The Don;Steve Jobs - NO SMARTER THAN YOU.mp3",
     f"{MUSIV}/Akira The Don/Singles")
move(NEWMUSIC, "Loot Bryon Smith - On Point.mp3",
     f"{MUSIV}/Loot Bryon Smith/Spotify Albums/Refined Yet Rebellious")
move(NEWMUSIC, "Loot Bryon Smith - Polarity.mp3",
     f"{MUSIV}/Loot Bryon Smith/Spotify Albums/Refined Yet Rebellious")

# ─────────────────────────────────────────────
# 7. MOVE: Artists — existing folders
# ─────────────────────────────────────────────
print("\n[7] Moving to existing Artist folders...")
# Avril Lavigne
move(NEWMUSIC, "Avril Lavigne - I'm with You.mp3",           f"{ARTISTS}/Avril Lavigne/Let Go")
move(NEWMUSIC, "Avril Lavigne - Bite Me.mp3",                f"{ARTISTS}/Avril Lavigne/Singles")
move(NEWMUSIC, "Avril Lavigne - Fall To Pieces.mp3",         f"{ARTISTS}/Avril Lavigne/Singles")
move(NEWMUSIC, "Avril Lavigne - When You're Gone.mp3",       f"{ARTISTS}/Avril Lavigne/Singles")
move(NEWMUSIC, "Avril Lavigne - Wish You Were Here.mp3",     f"{ARTISTS}/Avril Lavigne/Singles")
# David Guetta
move(NEWMUSIC, "David Guetta;Kid Cudi - Memories (feat. Kid Cudi).mp3", f"{ARTISTS}/David Guetta/Singles")
# Jay-Z
move(NEWMUSIC, "JAY-Z;Mr Hudson - Young Forever.mp3",        f"{ARTISTS}/Jay-Z/Singles")
# Katy Perry (2 songs same album → new album folder)
move(NEWMUSIC, "Katy Perry - Hot N Cold.mp3",                f"{ARTISTS}/Katy Perry/One Of The Boys (15th Anniversary Edition)")
move(NEWMUSIC, "Katy Perry - Waking Up In Vegas.mp3",        f"{ARTISTS}/Katy Perry/One Of The Boys (15th Anniversary Edition)")
# Nic D
move(NEWMUSIC, "Nic D;Vwillz - Daytona.mp3",                f"{ARTISTS}/Nic D/Singles")
# Phil Collins
move(NEWMUSIC, "Phil Collins - I Wish It Would Rain Down (2016 Remaster).mp3", f"{ARTISTS}/Phil Collins/Singles")
# Plies
move(NEWMUSIC, "Plies;Akon - Hypnotized (feat. Akon).mp3",   f"{ARTISTS}/Plies/Singles")
# Wiz Khalifa
move(NEWMUSIC, "Wiz Khalifa;Akon - Let It Go (feat. Akon).mp3", f"{ARTISTS}/Wiz Khalifa/Singles")
# mike (folder name is "mike")
move(NEWMUSIC, "mike. - woke up new.mp3",                    f"{ARTISTS}/mike/Singles")

# ─────────────────────────────────────────────
# 8. MOVE: Artists — new folders (threshold triggered)
# ─────────────────────────────────────────────
print("\n[8] Moving to new Artist folders (threshold)...")

# Fort Minor (4 new)
fm_album = f"{ARTISTS}/Fort Minor/The Rising Tied (Deluxe Edition)"
move(NEWMUSIC, "Fort Minor;BOBO;Styles Of Beyond - Believe Me (feat. Bobo & Styles of Beyond).mp3", fm_album)
move(NEWMUSIC, "Fort Minor;John Legend - High Road (feat. John Legend).mp3",                        fm_album)
move(NEWMUSIC, "Fort Minor - Welcome.mp3",                                                          fm_album)
move(NEWMUSIC, "Fort Minor;Holly Brook;Jonah Matranga - Where'd You Go (feat. Holly Brook & Jonah Matranga).mp3", fm_album)

# Backstreet Boys (2 new + 2 from Misc)
bsb = f"{ARTISTS}/Backstreet Boys/Singles"
move(NEWMUSIC, "Backstreet Boys - Everybody (Backstreet's Back) (Radio Edit).mp3", bsb)
move(NEWMUSIC, "Backstreet Boys - Shape of My Heart.mp3",                          bsb)
move(MISC,     "Backstreet Boys - I Want It That Way.mp3",                         bsb)
move(MISC,     "Backstreet Boys - Inconsolable.mp3",                               bsb)

# Bone Thugs-N-Harmony (2 new + 1 from Misc)
bt_sl = f"{ARTISTS}/Bone Thugs-N-Harmony/Strength & Loyalty"
bt_si = f"{ARTISTS}/Bone Thugs-N-Harmony/Singles"
move(NEWMUSIC, "Bone Thugs-N-Harmony;Akon - Never Forget Me (Album Version Explicit).mp3", bt_sl)
move(NEWMUSIC, "Bone Thugs-N-Harmony - Tha Crossroads.mp3",                                bt_si)
move(MISC,     "Bone Thugs-N-Harmony; Akon - I Tried.mp3",
               bt_sl, "Bone Thugs-N-Harmony;Akon - I Tried.mp3")  # also fix filename spacing

# Bryan Adams (all 3 from "Reckless")
ba = f"{ARTISTS}/Bryan Adams/Reckless"
move(NEWMUSIC, "Bryan Adams - Heaven.mp3",         ba)
move(NEWMUSIC, "Bryan Adams - Somebody.mp3",       ba)
move(MISC,     "Bryan Adams - Summer Of '69.mp3",  ba)

# Lupe Fiasco
lf_cool = f"{ARTISTS}/Lupe Fiasco/Lupe Fiasco's The Cool"
lf_si   = f"{ARTISTS}/Lupe Fiasco/Singles"
move(NEWMUSIC, "Lupe Fiasco;Nikki Jean - Hip-Hop Saved My Life (feat. Nikki Jean).mp3",  lf_cool)
move(NEWMUSIC, "Lupe Fiasco;Matthew Santos - Superstar (feat. Matthew Santos).mp3",      lf_cool)
move(MISC,     "Lupe Fiasco - The Show Goes On.mp3",                                     lf_si)

# ─────────────────────────────────────────────
# 9. MOVE: Remaining → Miscellaneous Songs
# ─────────────────────────────────────────────
print("\n[9] Moving remaining files to Miscellaneous Songs...")
misc_files = [
    "A Touch Of Class;Pete Konemann - Around the World (La La La La La).mp3",
    "Busy Signal - Sweet Love (Night Shift).mp3",
    "Chiddy Bang - Dream Chasin'.mp3",
    "Chiddy Bang;Icona Pop - Mind Your Manners (feat. Icona Pop).mp3",
    "Colby O'Donis;Akon - What You Got.mp3",
    "Dan + Shay;Justin Bieber - 10,000 Hours.mp3",
    "David Fleming - Last Son.mp3",
    "Jimmy Eat World - The Middle.mp3",
    "Kevin Rudolf;Birdman;Jay Sean;Lil Wayne - I Made It (Cash Money Heroes).mp3",
    "k-os;Saukrates;Nelly Furtado - I Wish I Knew Natalie Portman.mp3",
    "Lifehouse - Hanging By A Moment.mp3",
    "Maino;T-Pain - All the Above (feat. T-Pain).mp3",
    "Mase - All I Ever Wanted.mp3",
    "Mase;Total - What You Want (feat. Total).mp3",
    "Michelle Branch - All You Wanted (20th Anniversary Edition).mp3",
    "Michelle Branch - Everywhere.mp3",
    "Nic D;Vwillz - Daytona.mp3",
    "P-Money;Akon - Keep on Calling (feat. Akon).mp3",
    "Simply Red - Holding Back the Years (2008 Remaster).mp3",
]
for f in misc_files:
    move(NEWMUSIC, f, MISC)

# ─────────────────────────────────────────────
# Summary
# ─────────────────────────────────────────────
remaining = [f for f in os.listdir(NEWMUSIC) if f.endswith(".mp3")]
print(f"\n{'='*50}")
print(f"Errors: {len(errors)}")
for e in errors:
    print(f"  - {e}")
print(f"Remaining in NewMusic: {len(remaining)}")
for r in remaining:
    print(f"  - {r}")
print("="*50)
