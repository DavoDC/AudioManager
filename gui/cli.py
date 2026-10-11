"""Command line for everything the GUI does.

  python -m gui.cli actions                 list actions with their CLI lines
  python -m gui.cli run ID [--yes] [--print] [--manifest PATH] [--message TEXT]
  python -m gui.cli state [TAB]             print the state contract as JSON
  python -m gui.cli build                   print the build line
  python -m gui.cli log [-n N]              tail today's Claude-visible log

Mutating actions (see `actions`) refuse to run without --yes.
"""
from __future__ import annotations

import argparse
import json
import subprocess
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from gui import actions, applog, buildinfo, config, uistate  # noqa: E402


def _build_command(action: dict, ns) -> list[str] | None:
    args = list(action["args"])
    if action.get("needs") == "manifest":
        if not ns.manifest:
            print("error: --manifest PATH is required for this action", file=sys.stderr)
            return None
        args = [ns.manifest if a == "<MANIFEST>" else a for a in args]
    if action.get("needs") == "message":
        if not ns.message:
            print("error: --message TEXT is required for this action", file=sys.stderr)
            return None
        args = [ns.message if a == "<MESSAGE>" else a for a in args]
    if action["kind"] == "exe":
        if "--no-input" not in args:
            args.append("--no-input")
        return [str(config.EXE_PATH), *args]
    return ["git", "-C", str(config.AUDIOMIRROR_REPO), *args]


def _cmd_run(ns) -> int:
    action = actions.get(ns.id)
    if action is None:
        print(f"error: unknown action {ns.id!r}; try: python -m gui.cli actions", file=sys.stderr)
        return 2
    if action["mutates"] and not ns.yes and not ns.print:
        print(f"error: {ns.id} changes files; re-run with --yes (or --print to see the command)",
              file=sys.stderr)
        return 2
    cmd = _build_command(action, ns)
    if cmd is None:
        return 2
    if ns.print:
        print(subprocess.list2cmdline(cmd))
        return 0
    applog.write("cli", f"run {ns.id}: {subprocess.list2cmdline(cmd)}")
    return subprocess.call(cmd, cwd=str(config.REPO_ROOT))  # window-ok: CLI runs in a console


def main(argv: list[str] | None = None) -> int:
    p = argparse.ArgumentParser(prog="python -m gui.cli", description=__doc__,
                                formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = p.add_subparsers(dest="cmd", required=True)
    sub.add_parser("actions")
    r = sub.add_parser("run")
    r.add_argument("id")
    r.add_argument("--yes", action="store_true")
    r.add_argument("--print", action="store_true", help="print the command, do not run it")
    r.add_argument("--manifest")
    r.add_argument("--message")
    s = sub.add_parser("state")
    s.add_argument("tab", nargs="?")
    sub.add_parser("build")
    lg = sub.add_parser("log")
    lg.add_argument("-n", type=int, default=50)
    ns = p.parse_args(argv)

    if ns.cmd == "actions":
        print(json.dumps(actions.describe(), indent=2))
        return 0
    if ns.cmd == "run":
        return _cmd_run(ns)
    if ns.cmd == "state":
        from gui.state import state
        state.load()
        payload = uistate.tab_state(ns.tab) if ns.tab else uistate.full_state()
        if payload is None:
            print(f"error: unknown tab {ns.tab!r}", file=sys.stderr)
            return 2
        print(json.dumps(payload, indent=2, default=str))
        return 0
    if ns.cmd == "build":
        print(buildinfo.format_line(buildinfo.describe()))
        return 0
    if ns.cmd == "log":
        for line in applog.tail(ns.n):
            print(line)
        return 0
    return 2


if __name__ == "__main__":
    sys.exit(main())
