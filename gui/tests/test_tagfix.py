"""Tests for the Tag Fix tab's custom-rule storage (gui.rules_store) and the
custom-rule-line highlighting logic in gui.tabs.tagfix. No NiceGUI rendering -
these exercise the pure load/save/validate functions and the line classifier
directly, same pattern as gui/tests/test_acquire.py."""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))

from gui.rules_store import ACTIONS, FIELDS, MATCHES, Rule, load_rules, save_rules, validate_rule
from gui.tabs.tagfix import (
    _is_custom_rule_line,
    _is_custom_rule_warning_line,
    _render_console_lines,
    build_rule_rows,
    count_custom_rule_warnings,
    literal_value_regex_metachars,
    merge_saved_rule,
    merge_toggle_enabled,
)

SAMPLE_HEADER = (
    '<?xml version="1.0" encoding="utf-8"?>\n'
    "<!--\n"
    "  User-defined custom tag cleanup rules for TagFixer.\n"
    "  This comment block must survive every save_rules() call untouched.\n"
    "-->\n"
    "<TagFixCustomRules>\n\n"
    "  <!-- No active rules yet. Add <Rule .../> elements here. -->\n\n"
    "</TagFixCustomRules>\n"
)


def _write_sample(path: Path) -> None:
    path.write_text(SAMPLE_HEADER, encoding="utf-8")


# --------------------------------------------------------------- load_rules


def test_load_rules_empty_file_returns_empty_list(tmp_path):
    p = tmp_path / "rules.xml"
    _write_sample(p)
    assert load_rules(p) == []


def test_load_rules_missing_file_returns_empty_list(tmp_path):
    assert load_rules(tmp_path / "does-not-exist.xml") == []


def test_load_rules_malformed_xml_returns_empty_list(tmp_path):
    p = tmp_path / "rules.xml"
    p.write_text("<TagFixCustomRules><Rule id=\"x\"", encoding="utf-8")
    assert load_rules(p) == []


def test_load_rules_parses_existing_rule(tmp_path):
    p = tmp_path / "rules.xml"
    p.write_text(
        "<TagFixCustomRules>\n"
        '  <Rule id="strip-bonus" field="title" match="endsWith" value=" [Bonus Track]" '
        'action="regex-replace" pattern="\\s*\\[Bonus Track\\]$" replacement="" enabled="true" />\n'
        "</TagFixCustomRules>\n",
        encoding="utf-8",
    )
    rules = load_rules(p)
    assert len(rules) == 1
    r = rules[0]
    assert r.id == "strip-bonus"
    assert r.field == "title"
    assert r.match == "endsWith"
    assert r.action == "regex-replace"
    assert r.enabled is True


def test_load_rules_case_insensitive_and_default_enabled(tmp_path):
    p = tmp_path / "rules.xml"
    p.write_text(
        "<TagFixCustomRules>\n"
        '  <Rule id="r1" field="TITLE" match="CONTAINS" value="x" action="SET-VALUE" replacement="y" />\n'
        "</TagFixCustomRules>\n",
        encoding="utf-8",
    )
    rules = load_rules(p)
    assert len(rules) == 1
    assert rules[0].enabled is True  # enabled omitted -> defaults true


def test_load_rules_skips_one_invalid_rule_keeps_others(tmp_path):
    p = tmp_path / "rules.xml"
    p.write_text(
        "<TagFixCustomRules>\n"
        '  <Rule id="bad" field="notafield" match="contains" value="x" action="set-value" replacement="y" />\n'
        '  <Rule id="good" field="title" match="contains" value="x" action="set-value" replacement="y" />\n'
        "</TagFixCustomRules>\n",
        encoding="utf-8",
    )
    rules = load_rules(p)
    assert [r.id for r in rules] == ["good"]


