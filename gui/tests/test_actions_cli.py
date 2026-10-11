"""CLI parity: every GUI action is listed with the command line that does it."""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))

import pytest

from gui import actions, cli


def test_registry_lists_every_gui_action():
    ids = {a["id"] for a in actions.describe()}
    assert ids >= {"analysis", "force-regen", "scan", "tagfix-dry-run", "integrate", "mirror-commit"}


def test_each_action_has_cli_text_and_flags():
    for a in actions.describe():
        assert a["cli"].startswith("python -m gui.cli run ")
        assert isinstance(a["mutates"], bool)


def test_mutating_action_requires_yes(capsys):
    rc = cli.main(["run", "force-regen"])
    assert rc == 2
    assert "--yes" in capsys.readouterr().err


def test_dry_run_prints_command_without_running(capsys):
    rc = cli.main(["run", "scan", "--print"])
    assert rc == 0
    out = capsys.readouterr().out
    assert "integrate" in out and "--dry-run" in out


def test_unknown_action(capsys):
    assert cli.main(["run", "nope"]) == 2


def test_state_subcommand_prints_json(capsys):
    import json
    assert cli.main(["state", "stats"]) == 0
    assert json.loads(capsys.readouterr().out)["tab"] == "stats"


def test_log_subcommand_runs(capsys):
    assert cli.main(["log"]) == 0
