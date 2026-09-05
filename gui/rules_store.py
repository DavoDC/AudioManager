"""Load/save/validate custom TagFixer rules (config/tagfix-custom-rules.xml).

This is the Python-side counterpart to the C# loader,
project/AudioManager/Code/Doer/TagFixCustomRules.cs (read-only reference for
this module - never edited from here). Everything here must produce XML that
loader parses correctly and must apply the same validation it applies:
field/match/action values are case-insensitive there, `enabled` is optional
and defaults to true, and one malformed rule never invalidates the rest.

Round-tripping preserves the tracked file's documentation-comment header:
saving only replaces the content between <TagFixCustomRules> and
</TagFixCustomRules>, so the header above it (and anything after the closing
tag) survives untouched.
"""
from __future__ import annotations

import re
import xml.etree.ElementTree as ET
from dataclasses import dataclass

from gui import config

# Valid values the C# loader accepts (ValidFields/ValidMatches/ValidActions in
# TagFixCustomRules.cs), compared case-insensitively there. Stored/displayed
# here in the same casing the file's own doc comment uses.
FIELDS = ("title", "album", "artists", "genre")
MATCHES = ("contains", "equals", "regex", "startsWith", "endsWith")
ACTIONS = ("regex-replace", "set-value")

_MATCH_LOOKUP = {m.lower(): m for m in MATCHES}
_FIELD_LOOKUP = {f.lower(): f for f in FIELDS}

_RULE_BLOCK_RE = re.compile(r"(<TagFixCustomRules>)(.*)(</TagFixCustomRules>)", re.DOTALL)

_EMPTY_BODY = "\n\n  <!-- No active rules yet. Add <Rule .../> elements here. -->\n\n"

# Fallback template used only if the tracked file is missing entirely -
# mirrors config/tagfix-custom-rules.xml's real header so a from-scratch
# write still documents the schema for a human reading the file later.
_DEFAULT_TEMPLATE = (
    '<?xml version="1.0" encoding="utf-8"?>\n'
    "<!--\n"
    "  User-defined custom tag cleanup rules for TagFixer.\n"
    "  Rule attributes: id, field (title|album|artists|genre), match\n"
    "  (contains|equals|regex|startsWith|endsWith), value, action\n"
    "  (regex-replace|set-value), pattern, replacement, enabled.\n"
    "  See docs/Development/IDEAS.md 'TagFix configurable rules' for the full schema.\n"
    "-->\n"
    "<TagFixCustomRules>" + _EMPTY_BODY + "</TagFixCustomRules>\n"
)


@dataclass
class Rule:
    id: str
    field: str
    match: str
    value: str = ""
    action: str = "regex-replace"
    pattern: str = ""
    replacement: str = ""
    enabled: bool = True


def load_rules(path=None) -> list[Rule]:
    """Load rules from the XML file. Missing/malformed file -> empty list,
    same fail-soft behaviour as the C# loader. One invalid <Rule> is skipped;
    the rest still load."""
    path = path or config.TAGFIX_CUSTOM_RULES_XML
    if not path.exists():
        return []
    try:
        tree = ET.parse(path)
    except ET.ParseError:
        return []

    rules: list[Rule] = []
    for el in tree.getroot().findall(".//Rule"):
        rule = _parse_rule_element(el)
        if rule is not None:
            rules.append(rule)
    return rules


def _parse_rule_element(el) -> Rule | None:
    rid = (el.get("id") or "").strip()
    field_raw = (el.get("field") or "").strip()
    match_raw = (el.get("match") or "").strip()
    action_raw = (el.get("action") or "").strip()

    if not rid:
        return None
    field_norm = _FIELD_LOOKUP.get(field_raw.lower())
    if field_norm is None:
        return None
    match_norm = _MATCH_LOOKUP.get(match_raw.lower())
    if match_norm is None:
        return None
    if action_raw.lower() not in ACTIONS:
        return None

    enabled_attr = el.get("enabled")
    enabled = True
    if enabled_attr is not None and enabled_attr.strip() != "":
        enabled = enabled_attr.strip().lower() == "true"

    return Rule(
        id=rid,
        field=field_norm,
        match=match_norm,
        value=el.get("value") or "",
        action=action_raw.lower(),
        pattern=el.get("pattern") or "",
        replacement=el.get("replacement") or "",
        enabled=enabled,
    )


def save_rules(rules: list[Rule], path=None) -> None:
    """Write rules back to the XML file, preserving the existing header
    comment/declaration and anything after the closing tag."""
    path = path or config.TAGFIX_CUSTOM_RULES_XML
    if path.exists():
        raw = path.read_text(encoding="utf-8")
        if not _RULE_BLOCK_RE.search(raw):
            raw = _DEFAULT_TEMPLATE
    else:
        raw = _DEFAULT_TEMPLATE

    body = ("\n\n" + "\n".join(_rule_to_xml(r) for r in rules) + "\n\n") if rules else _EMPTY_BODY

    def _replace(m: re.Match) -> str:
        return m.group(1) + body + m.group(3)

    new_raw = _RULE_BLOCK_RE.sub(_replace, raw, count=1)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(new_raw, encoding="utf-8")


def _escape_attr(value: str) -> str:
    return (
        (value or "")
        .replace("&", "&amp;")
        .replace('"', "&quot;")
        .replace("<", "&lt;")
        .replace(">", "&gt;")
    )


def _rule_to_xml(rule: Rule) -> str:
    parts = [
        f'id="{_escape_attr(rule.id)}"',
        f'field="{rule.field}"',
        f'match="{rule.match}"',
        f'value="{_escape_attr(rule.value)}"',
        f'action="{rule.action}"',
    ]
    if rule.action == "regex-replace" and rule.pattern:
        parts.append(f'pattern="{_escape_attr(rule.pattern)}"')
    parts.append(f'replacement="{_escape_attr(rule.replacement)}"')
    parts.append(f'enabled="{"true" if rule.enabled else "false"}"')
    return "  <Rule " + " ".join(parts) + " />"


def validate_rule(rule: Rule, existing: list[Rule], editing_id: str | None = None) -> list[str]:
    """Client-side validation mirroring the C# loader's accept/reject rules,
    plus GUI-only uniqueness (the C# side simply loads whatever id shows up;
    this stops a user creating two rules with the same id in the first place).
    Returns a list of human-readable errors - empty means valid."""
    errors: list[str] = []

    if not rule.id or not rule.id.strip():
        errors.append("Rule id is required.")
    elif any(r.id == rule.id and r.id != editing_id for r in existing):
        errors.append(f"Rule id '{rule.id}' is already in use.")

    if rule.field.lower() not in _FIELD_LOOKUP:
        errors.append(f"Field must be one of: {', '.join(FIELDS)}.")

    if rule.match.lower() not in _MATCH_LOOKUP:
        errors.append(f"Match must be one of: {', '.join(MATCHES)}.")

    if rule.action not in ACTIONS:
        errors.append(f"Action must be one of: {', '.join(ACTIONS)}.")

    if not rule.value:
        errors.append("Value (the match operand) is required.")

    if rule.action == "set-value" and not (rule.replacement or "").strip():
        errors.append(
            "Replacement is required for a set-value rule (use regex-replace "
            "with an empty Replacement if you actually want to remove text)."
        )

    return errors