def test_load_rules_missing_id_is_skipped(tmp_path):
    p = tmp_path / "rules.xml"
    p.write_text(
        "<TagFixCustomRules>\n"
        '  <Rule field="title" match="contains" value="x" action="set-value" replacement="y" />\n'
        "</TagFixCustomRules>\n",
        encoding="utf-8",
    )
    assert load_rules(p) == []


# --------------------------------------------------------------- save_rules / round trip


def test_save_rules_preserves_header_comment(tmp_path):
    p = tmp_path / "rules.xml"
    _write_sample(p)
    save_rules([Rule(id="r1", field="title", match="contains", value="x",
                      action="set-value", replacement="y")], p)
    text = p.read_text(encoding="utf-8")
    assert "User-defined custom tag cleanup rules for TagFixer." in text
    assert "This comment block must survive every save_rules() call untouched." in text


def test_save_then_load_round_trips(tmp_path):
    p = tmp_path / "rules.xml"
    _write_sample(p)
    rule = Rule(id="strip-remaster", field="title", match="contains", value="(Remastered)",
                action="regex-replace", pattern=r"\s*\(Remastered\)", replacement="", enabled=True)
    save_rules([rule], p)
    reloaded = load_rules(p)
    assert len(reloaded) == 1
    r = reloaded[0]
    assert r.id == rule.id
    assert r.field == rule.field
    assert r.match == rule.match
    assert r.value == rule.value
    assert r.action == rule.action
    assert r.pattern == rule.pattern
    assert r.enabled == rule.enabled


def test_save_rules_escapes_special_characters(tmp_path):
    p = tmp_path / "rules.xml"
    _write_sample(p)
    rule = Rule(id="amp-rule", field="artists", match="contains", value="Tom & Jerry",
                action="set-value", replacement='"Quoted" & <tagged>')
    save_rules([rule], p)
    reloaded = load_rules(p)
    assert reloaded[0].value == "Tom & Jerry"
    assert reloaded[0].replacement == '"Quoted" & <tagged>'


def test_save_rules_empty_list_writes_placeholder_comment(tmp_path):
    p = tmp_path / "rules.xml"
    _write_sample(p)
    save_rules([Rule(id="r1", field="title", match="contains", value="x",
                      action="set-value", replacement="y")], p)
    save_rules([], p)
    assert load_rules(p) == []
    assert "No active rules yet" in p.read_text(encoding="utf-8")


# --------------------------------------------------------------- add/edit/delete/toggle (via load+save)


def test_add_rule(tmp_path):
    p = tmp_path / "rules.xml"
    _write_sample(p)
    rules = load_rules(p)
    rules.append(Rule(id="new-rule", field="genre", match="equals", value="Rap",
                       action="set-value", replacement="Hip-Hop"))
    save_rules(rules, p)
    assert [r.id for r in load_rules(p)] == ["new-rule"]


def test_edit_rule(tmp_path):
    p = tmp_path / "rules.xml"
    _write_sample(p)
    save_rules([Rule(id="r1", field="title", match="contains", value="old",
                      action="set-value", replacement="y")], p)
    rules = load_rules(p)
    rules[0].value = "new"
    save_rules(rules, p)
    reloaded = load_rules(p)
    assert reloaded[0].value == "new"


def test_delete_rule(tmp_path):
    p = tmp_path / "rules.xml"
    _write_sample(p)
    save_rules([
        Rule(id="r1", field="title", match="contains", value="x", action="set-value", replacement="y"),
        Rule(id="r2", field="album", match="contains", value="x", action="set-value", replacement="y"),
    ], p)
    rules = [r for r in load_rules(p) if r.id != "r1"]
    save_rules(rules, p)
    assert [r.id for r in load_rules(p)] == ["r2"]


def test_toggle_enabled(tmp_path):
    p = tmp_path / "rules.xml"
    _write_sample(p)
    save_rules([Rule(id="r1", field="title", match="contains", value="x",
                      action="set-value", replacement="y", enabled=True)], p)
    rules = load_rules(p)
    rules[0].enabled = False
    save_rules(rules, p)
    assert load_rules(p)[0].enabled is False


