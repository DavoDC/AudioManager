"""Registry of GUI actions and their command-line equivalents.

The GUI never mutates the library itself: each button shells out to the C#
exe (gui/runner.py) or, for the mirror commit, to git. This registry lists the
same invocations so `python -m gui.cli run <id>` does exactly what the button
does. Behaviour of the invocations is unchanged; this is metadata plus a
thin launcher.
"""
from __future__ import annotations

# kind "exe": args passed to AudioManager.exe. kind "git": args passed to git -C <AudioMirror>.
ACTIONS = [
    {"id": "analysis", "label": "Re-run analysis (Statistics)", "kind": "exe",
     "args": ["analysis", "--json-output"], "mutates": False,
     "note": "Rewrites logs/analysis-stats.json and tracks.json"},
    {"id": "force-regen", "label": "Force full regen (Statistics)", "kind": "exe",
     "args": ["analysis", "--json-output", "--force-regen", "--no-auto-commit"], "mutates": True,
     "note": "May rewrite AudioMirror XML; never auto-commits"},
    {"id": "scan", "label": "Scan NewMusic (Integration dry run)", "kind": "exe",
     "args": ["integrate", "--dry-run", "--json-output"], "mutates": False,
     "note": "No files change"},
    {"id": "tagfix-dry-run", "label": "Run Fixed Rules (Tag Fix dry run)", "kind": "exe",
     "args": ["tagfix", "--dry-run"], "mutates": False, "note": "No files change"},
    {"id": "integrate", "label": "Integrate accepted tracks (Integration)", "kind": "exe",
     "args": ["integrate", "--manifest", "<MANIFEST>"], "mutates": True,
     "needs": "manifest",
     "note": "Decisions come from the review stage; pass --manifest PATH to a "
             "gui/.cache/manifests/accepted-manifest-*.json file"},
    {"id": "mirror-commit", "label": "Commit AudioMirror (Mirror)", "kind": "git",
     "args": ["commit", "-am", "<MESSAGE>"], "mutates": True, "needs": "message",
     "note": "Pass --message TEXT; never pushes"},
]


def get(action_id: str) -> dict | None:
    return next((a for a in ACTIONS if a["id"] == action_id), None)


def describe() -> list[dict]:
    out = []
    for a in ACTIONS:
        flags = " --yes" if a["mutates"] else ""
        extra = {"manifest": " --manifest PATH", "message": " --message TEXT"}.get(a.get("needs", ""), "")
        out.append({"id": a["id"], "label": a["label"], "mutates": a["mutates"],
                    "cli": f"python -m gui.cli run {a['id']}{extra}{flags}", "note": a["note"]})
    return out
