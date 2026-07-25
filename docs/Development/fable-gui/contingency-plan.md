# Fable Session Contingency Plan

Covers: when to start, what happens if usage runs out mid-build, what happens if the promo window closes first.

## When to start

**Don't wait for 0% usage.** Promo window is a hard deadline (2026-07-07 11:59:59 PM PT), not a rolling reset. As of 2026-07-02 Fable was at 0% used with ample headroom - start as soon as the brief is ready, don't hold off "to be safe."

## If usage runs out mid-session

Claude Code sessions preserve full conversation/tool-call history across a usage-limit pause. The limit message states the reset time.

1. **Leave the terminal/window open.** Don't kill the process.
2. When the reset time passes, **resume the same session** (`--resume` / `--continue`), don't start a fresh one - Fable keeps full context of what it already built and doesn't re-research or re-decide things it already decided.
3. This is why the brief requires frequent git commits (one per tab/panel completed, not one commit at the end) - if the session is killed instead of cleanly paused, committed work survives; uncommitted work doesn't.

## If the promo window closes before the build finishes

Work continuing past 2026-07-07 11:59:59 PM PT runs on paid usage credits, not free promo access - a real-money decision, not Fable's to make silently.

**Brief instruction to Fable:** if you're still actively building as the deadline approaches, stop and report status (what's done, what's not, current git state) rather than assuming continuation onto credits is fine. David decides whether to burn credits to finish or pick it up as a normal Sonnet task later.

## 1M context decision (separate from the above)

`CLAUDE_CODE_DISABLE_1M_CONTEXT=1` is currently set globally (see the maintainer's private notes on a resume-sticky context incident) - this blocks the `[1m]` model variants from even appearing in the picker. This build's "go deep, don't run out of context mid-build" goal may want 1M context given the scope (six tabs, full chart library, deep research folded in). But 1M context is never free - it draws usage credits on every plan, Fable's 1M rate likely draws faster than Sonnet's.

**Decision made (2026-07-02): enable 1M context for this build.** Before starting the Fable session, temporarily unset `CLAUDE_CODE_DISABLE_1M_CONTEXT` in both `~/.claude/settings.json` and the workspace `.claude/settings.json`, select `fable[1m]` explicitly via `/model`. **Re-set the env var back to `1` immediately after this session ends** so it doesn't stick across future resumed sessions (the whole reason that var exists - see the maintainer's private notes for the incident that caused it). Don't leave it unset "just in case" once this build wraps up.
