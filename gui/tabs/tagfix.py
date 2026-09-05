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
            with ui.expansion("Dry-run output").classes("w-full") \
                    .style("margin-top:14px;border:1px solid var(--panel-border);border-radius:3px;"):
                ui.html(f'<div class="console" style="max-height:260px;">'
                        f'{_render_console_lines(T.lines[-300:])}</div>')

    T.refresh = content.refresh
    content()


# --------------------------------------------------------- custom rules CRUD


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

    head = ("<th>ID</th><th>Field</th><th>Match</th><th>Value</th><th>Action</th>"
            "<th>Enabled</th><th></th>")
    rows = []
    for r in rules:
        rows.append(
            "<tr>"
            f"<td>{_esc(r.id)}</td>"
            f"<td>{_esc(r.field)}</td>"
            f"<td>{_esc(r.match)}</td>"
            f"<td>{_esc(r.value)}</td>"
            f"<td>{_esc(r.action)}</td>"
            f'<td><span class="status-badge {"active" if r.enabled else "draft"}">'
            f'{"Yes" if r.enabled else "No"}</span></td>'
            "<td></td>"
            "</tr>"
        )
    ui.html('<table class="am-table"><thead><tr>' + head + "</tr></thead><tbody>"
            + "".join(rows) + "</tbody></table>").classes("w-full")

    # Action buttons per row, laid out over the table (NiceGUI can't put
    # interactive widgets inside raw ui.html rows, so they're rendered as a
    # second, aligned column stack rather than true inline <td> buttons).
    with ui.column().style("gap:4px;margin-top:-8px;"):
        for r in rules:
            with ui.row().style("gap:6px;align-items:center;"):
                ui.label(r.id).style("font-family:var(--font-mono);font-size:12px;color:var(--text-dim);min-width:140px;")
                ui.button("Edit", on_click=lambda _, r=r: _open_rule_dialog(r)).props("flat dense size=sm color=primary")
                ui.button("Delete", on_click=lambda _, r=r: _confirm_delete(r)).props("flat dense size=sm color=negative")
                ui.switch(value=r.enabled, on_change=lambda e, r=r: _toggle_enabled(r, e.value)) \
                    .props("dense color=primary").tooltip("Enable/disable this rule")


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
        value_input = ui.input("Value", value=r.value).props("dense dark outlined").classes("w-full")
        action_select = ui.select(list(ACTIONS), value=r.action, label="Action").props("dense dark outlined").classes("w-full")
        pattern_input = ui.input("Pattern (regex-replace only, optional)", value=r.pattern) \
            .props("dense dark outlined").classes("w-full")
        replacement_input = ui.input("Replacement", value=r.replacement).props("dense dark outlined").classes("w-full")
        enabled_switch = ui.switch("Enabled", value=r.enabled).props("dense color=primary")

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


def _is_custom_rule_line(line: str) -> bool:
    """True if this dry-run output line was produced by a custom rule
    (TagFixCustomRuleChange.Describe() suffixes it with "[custom rule: <id>]"),
    as opposed to one of the exe's built-in fixed transforms."""
    return _CUSTOM_RULE_MARKER in line


def _render_console_lines(lines: list[str]) -> str:
    """Render dry-run output as one <div> per line, tagging custom-rule lines
    with a distinct class so they read differently from built-in-fix lines."""
    out = []
    for line in lines:
        esc = _esc(line)
        cls = "custom-rule-line" if _is_custom_rule_line(line) else ""
        out.append(f'<div class="{cls}">{esc}</div>' if cls else f"<div>{esc}</div>")
    return "".join(out)


def _esc(text: str) -> str:
    return text.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")