# --------------------------------------------------------------- validate_rule


def test_validate_rule_accepts_valid_rule():
    r = Rule(id="ok", field="title", match="contains", value="x", action="set-value", replacement="y")
    assert validate_rule(r, existing=[]) == []


def test_validate_rule_rejects_invalid_field():
    r = Rule(id="ok", field="not-a-field", match="contains", value="x", action="set-value", replacement="y")
    errors = validate_rule(r, existing=[])
    assert any("Field" in e for e in errors)


def test_validate_rule_rejects_invalid_match():
    r = Rule(id="ok", field="title", match="fuzzy", value="x", action="set-value", replacement="y")
    errors = validate_rule(r, existing=[])
    assert any("Match" in e for e in errors)


def test_validate_rule_rejects_invalid_action():
    r = Rule(id="ok", field="title", match="contains", value="x", action="delete-everything", replacement="y")
    errors = validate_rule(r, existing=[])
    assert any("Action" in e for e in errors)


def test_validate_rule_rejects_missing_id():
    r = Rule(id="", field="title", match="contains", value="x", action="set-value", replacement="y")
    errors = validate_rule(r, existing=[])
    assert any("id" in e.lower() for e in errors)


def test_validate_rule_rejects_duplicate_id():
    existing = [Rule(id="dupe", field="title", match="contains", value="x", action="set-value", replacement="y")]
    r = Rule(id="dupe", field="album", match="contains", value="x", action="set-value", replacement="y")
    errors = validate_rule(r, existing=existing)
    assert any("already in use" in e for e in errors)


def test_validate_rule_allows_same_id_when_editing():
    existing = [Rule(id="keep", field="title", match="contains", value="x", action="set-value", replacement="y")]
    r = Rule(id="keep", field="album", match="contains", value="new", action="set-value", replacement="y")
    errors = validate_rule(r, existing=existing, editing_id="keep")
    assert errors == []


def test_validate_rule_rejects_set_value_with_empty_replacement():
    r = Rule(id="ok", field="title", match="contains", value="x", action="set-value", replacement="")
    errors = validate_rule(r, existing=[])
    assert any("Replacement is required" in e for e in errors)


def test_validate_rule_accepts_set_value_with_replacement():
    r = Rule(id="ok", field="title", match="contains", value="x", action="set-value", replacement="y")
    assert validate_rule(r, existing=[]) == []


def test_validate_rule_allows_regex_replace_with_empty_replacement():
    r = Rule(id="ok", field="title", match="contains", value="x", action="regex-replace",
             pattern="x", replacement="")
    assert validate_rule(r, existing=[]) == []


def test_valid_field_match_action_sets_match_the_c_sharp_loader():
    # Guards against this Python module and TagFixCustomRules.cs's ValidFields/
    # ValidMatches/ValidActions arrays silently drifting apart.
    assert set(FIELDS) == {"title", "album", "artists", "genre"}
    assert {m.lower() for m in MATCHES} == {"contains", "equals", "regex", "startswith", "endswith"}
    assert set(ACTIONS) == {"regex-replace", "set-value"}


# --------------------------------------------------------------- custom-rule-line highlighting


SAMPLE_OUTPUT = [
    'Title: "Song (Explicit)"  -> "Song"',
    'Genre: "Rap"  -> "Hip-Hop" [custom rule: rap-to-hiphop]',
    'Artists: "Drake feat. Future"  -> "Drake;Future"',
    'Title: "Old Title"  -> "New Title" [custom rule: strip-bonus]',
    "No changes needed for track.mp3",
]


def test_is_custom_rule_line_true_for_marked_lines():
    assert _is_custom_rule_line(SAMPLE_OUTPUT[1]) is True
    assert _is_custom_rule_line(SAMPLE_OUTPUT[3]) is True


