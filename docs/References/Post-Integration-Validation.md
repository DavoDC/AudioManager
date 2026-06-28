# Post-Integration Validation Correctness

Why post-integration LibChecker warnings are often false, and how to make
"a clean integration leaves a clean library" a reliable, testable property.

Case study: real integration run on 2026-06-28 (`logs/REAL_INTEGRATION_Sun_28.txt`).

**This is reference only - mechanism, proof, and design rationale.** Every
actionable item (fixes, investigations) is tracked in `docs/Development/IDEAS.md`
TIER 1, not here. Read this to understand *why*; act from IDEAS so nothing is
missed.

---

## The defect in one sentence

**Post-integration validation regenerates the mirror incrementally
(`Recreated: False`), and incremental regen never removes XMLs for files the
integration just deleted or moved - so the mirror it validates describes a
library that no longer exists, and LibChecker flags the ghosts.**

## The proof (from the 2026-06-28 run)

Post-integration regen reported:

- `MP3 file count: 5693`   (actual files on disk)
- `Tags parsed: 5702`      (XML entries in the mirror)

Delta = **9 ghost XMLs**. That is exactly the number of library locations the
integration vacated on this run:

- 8 duplicate `[L]` decisions deleted the library copy
  (Akira The Don - KALI YUGA, Chiddy Bang;Icona Pop - Mind Your Manners,
  Counting Crows;Vanessa Carlton - Big Yellow Taxi, Jay-Z - Song Cry,
  Jay-Z;Eminem - Renegade, Kanye West - BEAUTY AND THE BEAST / DAMN / PREACHER MAN)
- 1 Misc migration moved `Chiddy Bang - Dream Chasin'.mp3` out of
  `Miscellaneous Songs/` into `Artists/Chiddy Bang/Singles/`

The MP3 was removed from disk in every case; the XML stayed in the mirror.

## Mapping every warning to its cause

