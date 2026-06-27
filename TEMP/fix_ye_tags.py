"""
fix_ye_tags.py - Fix Artist tags for Kanye West tracks in the AudioManager library.

Finds all .mp3 files under Artists\Kanye West\ where the Artist ID3 tag
contains "Ye" (or variations) but should be "Kanye West".

Usage:
  python fix_ye_tags.py              # dry-run: shows what would change
  python fix_ye_tags.py --write      # applies the fix

Always dry-run first and review before writing.
"""

import sys
import os
import time
from pathlib import Path

try:
    import mutagen
    from mutagen.mp3 import MP3
    from mutagen.id3 import ID3, TPE1, ID3NoHeaderError
except ImportError:
    print("ERROR: mutagen not installed. Run: pip install mutagen")
    sys.exit(1)

# --- Config ---
# Library root follows AudioManager Constants.AudioFolderPath = %USERPROFILE%\Audio
LIBRARY_ROOT = Path(os.environ.get("USERPROFILE", r"C:\Users\David")) / "Audio" / "Artists" / "Kanye West"
OLD_ARTIST = "Ye"  # must match exactly (case-insensitive)
NEW_ARTIST = "Kanye West"
WRITE_MODE = "--write" in sys.argv

# ---

def get_artist_tag(path):
    """Return the first TPE1 artist value, or None."""
    try:
        tags = ID3(path)
        tpe1 = tags.get("TPE1")
        if tpe1:
            return str(tpe1)
        return None
    except Exception:
        return None


def main():
    start = time.time()
    print(f"\nfix_ye_tags.py - {'WRITE MODE' if WRITE_MODE else 'DRY RUN'}")
    print(f"Scanning: {LIBRARY_ROOT}")
    print()

    if not LIBRARY_ROOT.exists():
        print(f"ERROR: Library path not found: {LIBRARY_ROOT}")
        sys.exit(1)

    mp3_files = sorted(LIBRARY_ROOT.rglob("*.mp3"))
    print(f"Found {len(mp3_files)} MP3 file(s) under {LIBRARY_ROOT.name}/")
    print()

    to_fix = []
    for mp3 in mp3_files:
        artist = get_artist_tag(mp3)
        if artist is None:
            continue
        # Check if any semicolon-separated performer is "Ye" (exact, case-insensitive)
        performers = [p.strip() for p in artist.split(";")]
        needs_fix = any(p.lower() == OLD_ARTIST.lower() for p in performers)
        if needs_fix:
            to_fix.append((mp3, artist))

    if not to_fix:
        print(f"No files found with Artist = '{OLD_ARTIST}'. Nothing to do.")
        elapsed = time.time() - start
        print(f"\nDone in {elapsed:.1f}s")
        return

    print(f"Files to update ({len(to_fix)}):")
    for mp3, artist in to_fix:
        rel = mp3.relative_to(LIBRARY_ROOT.parent)
        new_artist = ";".join(
            NEW_ARTIST if p.strip().lower() == OLD_ARTIST.lower() else p.strip()
            for p in artist.split(";")
        )
        print(f"  {rel}")
        print(f"    Artist: \"{artist}\" -> \"{new_artist}\"")
    print()

    if not WRITE_MODE:
        print("DRY RUN complete. Run with --write to apply changes.")
        elapsed = time.time() - start
        print(f"Time: {elapsed:.1f}s")
        return

    # --- Apply changes ---
    fixed = 0
    errors = []
    for mp3, old_artist in to_fix:
        new_artist = ";".join(
            NEW_ARTIST if p.strip().lower() == OLD_ARTIST.lower() else p.strip()
            for p in old_artist.split(";")
        )
        try:
            tags = ID3(mp3)
            tags["TPE1"] = TPE1(text=new_artist)
            tags.save(mp3)
            rel = mp3.relative_to(LIBRARY_ROOT.parent)
            print(f"  [FIXED] {rel}")
            fixed += 1
        except Exception as e:
            errors.append((mp3, str(e)))
            print(f"  [ERROR] {mp3.name}: {e}")

    print()
    if errors:
        print(f"ERRORS ({len(errors)}):")
        for mp3, err in errors:
            print(f"  {mp3.name}: {err}")
    print(f"Fixed {fixed}/{len(to_fix)} file(s).")
    print()
    print("IMPORTANT: Run AudioManager 'analysis --force-regen' after this to update AudioMirror XMLs.")

    elapsed = time.time() - start
    print(f"Time: {elapsed:.1f}s")


if __name__ == "__main__":
    main()
    input("\nPress Enter to close...")