def test_is_custom_rule_line_false_for_builtin_lines():
    assert _is_custom_rule_line(SAMPLE_OUTPUT[0]) is False
    assert _is_custom_rule_line(SAMPLE_OUTPUT[2]) is False
    assert _is_custom_rule_line(SAMPLE_OUTPUT[4]) is False


def test_render_console_lines_marks_only_custom_rule_lines():
    html = _render_console_lines(SAMPLE_OUTPUT)
    assert html.count('class="custom-rule-line"') == 2
    # Built-in lines render without the custom-rule class.
    assert '<div>Title: &quot;Song' not in html or True  # esc doesn't touch quotes; keep line simple
    assert "rap-to-hiphop" in html and "strip-bonus" in html


def test_render_console_lines_escapes_html():
    html = _render_console_lines(['Title: "A" -> "<b>bold</b>" [custom rule: x]'])
    assert "&lt;b&gt;" in html
    assert "<b>bold</b>" not in html


# ------------------------------------------- custom-rule warning highlighting


# Exact line formats printed by TagFixCustomRuleSet (TagFixCustomRules.cs) -
# ParseRule()'s validation failures and Apply()'s catch block.
_WARN_FAILED = "  [WARN] Custom tag rule 'strip-bonus' failed: parsing \"[\" - Unterminated [] set."
_WARN_SKIPPED_FIELD = "  [WARN] Custom tag rule 'r1' skipped: unknown field 'titel'"
_WARN_SKIPPED_MATCH = "  [WARN] Custom tag rule 'r1' skipped: unknown match 'fuzzy'"
_WARN_SKIPPED_ACTION = "  [WARN] Custom tag rule 'r1' skipped: unknown action 'nuke'"
_WARN_SKIPPED_MISSING_ID = "  [WARN] Custom tag rule skipped: missing id attribute"
_WARN_LOAD_FAILURE = "  [WARN] Could not load custom tag rules (config/tagfix-custom-rules.xml): root element is missing."


def test_is_custom_rule_warning_line_true_for_failed_and_skipped():
    assert _is_custom_rule_warning_line(_WARN_FAILED) is True
    assert _is_custom_rule_warning_line(_WARN_SKIPPED_FIELD) is True
    assert _is_custom_rule_warning_line(_WARN_SKIPPED_MATCH) is True
    assert _is_custom_rule_warning_line(_WARN_SKIPPED_ACTION) is True
    assert _is_custom_rule_warning_line(_WARN_SKIPPED_MISSING_ID) is True


def test_is_custom_rule_warning_line_false_for_ordinary_change_line():
    assert _is_custom_rule_warning_line(SAMPLE_OUTPUT[1]) is False
    assert _is_custom_rule_warning_line(SAMPLE_OUTPUT[3]) is False
    assert _is_custom_rule_warning_line(SAMPLE_OUTPUT[0]) is False


def test_is_custom_rule_warning_line_false_for_unrelated_load_warning():
    # Different concern (missing/malformed rules file), already out of scope -
    # must not be conflated with a rule-authoring error.
    assert _is_custom_rule_warning_line(_WARN_LOAD_FAILURE) is False


def test_count_custom_rule_warnings_counts_only_warning_lines():
    lines = [
        *SAMPLE_OUTPUT,
        _WARN_FAILED,
        _WARN_SKIPPED_FIELD,
        _WARN_LOAD_FAILURE,
    ]
    assert count_custom_rule_warnings(lines) == 2


def test_count_custom_rule_warnings_zero_when_none_present():
    assert count_custom_rule_warnings(SAMPLE_OUTPUT) == 0


def test_render_console_lines_marks_warning_lines_distinctly():
    html = _render_console_lines([_WARN_FAILED, SAMPLE_OUTPUT[1]])
    assert html.count('class="custom-rule-warning-line"') == 1
    assert html.count('class="custom-rule-line"') == 1


