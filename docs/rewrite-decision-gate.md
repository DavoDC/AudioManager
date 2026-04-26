---
name: AudioManager - Python rewrite vs .NET 8 migration
description: Strategic fork in AudioManager: evaluate Python rewrite vs continuing with .NET 8 migration
type: project
---

# AudioManager Strategic Decision: Python Rewrite vs .NET 8 Migration

**Date:** 2026-04-26
**Status:** Pending evaluation (TIER 2 decision gate in IDEAS.md)

## The Fork

Current plan (TIER 2 in IDEAS.md): survey .NET 8 migration blockers, then migrate to SDK-style csproj.

Alternative: rewrite the console app in Python instead of migrating .NET.

## Why Python is Being Considered

- **No build step** - Python script runs directly, no compilation dependency
- **No VS2022 dev dependency** - lighter development environment
- **Lightweight dependencies** - modern Python audio metadata libs (mutagen, tinytag) vs .NET/TagLib#
- **ROI question** - which approach costs fewer tokens and yields better dev experience?

**Why this matters:** David prefers lightweight languages and no-build tooling. C# and C++ aren't readable during Claude sessions. Python would be simpler to maintain and extend.

## Decision Criteria

1. **Token cost:** Opus planning + Haiku execution for Python rewrite vs straight .NET 8 migration
2. **Dependency check:** confirm Python has audio metadata equivalent (expect `mutagen` to cover TagLib# use cases)
3. **Scope match:** ensure Python can handle the integration/analysis workload (it can - it's not CPU-bound, just file I/O and XML generation)

## Next Steps (for David)

1. Decide whether to evaluate Python rewrite (2-3 hour planning + evaluation)
2. If yes: `Opus` plans Python approach, `Haiku` implements it
3. If no: proceed with TIER 2 .NET migration as written

**DO NOT start TIER 2 until this decision is made.**

See `AudioManager/docs/IDEAS.md` - "TIER 2 - DECISION GATE: Python Rewrite vs .NET 8 Migration" section for the full decision criteria.
