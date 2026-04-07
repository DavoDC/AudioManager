# Integration Workflow

Steps for integrating a new music batch into the library.

1. Survey NewMusic - read tags, count per-artist
2. Write plan markdown to `docs/` (see `NewMusic-Integration-Plan.md` as a past example)
3. Review plan before executing
4. Execute: set TCMP, fix tags, rename files, move to Audio
5. Run AudioManager (Analysis -> Yes regen) -> review report
6. Fix any issues flagged, re-run if needed