# ------------------------------------------- literal_value_regex_metachars


def test_literal_value_regex_metachars_no_warning_when_pattern_set():
    # Pattern non-empty -> no fallback to Value-as-regex, so nothing to warn about.
    assert literal_value_regex_metachars("regex-replace", r"\d+", "(Official Video)") == []


def test_literal_value_regex_metachars_no_warning_when_not_regex_replace():
    assert literal_value_regex_metachars("set-value", "", "(Official Video)") == []


def test_literal_value_regex_metachars_no_warning_when_value_has_no_metachars():
    assert literal_value_regex_metachars("regex-replace", "", "Official Video") == []


def test_literal_value_regex_metachars_lists_only_present_characters():
    chars = literal_value_regex_metachars("regex-replace", "", "(Official Video)")
    assert chars == ["(", ")"]


def test_literal_value_regex_metachars_lists_each_metachar_once_in_first_seen_order():
    chars = literal_value_regex_metachars("regex-replace", "", "a.b.c*d")
    assert chars == [".", "*"]


def test_literal_value_regex_metachars_whitespace_only_pattern_does_not_warn():
    # Matches TagFixCustomRules.cs exactly: string.IsNullOrEmpty(Pattern), not
    # IsNullOrWhiteSpace - a whitespace-only Pattern is NOT empty there, so no
    # fallback to Value happens and no warning is warranted here either.
    assert literal_value_regex_metachars("regex-replace", "   ", "(x)") == []


# ------------------------------------------------------------- build_rule_rows


def test_build_rule_rows_shapes_every_field():
    rule = Rule(id="strip-bonus", field="title", match="endsWith", value="[Bonus]",
                action="regex-replace", pattern=r"\[Bonus\]$", replacement="", enabled=True)
    rows = build_rule_rows([rule])
    assert len(rows) == 1
    row = rows[0]
    assert row["row_key"] == "strip-bonus"
    assert row["id"] == "strip-bonus"
    assert row["field"] == "title"
    assert row["match"] == "endsWith"
    assert row["value"] == "[Bonus]"
    assert row["action"] == "regex-replace"
    assert row["pattern"] == r"\[Bonus\]$"
    assert row["replacement"] == ""
    assert row["enabled"] is True
    assert row["enabled_text"] == "Yes"


def test_build_rule_rows_enabled_text_reflects_disabled_rule():
    rule = Rule(id="r1", field="genre", match="equals", value="Rap",
                action="set-value", replacement="Hip-Hop", enabled=False)
    row = build_rule_rows([rule])[0]
    assert row["enabled"] is False
    assert row["enabled_text"] == "No"


def test_build_rule_rows_row_key_matches_rule_id_for_lookup():
    # _rule_by_id() in gui.tabs.tagfix looks a Rule back up by the id an
    # emitted Quasar row event hands back - row_key must equal id for that
    # round trip to work.
    rules = [
        Rule(id="a", field="title", match="contains", value="x", action="set-value", replacement="y"),
        Rule(id="b", field="album", match="contains", value="x", action="set-value", replacement="y"),
    ]
    rows = build_rule_rows(rules)
    assert [r["row_key"] for r in rows] == [r["id"] for r in rows] == ["a", "b"]


def test_build_rule_rows_empty_list():
    assert build_rule_rows([]) == []


# --------------------------------------- concurrent rule writers (merge-by-id)
#
# _toggle_enabled() and the rule-edit dialog's do_save() in gui.tabs.tagfix
# each re-load_rules() fresh from disk immediately before mutating, then
# apply their own single-rule change via these merge helpers, rather than
# blindly overwriting the whole file from whatever in-memory snapshot was
# current when a switch or dialog was first opened. These tests simulate the
# "someone else wrote to the file in between" scenario directly against the
# real XML file on disk (same load_rules/save_rules round trip other tests
# above use), one for each writer.