| LibChecker warning (post-integration) | Cause |
|---|---|
| `Chiddy Bang - Dream Chasin'` duplicated (Singles + Misc) | Misc ghost - file was moved, old XML remains |
| `Kanye West - PREACHER MAN/DAMN/BEAUTY` duplicated (BULLY-DELUXE + BULLY) | `BULLY\` ghosts - library copies deleted by `[L]` |
| `Jay-Z - Song Cry` duplicated (The Blueprint + Singles) | `Singles\` ghost - library copy deleted by `[L]` |
| `Chiddy Bang has Artists folder but song in Miscellaneous Songs` | same Misc ghost as above |
| `The Notorious B.I.G. ... only 1 song from album ... should be Singles/` | **GENUINE** - album-suffix split (see below) |

11 of 12 hits are ghosts. One is real. A user who treats the report at face
value would hunt for duplicates that do not exist and risk editing or deleting
real library files chasing phantoms - directly against the data-safety rule.

## Confirming ghost vs real (operational)

Re-run `analysis --force-regen --no-input --no-auto-commit`. Force regen deletes
the mirror and rebuilds it from disk, so ghosts vanish. Any warning that
survives a force-regen is real. The 9-XML count delta in the regen output is the
fast tell: parsed > on-disk means ghosts are present.

---

## Root cause: incremental regen is add-only

Reflector incremental mode creates XMLs for new MP3s and refreshes XMLs whose MP3
is newer, but it **never deletes** an XML whose MP3 is gone. The existing docs
already note this for analysis ("force regen handles deletion cleanup"). The gap
is that **integration itself deletes and moves library files** (duplicate `[L]`,
Misc migration), and then validates with incremental regen. The add-only mirror
is structurally guaranteed to carry ghosts after any such run.

This same asymmetry leaks into ParseCache (a deleted MP3's cached row persists
until force regen) and is the reason "is this a ghost or a real duplicate?"
recurs in diagnosis. Removing the asymmetry removes the whole class.

## The solution space (rationale only - actions in IDEAS TIER 1)

Three approaches exist, smallest to deepest. They are tracked as IDEAS items;
the rationale is recorded here so the trade-offs are not re-derived later.

1. **Interim - force-regen before post-integration LibChecker.** Correct, ~1s
   cost, minimal change, but wastefully rebuilds the whole mirror for the sake of
   a handful of deletions.

2. **Deeper - incremental Reflector prunes orphaned XMLs.** Makes incremental
   regen fully truthful, which removes the post-integration ghost class AND the
   ParseCache "deleted MP3 persists" caveat AND the "force regen required after
   deletion" footnotes everywhere. The one with the widest blast radius of
   simplification.

3. **Best (shift-left) - validate the projected final state in the dry run.**
   Catches real issues before any file moves and is inherently ghost-free
   because it models deletions correctly. Mechanism described below.

---

## The projection: validate before moving (the high-leverage idea)

The dry run already computes, for the batch: every fixed tag, every routing
destination, and every library file it will delete. That is enough to build the
exact post-integration mirror **in memory, without moving anything and without
needing the MP3s** - because LibChecker consumes `List<TrackTag>`, not MP3 files.

Projected mirror =
  (current parsed tags)
  MINUS the TrackTags whose path the integrator will delete or move-from
  PLUS synthesized TrackTags for each new file at its computed destination path
      carrying the post-TagFixer tag values.

Run LibChecker on that projected list. The result predicts precisely the
post-integration library state - with none of the ghost false positives, because
the projection removes vacated entries by construction.

What this would have done on 2026-06-28:

- Zero duplicate warnings (projection drops the deleted library copies).
- Zero Misc-folder warning (projection moves Dream Chasin').
- One warning surfaced in the dry run, before committing: the
  `The Notorious B.I.G.` album-suffix split - fixable while files are still
  staged.

This turns the goal ("after a real integration there should be no LibChecker
errors") into something achievable and enforceable: the dry run prints a
projected-LibChecker section, and integration refuses to proceed (or loudly
requires an override) when the projection is not clean.

Feasibility note: no actual MP3 reads are required for the projection - the
integrator already holds the fixed tag data for every batch file, and LibChecker
already operates on TrackTag objects. The only new work is synthesizing
destination-path TrackTags and splicing the list.

(Build action tracked in IDEAS TIER 1 "Dry-run projected LibChecker".)

---

## The genuine issue this run surfaced: album-suffix split

`The Notorious B.I.G.;Angela Winbush;Jay-Z - I Love The Dough (2014 Remaster)`
routed to `Artists/The Notorious B.I.G./Life After Death (2014 Remastered Edition)/`
with reason "4 songs from album", but LibChecker then reported "only 1 song from
album ... should be Singles/".

Two things are wrong and they compound:

- **Album-suffix normalization is incomplete and ad hoc.** TagFixer strips
  `(Explicit Version)`, `(Expanded Edition)`, `(feat. ...)` but not
  `(2014 Remastered Edition)`. The new file landed in a `Life After Death
  (2014 Remastered Edition)/` subfolder distinct from the library's existing
  `Life After Death/` tracks, splitting the album.
- **Scan-ahead album-count and on-disk folder placement use different
  normalization.** Scan-ahead counted 4 songs (matched the album loosely);
  placement used the exact suffixed album name (landed the file alone).
  Routing said "album subfolder is fine"; LibChecker said "should be Singles".
  That is a routing<->LibChecker divergence of the same family already noted in
  DevContext "Routing-LibChecker threshold parity".

The direction (tracked in IDEAS TIER 1 "Album version-suffix normalization"): a
single canonical album-version normalizer (strip / fold Remaster(ed),
Anniversary, Mono, Stereo, Expanded, and a consistent Deluxe-vs-base rule) used
by BOTH the album-count logic and the destination-path builder, so count and
placement can never disagree.

---

## Secondary observation (not yet proven a bug)

In the confidence report, every multi-artist destination filename keeps the
`"; "` (space after semicolon) form while the corresponding artist tag is
normalized to `";"` (no space) - e.g. file
`Akira The Don; David Lynch - KALI YUGA.mp3` vs tag `Akira The Don;David Lynch`.
LibChecker's filename-vs-tag check did not flag this.

Either (a) the on-disk filename is actually correct and only the log display is
stale, or (b) TagFixer normalizes the tag's artist separator but the file rename
uses the un-normalized artist string, producing a real tag/filename mismatch that
LibChecker's `CheckFilenamesCasingsMatchArtistTags` does not catch (it compares
casing, perhaps not separator spacing).

The confirmation step and follow-up are tracked in IDEAS TIER 1 "Multi-artist
destination filename keeps '; '". If confirmed, it is two defects: a TagFixer
rename normalization gap AND a LibChecker filename-check gap.
