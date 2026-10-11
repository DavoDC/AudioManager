"""Tab and nav-group definitions shared by main.py and the state contract."""
from __future__ import annotations

TABS = [
    ("acquire", "Acquire", None),
    ("integration", "Integration", None),
    ("tagfix", "Tag Fix", None),
    ("stats", "Statistics", None),
    ("library", "Library", None),
    ("mirror", "Mirror", None),
    ("services", "Services", None),
]

# Sidebar section headers, in render order. Grouping is purely visual.
NAV_GROUPS = [
    ("Library Intake", ["acquire", "integration", "tagfix"]),
    ("Library Insight", ["stats", "library", "mirror", "services"]),
]