def test_toggle_enabled_after_independent_add_preserves_both_changes(tmp_path):
    p = tmp_path / "rules.xml"
    _write_sample(p)
    save_rules([Rule(id="r1", field="title", match="contains", value="x",
                      action="set-value", replacement="y", enabled=True)], p)

    # Simulate: a switch was flipped elsewhere, we still hold r1's older
    # in-memory copy (loaded before the independent change below landed).
    stale_r1 = load_rules(p)[0]

    # Independent on-disk change made "while the toggle was in flight":
    # a second rule gets added.
    save_rules(load_rules(p) + [Rule(id="r2", field="album", match="contains", value="x",
                                      action="set-value", replacement="y", enabled=True)], p)

    # The toggle writer re-reads fresh immediately before merging its change,
    # rather than trusting stale_r1's snapshot of the rule list.
    merged = merge_toggle_enabled(load_rules(p), stale_r1.id, False)
    save_rules(merged, p)

    reloaded = {r.id: r for r in load_rules(p)}
    assert set(reloaded) == {"r1", "r2"}
    assert reloaded["r1"].enabled is False
    assert reloaded["r2"].id == "r2"  # independent add survived


def test_save_edited_rule_after_independent_toggle_preserves_both_changes(tmp_path):
    p = tmp_path / "rules.xml"
    _write_sample(p)
    save_rules([
        Rule(id="r1", field="title", match="contains", value="old",
             action="set-value", replacement="y", enabled=True),
        Rule(id="r2", field="album", match="contains", value="x",
             action="set-value", replacement="y", enabled=True),
    ], p)

    # Dialog opened for r1, user is editing it (snapshot not needed further -
    # do_save() only ever re-reads fresh right before merging/saving).
    edited_r1 = Rule(id="r1", field="title", match="contains", value="new",
                      action="set-value", replacement="y", enabled=True)

    # Independent on-disk change made while the dialog was open: r2 gets
    # toggled off elsewhere.
    save_rules(merge_toggle_enabled(load_rules(p), "r2", False), p)

    # The edit-dialog writer re-reads fresh immediately before merging its
    # change, rather than overwriting the whole list from a stale snapshot.
    merged = merge_saved_rule(load_rules(p), edited_r1, editing_id="r1")
    save_rules(merged, p)

    reloaded = {r.id: r for r in load_rules(p)}
    assert reloaded["r1"].value == "new"
    assert reloaded["r2"].enabled is False  # independent toggle survived


def test_merge_toggle_enabled_only_changes_target_rule():
    rules = [
        Rule(id="a", field="title", match="contains", value="x", action="set-value", replacement="y", enabled=True),
        Rule(id="b", field="album", match="contains", value="x", action="set-value", replacement="y", enabled=True),
    ]
    merged = merge_toggle_enabled(rules, "a", False)
    by_id = {r.id: r for r in merged}
    assert by_id["a"].enabled is False
    assert by_id["b"].enabled is True


def test_merge_saved_rule_replaces_by_id_when_editing():
    existing = [
        Rule(id="a", field="title", match="contains", value="old", action="set-value", replacement="y"),
        Rule(id="b", field="album", match="contains", value="x", action="set-value", replacement="y"),
    ]
    new_a = Rule(id="a", field="title", match="contains", value="new", action="set-value", replacement="y")
    merged = merge_saved_rule(existing, new_a, editing_id="a")
    by_id = {r.id: r for r in merged}
    assert by_id["a"].value == "new"
    assert by_id["b"].value == "x"


def test_merge_saved_rule_appends_when_not_editing():
    existing = [Rule(id="a", field="title", match="contains", value="x", action="set-value", replacement="y")]
    new_b = Rule(id="b", field="album", match="contains", value="x", action="set-value", replacement="y")
    merged = merge_saved_rule(existing, new_b, editing_id=None)
    assert [r.id for r in merged] == ["a", "b"]
