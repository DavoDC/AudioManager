# GUI Design Consistency

Rules for keeping `gui/theme.py` and the tab modules in `gui/tabs/` visually
consistent as features get added. Read this before touching `HEAD_HTML` or
adding a new control to a tab. Modelled on StreamPilot's `docs/DESIGN.md`
(the workspace's reference example for this kind of doc) but written from
AudioManager's own theme and tab code, not copied from it.

## Design principles

Dark theme, IBM Plex pairing, sharp panels, pill controls - the whole look
lives in one file, `gui/theme.py` (`HEAD_HTML`, injected once via NiceGUI's
head, plus the CSS custom properties under `:root`). A tab module never
writes its own `<style>` block or inline color literal; it composes the
classes and CSS vars `theme.py` already defines. This is what makes
`apply_mood()` (genre-based accent retinting, see `theme.py`) work at all -
if a tab hardcoded `#5b8cff` instead of `var(--accent)`, that tab would stop
retinting with everything else the moment a mood was applied.

Every screen's content sits in `.panel` cards (`gui/theme.py` line ~109) -
one card per logical group of controls/data, same as StreamPilot's
one-card-per-source rule. The Acquire tab's "Open Playlist Tracks" panel
(`gui/tabs/acquire.py: build()`) is the reference instance: title row, then
input controls, then the data table, all inside one `.panel`.

Every tab opens with `ui.element("header").classes("page")` (h1 + `.meta`
subtitle) before its first panel. `header.page` carries `margin-bottom:20px`
in the shared CSS - this is deliberate breathing room applied identically on
every tab (verified 2026-09-02 while investigating a "gap under Acquire"
question - it's consistent spacing, not an Acquire-specific bug). Don't
special-case a smaller margin on one tab; if the spacing is wrong, fix
`header.page` in `theme.py` so every tab changes together.

## Alignment

Everything should line up and look intentional - a row of controls with one
element sitting higher/lower/bigger than its neighbors reads as broken even
when every individual control is "correct" in isolation. Before shipping a
new row of controls:

- Every element in a `ui.row()` shares the same visual baseline. Quasar
  components (`q-checkbox`, `q-btn`) carry their own internal padding/margin
  that doesn't match plain `ui.label`/`ui.button` sizing - check rendered
  alignment against neighbors, don't assume `align-items:center` alone fixes it
  (see the "Hide downloaded" checkbox fix, `gui/tabs/acquire.py`, which needed
  an explicit `margin:0;padding:0;` override to sit level with the buttons
  beside it).
- Don't stack a second block-level label inside a table `<th>` "for extra
  info" - it grows that header cell (and the whole header row) taller than
  its neighbors. If a number needs surfacing, put it somewhere with its own
  row (a stat tile, the progress bar) rather than doubling up a header cell.
- When two adjacent elements should read as one unit (a toggle and the table
  it filters, a count and the bar it summarizes), duplicating the same
  information in two places is worse than picking one location - it also
  causes the taller-row problem above.

## Color palette

`gui/theme.py`'s `:root` block is the single source of truth - never
introduce a new hex literal in a tab module, reuse one of these CSS vars.

| Var | Value | Used for |
|---|---|---|
| `--bg` | `#14161c` | page background |
| `--panel` | `#1c1f28` | `.panel`, `.stat-tile`, `.rule-card`, `.track-card`, `.review-card` |
| `--panel-border` | `#2a2e3a` | panel/control borders |
| `--text` | `#e6e8ee` | primary text |
| `--text-dim` | `#9aa0ac` | labels, `.note`, muted metadata |
| `--accent` | `#5b8cff` (mood-retintable) | primary actions, active state, links |
| `--accent2` | `#7fd1ae` | positive/done/success (`.delta-pos`, `.st-done`) |
| `--accent3` | `#f2b84b` | warning/attention/dupe (stretch badges, `.gap-note`, `tr.batch-header`) |
| `--accent4` | `#e26d6d` | destructive/error/failed (`.am-btn.danger`, `.st-failed`, err-modal) |
| `--accent5` | `#b98af0` | secondary brand gradient partner (mood-retintable alongside `--accent`) |

Yellow-highlighted rows (e.g. Acquire's "IN NEWMUSIC, NOT IN THIS PLAYLIST"
batch, `gui/tabs/acquire.py`) use `rgba(242,184,75,0.10)` - the `--accent3`
value at low alpha, matching `.gap-note`'s `rgba(242,184,75,.06)` background.
Reuse that alpha-of-accent3 pattern for any new "needs attention, not yet an
error" row highlight rather than inventing a new yellow.

White/dim means normal, an accent color means attention - the same governing
rule StreamPilot's dashboard uses. A row only earns a background tint or a
non-dim text color when it's flagging something (downloaded=done in blue,
extra-in-newmusic=unmatched in yellow, failed=red). Don't tint a row purely
for decoration; once color stops meaning something, the eye stops reading it.

## Typography

- Two font stacks, both declared once in `theme.py`: `--font-body` (IBM Plex
  Sans, everything by default) and `--font-mono` (IBM Plex Mono, for numeric/
  identifier values - stat-tile values, table `.num` cells, console output,
  route strings). Never set a third font family in a tab.
- Buttons and nav links (`.tab-link`) set `font-family:inherit` explicitly in
  their own rules rather than relying on a global `button{}` reset, because
  NiceGUI's Quasar components carry their own font defaults that a bare
  `button{font-family:inherit}` rule doesn't reach. Any new custom button-like
  element should set it explicitly too rather than assuming inheritance.

## Buttons

- Dense, small, outlined is the default for tab-level actions: `ui.button(...).props("dense outline size=sm")`
  (see Acquire's Fetch Tracks / Clear / history buttons). Reserve the filled
  `.am-btn` pill style (`gui/theme.py` line ~112) for primary calls to action
  outside NiceGUI's own button component, not for routine per-tab controls.
- Destructive or de-emphasized actions add `color=grey` or `color=negative`
  via `.props(...)` (see Clear using `color=grey`) rather than a custom class.
- A toggle that filters/hides content (e.g. Acquire's "Hide downloaded"
  checkbox) sits inline in the same `ui.row()` as the buttons it modifies,
  with `.classes("note")` so its label matches `.note`'s dim, small text
  rather than full-brightness body text - it's a filter, not an action.

## Tables

- `am-table` is the base table class; `am-table.acquire-table` is a
  size/padding variant (`gui/theme.py` line ~132) for a table meant to be
  read as a checklist rather than dense data - larger font, more cell
  padding, zebra striping. Reuse `.acquire-table` for any other tab that
  becomes a large interactive checklist rather than inventing new sizing.
- `tr.batch-header` (accent-colored, extra top padding) is the pattern for
  a labelled section break inside one table (e.g. Acquire's "IN NEWMUSIC,
  NOT IN THIS PLAYLIST (N)" row) - reuse it instead of a second table or a
  panel-title split when two logically distinct row groups belong in one view.
- A table is the right choice when rows share fixed columns and the view's
  job is "scan/compare many rows with per-row state" (checkboxes, status,
  sortable columns) - that's the test that justified Acquire's "Open
  Playlist Tracks" staying a table rather than becoming a card list
  (reviewed 2026-09-02: many tracks, per-row Downloaded/extra state, sortable
  columns - a table earns its keep there). A `card-grid` of `.track-card`
  (see Library tab) is the right choice instead when rows are visually rich
  (cover art) and don't need column-aligned comparison.

## Adding a new control checklist

1. Does it need a color? Reuse a `--accent*`/`--text*` var above, never a new hex.
2. Is it a button? `.props("dense outline size=sm")` unless it's a genuine
   primary CTA, in which case use `.am-btn`.
3. Is it a filter/toggle rather than an action? Style its label `.note`,
   place it inline with the buttons/table it affects, and make sure the
   underlying state (see Acquire's `_state["hide_downloaded"]`) is read at
   render time in the same `@ui.refreshable` function it filters, not
   duplicated into a second code path.
4. Is it tabular data? Check whether `.am-table` / `.acquire-table` already
   fits before adding new table CSS; check whether a table is even the right
   shape (see the Tables section above) before reaching for one.
5. Does it need to react to live state (poll timer, fetch result)? Call the
   owning `@ui.refreshable` function's `.refresh()` after the state changes,
   the way `_poll_downloads()` and `clear()` do in `gui/tabs/acquire.py`.
