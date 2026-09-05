"""Tag Fix tab.

The FIXED_RULES cards below DESCRIBE the exe's built-in TagFixer transforms,
which are hardcoded and unaffected by anything in this tab (TCMP, genre
normalisation, parentheticals, featured-artist extraction). Below that is the
custom-rule CRUD UI: rule list/add/edit/delete/enable-disable, persisted to
config/tagfix-custom-rules.xml via gui.rules_store, which the exe's
TagFixCustomRuleSet applies as an additive second pass. "Run Fixed Rules
(dry run)" triggers the exe's existing `tagfix --dry-run` and shows its
output, highlighting the lines a custom rule produced (marked by the exe
with "[custom rule: <id>]") distinctly from built-in-fix lines.
"""
from __future__ import annotations

import asyncio

from nicegui import ui

from gui import config, rules_store
from gui.components.error_modal import show_error_modal
from gui.rules_store import ACTIONS, FIELDS, MATCHES, Rule
from gui.runner import runner

# The exe's actual hardcoded transforms, presented as read-only rule cards.
FIXED_RULES = [
    ("Compilation flag (TCMP)", "Sets the iTunes TCMP compilation frame where album context requires it",
     "Active", "NewMusic only", "hardcoded"),
    ("Genre normalisation", "Maps raw genre strings onto the library's canonical genre set",
     "Active", "NewMusic only", "hardcoded"),
    ("Strip parentheticals", "Removes '(Explicit)', '(Official Video)' and similar noise from titles",
     "Active", "NewMusic only", "hardcoded"),
    ("Extract featured artists", "Moves 'feat. X' out of the title into the Artists field",
     "Active", "NewMusic only", "hardcoded"),
    ("File renames", "Renames files to the library's 'Artist - Title.mp3' convention",
     "Active", "NewMusic only", "hardcoded"),
]

# Marker the exe suffixes a custom-rule change with (TagFixCustomRuleChange.Describe(),
# ChangeMarkerPrefix in TagFixCustomRules.cs) - used to render those lines distinctly.
_CUSTOM_RULE_MARKER = "[custom rule: "

# Substring shared by both custom-rule-authoring warning formats TagFixCustomRuleSet
# prints (TagFixCustomRules.cs, ParseRule() and Apply()'s catch block):
#   "  [WARN] Custom tag rule '<id>' failed: <message>"
#   "  [WARN] Custom tag rule '<id>' skipped: unknown field '<x>'" (also missing-id,
#   unknown match, unknown action)
# Capital "Custom tag rule" (singular) deliberately excludes the unrelated load-time
# warning "  [WARN] Could not load custom tag rules (...)" (lowercase "custom", plural
# "rules") - that one is a config-file problem, not a rule-authoring error.
_CUSTOM_RULE_WARNING_MARKER = "[WARN] Custom tag rule"

# Regex metacharacters (.NET Regex syntax, same set the C# loader's Regex.Replace
# would interpret) - used only to warn when an empty Pattern falls back to Value
# as-is (TagFixCustomRules.cs's Pattern = IsNullOrEmpty(Pattern) ? Value : Pattern,
# left untouched by this fix per the recorded OPUS decision, docs/Development/IDEAS.md).
_REGEX_METACHARS = ".^$*+?()[]{}|\\"


class TagFixState:
    def __init__(self):
        self.lines: list[str] = []
        self.refresh = lambda: None


T = TagFixState()


def build() -> None:
    with ui.element("header").classes("page"):
        ui.html("<h1>Tag Fix</h1>")
        ui.html('<div class="meta">Built-in cleanup for NewMusic, plus your own custom rules</div>')

    @ui.refreshable
    def content():
        with ui.row().style("gap:10px;margin-bottom:16px;flex-wrap:wrap;align-items:center;"):
            if runner.busy:
                ui.html('<div class="note" style="margin:0;"><span class="spin"></span>'
                        f"{runner.current_action} running&hellip;</div>")
                ui.button("Cancel", on_click=lambda: runner.cancel()).props("outline dense color=negative size=sm")
            else:
                ui.button("Run Fixed Rules (dry run)",
                          on_click=lambda: asyncio.create_task(run_tagfix())) \
                    .props("outline color=primary") \
                    .tooltip("Runs the exe's existing tagfix --dry-run on NewMusic - no files changed")

        for name, desc, status, scope, kind in FIXED_RULES:
            with ui.element("div").classes("rule-card"):
                ui.html(
                    '<div style="display:flex;justify-content:space-between;align-items:center;">'
                    f'<div class="rule-name">{name}</div>'
                    f'<span class="status-badge active">{status}</span></div>'
                    f'<div class="rule-desc">{desc}</div>'
                    '<div class="rule-meta">'
                    f"<div>Applies to<b>{scope}</b></div>"
                    f"<div>Type<b>{kind}</b></div>"
                    "<div>Editable<b>Not here (built-in)</b></div>"
                    "</div>"
                )

        _custom_rules_section()

        if T.lines:
            warning_count = count_custom_rule_warnings(T.lines)
            if warning_count:
                ui.html(
                    f'<div class="rule-warning-badge" style="margin-top:14px;">'
                    f'{warning_count} rule warning{"s" if warning_count != 1 else ""} - '
                    "open the dry-run output below to see them</div>"
                )
            with ui.expansion("Dry-run output").classes("w-full") \
                    .style(f"margin-top:{0 if warning_count else 14}px;"
                           "border:1px solid var(--panel-border);border-radius:3px;"):
                ui.html(f'<div class="console" style="max-height:260px;">'
                        f'{_render_console_lines(T.lines[-300:])}</div>')

    T.refresh = content.refresh
    content()


