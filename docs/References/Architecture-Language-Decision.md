# Architecture & Language Decision: Stay Polyglot

**Status: Decided 2026-09-06. Do not relitigate without a new trigger event (see below).**

This closes the decision gate parked at `docs/Development/HISTORY.md` ("Settled / Not Doing" -> "DECISION GATE: Python Rewrite vs .NET 8 Migration"). That entry listed "confirm Python libs cover TagLib# use cases" as the open factor blocking a decision. This document answers it with evidence and settles the gate.

## Bottom line

**Keep the C#/Python split. Do not port to a single language.** The split is a sound division of responsibility, not accumulated accretion. The actual weak point is the contract at the C#/Python boundary, not the choice of two languages - see "What to fix instead" below.

## Why the split is sound

- **C# owns everything that writes to the library.** Integration, tag-fixing, and mirror commits are all C#-only (`Program.cs`). The Python GUI is documented and enforced as read-only against library data (`gui/config.py`), with two narrow, sanctioned exceptions: reading tags/art via `mutagen` for the Acquire tab and thumbnails (`gui/art.py`) - it never parses the AudioMirror XML or reimplements C# logic.
- **No logic is duplicated across the language boundary.** The one real case of duplicated logic in the program (`MusicIntegrator.GetDestDir` vs `LibChecker` independently encoding routing rules - see IDEAS.md) is *within* C#, not across languages.
- **Python is the right tool for the part that's actually growing.** The GUI is NiceGUI + ECharts specifically to avoid a JS build step. It is also the larger, faster-moving side of the codebase today. A port to C# would mean rebuilding that dashboard in a .NET UI stack for no user-visible benefit.

## Why TagLib# is not a constraint on porting

This was the open question. Answer: **it is not a real constraint**, for a specific, checkable reason - the program never uses the part of TagLib# that Python would struggle to match.

- The program is **MP3/ID3v2-only end to end**. Every file-enumeration site in the C# core globs `*.mp3` only (`Analyser.cs`, `MusicIntegrator.cs`, `TagFixer.cs`, `Reflector.cs`, `TracksJson.cs`), and destination filenames are hardcoded `.mp3`. `docs/References/GUI-Architecture.md` currently describes TagLib# as handling "ID3, Vorbis, APE, FLAC tags" - true of the library in general, but not a capability this program exercises anywhere. (Backlog item filed to correct that line - see IDEAS.md.)
- The actual TagLib# surface used is small: `Title`, `JoinedPerformers`/`Performers`, `Album`, `Year`, `Track`, `JoinedGenres`/`Genres`, `Pictures`, and the ID3v2 TCMP (compilation) flag. That's the whole read+write surface (`TrackTag.cs`, `TagFixer.cs`).
- **`mutagen` already does the read side of this job, today, in this repo, against these same files.** It's a pinned dependency (`gui/requirements.txt`), and `gui/art.py` already opens `mutagen.id3.ID3` on library MP3s in production; `GUI-Architecture.md`'s own Acquire-tab section documents `mutagen.easyid3.EasyID3` reading Album/Year/Length. Pillow (also already a dependency) already decodes the same cover art that `TrackTag.cs` currently hand-parses PNG/JPEG headers for.

So the one thing that looked like it might be load-bearing turns out not to be. That said, a port would still be a **careful migration, not a mechanical translation** if it were ever done - three specific write-side risks would need verification first (not blockers, just due diligence for whoever picks this up):

1. **ID3 version on save.** TagLib# and mutagen don't necessarily default to the same ID3v2 minor version; changing this on ~5,700 already-tagged files could change how other tools read them. Verify with a round-trip diff on a copied sample before trusting it.
2. **TCMP (compilation flag).** TagLib#'s `IsCompilation` is a convenience over the non-standard iTunes `TCMP` frame; mutagen would need an explicit frame write instead. Verify iTunes/Mp3tag still read it correctly after a round trip.
3. **Multi-artist join separator.** `TagLib#.JoinedPerformers` uses `"; "`; `TagFixer.cs` currently joins with `";"` (no space) - already a documented live divergence risk (see IDEAS.md). Any reimplementation must match whatever the existing ~5,700-track library was actually written with, not the "correct" one.

## What would actually justify revisiting this

Not a calendar date. The trigger: **the next time a single user-visible feature genuinely requires a code change in both languages** (not a C#-side feature with a thin Python front-end, which has happened before and does not count). If and when a port is ever justified, the direction is **Python**, not C# - the growth is on that side, and the format constraint (MP3/ID3v2 only) is one mutagen already covers.

## What to fix instead (see IDEAS.md backlog)

The real problem this review surfaced is that the C#/Python contract is versioned in inverse proportion to risk: the read-only statistics JSON has a schema version and fails loudly on mismatch, while the path that permanently moves files has the GUI inferring what happened by pattern-matching human-readable console text. That - not the language split - is where the fragility actually lives. Filed as backlog items in `docs/Development/IDEAS.md`; this document only settles the language question.

## Separate, cheaper, and NOT the same decision

The .NET Framework 4.8 legacy-csproj question (hand-registered `<Compile Include>` list, which has already caused a real build failure) is a real and much cheaper decision than the language question, and the referenced TagLib# assembly is already `netstandard2.0`-compatible. It should not be held hostage by the language decision above - filed as its own backlog item.
