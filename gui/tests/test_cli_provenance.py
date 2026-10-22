"""CLI provenance: every `python -m gui.cli <verb>` prints the build line to stderr first.

Stdout stays clean so JSON and pipe output are unaffected.
"""
import subprocess
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(REPO_ROOT))

from gui import buildinfo  # noqa: E402


def test_build_verb_prints_build_line_to_stderr_first():
    expected = buildinfo.format_line(buildinfo.describe())
    proc = subprocess.run([sys.executable, "-m", "gui.cli", "build"], cwd=str(REPO_ROOT),
                          capture_output=True, text=True, timeout=60)
    assert proc.returncode == 0, proc.stderr
    first_err = proc.stderr.splitlines()[0]
    assert first_err == expected
    assert first_err.startswith("AudioManager ")
    assert "unknown" in first_err or len(first_err.split()[1]) >= 4
    # Stdout carries only the verb's own output: the build line, once.
    assert proc.stdout.strip() == expected


def test_state_verb_keeps_stdout_pure_json_and_provenance_on_stderr():
    import json
    expected = buildinfo.format_line(buildinfo.describe())
    proc = subprocess.run([sys.executable, "-m", "gui.cli", "state"], cwd=str(REPO_ROOT),
                          capture_output=True, text=True, timeout=60)
    assert proc.returncode == 0, proc.stderr
    assert proc.stderr.splitlines()[0] == expected
    json.loads(proc.stdout)  # raises if the build line leaked into stdout