# --------------------------------------------------------- custom rules CRUD


_RULE_TABLE_COLUMNS = [
    # (Quasar column name, header label, row-dict field)
    ("id", "ID", "id"),
    ("field", "Field", "field"),
    ("match", "Match", "match"),
    ("value", "Value", "value"),
    ("action", "Action", "action"),
    ("pattern", "Pattern", "pattern"),
    ("replacement", "Replacement", "replacement"),
    ("enabled", "Enabled", "enabled_text"),
    ("actions", "", "id"),
]


def build_rule_rows(rules: list[Rule]) -> list[dict]:
    """Row dicts for the custom-rules ui.table - render layer only, one dict
    per Rule with every field the table's body slot binds to. `row_key` is
    the rule id (already required to be unique by validate_rule), used both
    as ui.table's row_key and to look the Rule back up from an emitted
    Quasar row-click event (those events hand back the row dict, not the
    dataclass)."""
    return [
        {
            "row_key": r.id,
            "id": r.id,
            "field": r.field,
            "match": r.match,
            "value": r.value,
            "action": r.action,
            "pattern": r.pattern,
            "replacement": r.replacement,
            "enabled": r.enabled,
            "enabled_text": "Yes" if r.enabled else "No",
        }
        for r in rules
    ]


def _rule_by_id(rule_id: str) -> Rule | None:
    for r in rules_store.load_rules():
        if r.id == rule_id:
            return r
    return None


def _custom_rules_section() -> None:
    ui.html('<div class="panel-title" style="margin-top:22px;">Custom Rules</div>')
    ui.html(
        '<div class="gap-note w-full" style="margin-bottom:12px;">'
        "Runs as an additive second pass, after the built-in fixes above. "
        "Persisted to <code>config/tagfix-custom-rules.xml</code>."
        "</div>"
    )

    with ui.row().style("margin-bottom:10px;"):
        ui.button("+ New Rule", on_click=lambda: _open_rule_dialog()).props("unelevated color=primary size=sm")

    rules = rules_store.load_rules()
    if not rules:
        ui.html('<div class="note" style="margin:0;">No custom rules yet.</div>')
        return

    def _handle_edit_rule(row: dict) -> None:
        rule = _rule_by_id(row["id"])
        if rule is not None:
            _open_rule_dialog(rule)

    def _handle_delete_rule(row: dict) -> None:
        rule = _rule_by_id(row["id"])
        if rule is not None:
            _confirm_delete(rule)

    def _handle_toggle_enabled(row: dict) -> None:
        rule = _rule_by_id(row["id"])
        if rule is not None:
            _toggle_enabled(rule, not rule.enabled)

    columns = [
        {"name": name, "label": label, "field": field,
         "align": "center" if name in ("enabled", "actions") else "left", "sortable": False}
        for name, label, field in _RULE_TABLE_COLUMNS
    ]
    table = ui.table(
        rows=build_rule_rows(rules), columns=columns, row_key="row_key",
    ).classes("am-table w-full")
    # Per-row Edit/Delete/Enable controls live inside the same q-tr as the
    # rule's own data (genuinely part of the row, not a second stack
    # positioned underneath it) - same Quasar header/body-slot idiom as
    # track_table() in gui/tabs/acquire.py.
    table.add_slot("header", r'''
        <q-tr :props="props">
            <q-th v-for="col in props.cols" :key="col.name" :props="props">
                {{ col.label }}
            </q-th>
        </q-tr>
    ''')
    table.add_slot("body", r'''
        <q-tr :props="props">
            <q-td key="id" :props="props">{{ props.row.id }}</q-td>
            <q-td key="field" :props="props">{{ props.row.field }}</q-td>
            <q-td key="match" :props="props">{{ props.row.match }}</q-td>
            <q-td key="value" :props="props">{{ props.row.value }}</q-td>
            <q-td key="action" :props="props">{{ props.row.action }}</q-td>
            <q-td key="pattern" :props="props">{{ props.row.pattern }}</q-td>
            <q-td key="replacement" :props="props">{{ props.row.replacement }}</q-td>
            <q-td key="enabled" :props="props" style="text-align:center;cursor:pointer;"
                  title="Enable/disable this rule"
                  @click="() => $parent.$emit('toggle_enabled', props.row)">
                <span :class="props.row.enabled ? 'status-badge active' : 'status-badge draft'">
                    {{ props.row.enabled_text }}
                </span>
            </q-td>
            <q-td key="actions" :props="props" style="text-align:right;white-space:nowrap;">
                <q-btn flat dense size="sm" color="primary" label="Edit"
                       @click="() => $parent.$emit('edit_rule', props.row)" />
                <q-btn flat dense size="sm" color="negative" label="Delete"
                       @click="() => $parent.$emit('delete_rule', props.row)" />
            </q-td>
        </q-tr>
    ''')
    table.on("edit_rule", lambda e: _handle_edit_rule(e.args))
    table.on("delete_rule", lambda e: _handle_delete_rule(e.args))
    table.on("toggle_enabled", lambda e: _handle_toggle_enabled(e.args))


def _toggle_enabled(rule: Rule, value: bool) -> None:
    rules = rules_store.load_rules()
    for r in rules:
        if r.id == rule.id:
            r.enabled = bool(value)
    rules_store.save_rules(rules)
    ui.notify(f"Rule '{rule.id}' {'enabled' if value else 'disabled'}", type="positive")
    T.refresh()


def _confirm_delete(rule: Rule) -> None:
    with ui.dialog() as dlg, ui.card().style(
            "background:var(--panel);color:var(--text);padding:20px;max-width:420px;gap:12px;"):
        ui.label(f"Delete rule '{rule.id}'?").style("font-weight:600;font-size:15px;")
        ui.label("This removes it from config/tagfix-custom-rules.xml. It can't be undone here.") \
            .classes("note").style("margin:0;")
        with ui.row().classes("w-full justify-end").style("gap:10px;"):
            ui.button("Cancel", on_click=dlg.close).props("flat color=grey")

            def do_delete():
                dlg.close()
                rules = [r for r in rules_store.load_rules() if r.id != rule.id]
                rules_store.save_rules(rules)
                ui.notify(f"Deleted rule '{rule.id}'", type="positive")
                T.refresh()

            ui.button("Delete", on_click=do_delete).props("unelevated color=negative")
    dlg.open()


def _open_rule_dialog(existing: Rule | None = None) -> None:
    editing_id = existing.id if existing else None
    r = existing or Rule(id="", field=FIELDS[0], match=MATCHES[0], action=ACTIONS[0])

    with ui.dialog() as dlg, ui.card().style(
            "background:var(--panel);color:var(--text);padding:20px;min-width:420px;gap:10px;"):
        ui.label("Edit rule" if existing else "New rule").style("font-weight:600;font-size:15px;")

        id_input = ui.input("Id", value=r.id).props("dense dark outlined").classes("w-full")
        if existing:
            id_input.props("readonly")
        field_select = ui.select(list(FIELDS), value=r.field, label="Field").props("dense dark outlined").classes("w-full")
        match_select = ui.select(list(MATCHES), value=r.match, label="Match").props("dense dark outlined").classes("w-full")
        value_input = ui.input("Value", value=r.value, on_change=lambda e: _update_regex_warning()) \
            .props('dense dark outlined debounce="200"').classes("w-full")
        action_select = ui.select(list(ACTIONS), value=r.action, label="Action",
                                   on_change=lambda e: _update_regex_warning()) \
            .props("dense dark outlined").classes("w-full")
        pattern_input = ui.input("Pattern (regex-replace only, optional)", value=r.pattern,
                                  on_change=lambda e: _update_regex_warning()) \
            .props('dense dark outlined debounce="200"').classes("w-full")
        replacement_input = ui.input("Replacement", value=r.replacement).props("dense dark outlined").classes("w-full")
        enabled_switch = ui.switch("Enabled", value=r.enabled).props("dense color=primary")

        regex_warning_box = ui.column().classes("w-full")

        def _update_regex_warning() -> None:
            chars = literal_value_regex_metachars(
                action_select.value, pattern_input.value or "", value_input.value or ""
            )
            regex_warning_box.clear()
            if not chars:
                return
            with regex_warning_box:
                ui.label(
                    "These characters in Value will be treated as regex syntax, not "
                    f"matched literally: {' '.join(chars)}. Leave Pattern blank only if "
                    "that's intended."
                ).style("color:var(--accent3, #d9a441);font-size:12px;")

        _update_regex_warning()

        error_box = ui.column().classes("w-full")

        with ui.row().classes("w-full justify-end").style("gap:10px;"):
            ui.button("Cancel", on_click=dlg.close).props("flat color=grey")

            def do_save():
                new_rule = Rule(
                    id=(id_input.value or "").strip(),
                    field=field_select.value,
                    match=match_select.value,
                    value=value_input.value or "",
                    action=action_select.value,
                    pattern=pattern_input.value or "",
                    replacement=replacement_input.value or "",
                    enabled=bool(enabled_switch.value),
                )
                existing_rules = rules_store.load_rules()
                errors = rules_store.validate_rule(new_rule, existing_rules, editing_id=editing_id)
                if errors:
                    error_box.clear()
                    with error_box:
                        for msg in errors:
                            ui.label(msg).style("color:var(--accent4);font-size:12px;")
                    return

                if editing_id:
                    existing_rules = [new_rule if x.id == editing_id else x for x in existing_rules]
                else:
                    existing_rules.append(new_rule)
                rules_store.save_rules(existing_rules)
                ui.notify(f"Saved rule '{new_rule.id}'", type="positive")
                dlg.close()
                T.refresh()

            ui.button("Save", on_click=do_save).props("unelevated color=primary")
    dlg.open()


async def run_tagfix() -> None:
    if runner.busy:
        ui.notify("Another operation is already running", type="warning")
        return
    T.lines = []
    T.refresh()
    result = await runner.run(
        ["tagfix", "--dry-run"],
        action="Tag fix (dry run)",
        on_line=T.lines.append,
        timeout=config.TIMEOUT_TAGFIX,
    )
    T.refresh()
    if not result.ok and not result.cancelled:
        show_error_modal("Tag fix (dry run)", result, retry=run_tagfix)
    elif result.ok:
        ui.notify("Dry run complete - output below", type="positive")


def literal_value_regex_metachars(action: str, pattern: str, value: str) -> list[str]:
    """Return the regex metacharacters present in `value`, in the order they
    first appear, when an empty Pattern would fall back to using Value as the
    regex verbatim (TagFixCustomRules.cs's Pattern-defaults-to-Value fallback,
    `string.IsNullOrEmpty(rule.Pattern) ? rule.Value : rule.Pattern` - matched
    here exactly, so a whitespace-only Pattern does NOT count as empty, same
    as the C# side). Empty list means no warning is warranted: the action
    isn't regex-replace, Pattern is already set (no fallback happens), or
    Value has nothing a regex engine would treat specially."""
    if action != "regex-replace" or pattern:
        return []
    seen: list[str] = []
    for ch in value or "":
        if ch in _REGEX_METACHARS and ch not in seen:
            seen.append(ch)
    return seen


def _is_custom_rule_line(line: str) -> bool:
    """True if this dry-run output line was produced by a custom rule
    (TagFixCustomRuleChange.Describe() suffixes it with "[custom rule: <id>]"),
    as opposed to one of the exe's built-in fixed transforms."""
    return _CUSTOM_RULE_MARKER in line


def _is_custom_rule_warning_line(line: str) -> bool:
    """True if this dry-run output line is one of TagFixCustomRuleSet's two
    rule-authoring warnings (a rule that failed to apply, or one skipped at
    load time for a bad id/field/match/action) - see TagFixCustomRules.cs,
    ParseRule() and Apply()'s catch block for the exact text. These render as
    ordinary grey console text otherwise, so a bad rule (e.g. an uncompilable
    regex) looks identical to a rule that simply matched nothing. Deliberately
    excludes the unrelated "Could not load custom tag rules (...)" file-load
    warning, which is a config-file problem rather than a rule-authoring one."""
    return _CUSTOM_RULE_WARNING_MARKER in line


def count_custom_rule_warnings(lines: list[str]) -> int:
    """Count of rule-authoring warning lines in dry-run output, shown outside
    the collapsed expansion so a bad rule is visible without opening it."""
    return sum(1 for line in lines if _is_custom_rule_warning_line(line))


def _render_console_lines(lines: list[str]) -> str:
    """Render dry-run output as one <div> per line, tagging custom-rule lines
    with a distinct class so they read differently from built-in-fix lines,
    and rule-authoring warning lines with a second distinct class."""
    out = []
    for line in lines:
        esc = _esc(line)
        if _is_custom_rule_warning_line(line):
            cls = "custom-rule-warning-line"
        elif _is_custom_rule_line(line):
            cls = "custom-rule-line"
        else:
            cls = ""
        out.append(f'<div class="{cls}">{esc}</div>' if cls else f"<div>{esc}</div>")
    return "".join(out)


def _esc(text: str) -> str:
    return text.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")
