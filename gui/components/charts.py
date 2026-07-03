"""ECharts option builders with EXPLICIT theming on every chart.

Chart libraries do not inherit page CSS variables for axis/legend/tooltip
text (round 1's radar shipped white-on-white). Every builder here sets
label/legend/tooltip colors explicitly - use these builders for all charts,
never a raw option dict.
"""
from __future__ import annotations

from gui import theme

DIM = theme.TEXT_DIM
TEXT = theme.TEXT
FONT = "IBM Plex Sans, sans-serif"
MONO = "IBM Plex Mono, monospace"

TOOLTIP = {
    "backgroundColor": "#0c0e13",
    "borderColor": theme.PANEL_BORDER,
    "textStyle": {"color": TEXT, "fontFamily": FONT, "fontSize": 12},
}
AXIS_LABEL = {"color": DIM, "fontSize": 11, "fontFamily": FONT}
AXIS_LINE = {"lineStyle": {"color": theme.PANEL_BORDER}}
SPLIT_LINE = {"lineStyle": {"color": theme.PANEL_BORDER}}


def _base() -> dict:
    return {
        "backgroundColor": "transparent",
        "textStyle": {"fontFamily": FONT, "color": TEXT},
        "tooltip": {**TOOLTIP, "trigger": "item"},
        "animationDuration": 700,
        "animationEasing": "cubicOut",
    }


def donut(data: list[dict], *, pie: bool = False, center_label: str | None = None) -> dict:
    """data: [{label, count}] -> donut (or full pie) with bottom legend.
    center_label puts a headline figure in the donut's hole."""
    opt = _base()
    opt["legend"] = {
        "bottom": 0, "type": "scroll",
        "textStyle": {"color": DIM, "fontSize": 11, "fontFamily": FONT},
        "pageTextStyle": {"color": DIM},
        "pageIconColor": theme.ACCENT,
        "pageIconInactiveColor": theme.PANEL_BORDER,
    }
    opt["color"] = theme.CHART_PALETTE
    total = sum(d["count"] for d in data)
    opt["series"] = [{
        "type": "pie",
        "radius": ["0%", "70%"] if pie else ["46%", "70%"],
        "center": ["50%", "44%"],
        "data": [{"name": d["label"], "value": d["count"]} for d in data],
        # legend + tooltip carry the small slices; only label sizeable ones
        "label": {"color": DIM, "fontSize": 11, "fontFamily": FONT,
                  "formatter": "{b}", "show": True,
                  "minAngle": 0},
        "labelLine": {"lineStyle": {"color": theme.PANEL_BORDER}, "length": 12, "length2": 8},
        "minShowLabelAngle": 14,
        "itemStyle": {"borderColor": theme.PANEL, "borderWidth": 2, "borderRadius": 4},
        "emphasis": {"scaleSize": 6,
                     "itemStyle": {"shadowBlur": 18, "shadowColor": "rgba(91,140,255,.35)"}},
    }]
    if center_label and not pie:
        head, _, sub = center_label.partition("\n")
        opt["graphic"] = [{
            "type": "text", "left": "center", "top": "38%",
            "style": {"text": head, "fill": TEXT, "fontSize": 24,
                      "fontWeight": 700, "fontFamily": MONO, "textAlign": "center"},
        }, {
            "type": "text", "left": "center", "top": "48%",
            "style": {"text": sub, "fill": DIM, "fontSize": 11,
                      "fontFamily": FONT, "textAlign": "center"},
        }]
    return opt


def treemap(data: list[dict]) -> dict:
    opt = _base()
    opt["series"] = [{
        "type": "treemap",
        "roam": False,
        "nodeClick": False,
        "breadcrumb": {"show": False},
        "data": [
            {"name": d["label"], "value": d["count"],
             "itemStyle": {"color": theme.CHART_PALETTE[i % len(theme.CHART_PALETTE)]}}
            for i, d in enumerate(data)
        ],
        "label": {"color": "#0c0e13", "fontSize": 11, "fontFamily": FONT, "fontWeight": 600},
        "itemStyle": {"borderColor": theme.PANEL, "borderWidth": 2, "gapWidth": 2},
    }]
    return opt


def bar(data: list[dict], color: str = theme.ACCENT, *, horizontal: bool = False,
        name: str = "Tracks") -> dict:
    """data: [{label, count}]. Horizontal bars read top-down (first = top)."""
    cats = [d["label"] for d in data]
    vals = [d["count"] for d in data]
    if horizontal:
        cats, vals = cats[::-1], vals[::-1]
    cat_axis = {
        "type": "category", "data": cats,
        "axisLabel": AXIS_LABEL, "axisLine": AXIS_LINE, "axisTick": {"show": False},
    }
    val_axis = {
        "type": "value",
        "axisLabel": AXIS_LABEL, "axisLine": {"show": False},
        "splitLine": SPLIT_LINE,
    }
    opt = _base()
    opt["tooltip"] = {**TOOLTIP, "trigger": "axis",
                      "axisPointer": {"type": "shadow", "shadowStyle": {"color": "rgba(91,140,255,.08)"}}}
    opt["grid"] = {"left": 8, "right": 16, "top": 12, "bottom": 4, "containLabel": True}
    opt["xAxis"] = val_axis if horizontal else cat_axis
    opt["yAxis"] = cat_axis if horizontal else val_axis
    opt["series"] = [{
        "type": "bar", "name": name, "data": vals,
        "itemStyle": {"color": color, "borderRadius": [0, 3, 3, 0] if horizontal else [3, 3, 0, 0]},
        "barMaxWidth": 26,
    }]
    return opt


def radar(data: list[dict], color: str = theme.ACCENT5) -> dict:
    """Genre balance radar - same data as the genre distribution."""
    maxv = max((d["count"] for d in data), default=1)
    opt = _base()
    opt["radar"] = {
        "indicator": [{"name": d["label"], "max": maxv} for d in data] or [{"name": "-", "max": 1}],
        "axisName": {"color": DIM, "fontSize": 11, "fontFamily": FONT},
        "splitLine": {"lineStyle": {"color": theme.PANEL_BORDER}},
        "splitArea": {"areaStyle": {"color": ["rgba(255,255,255,.015)", "rgba(255,255,255,0)"]}},
        "axisLine": {"lineStyle": {"color": theme.PANEL_BORDER}},
        "radius": "72%",
    }
    opt["series"] = [{
        "type": "radar",
        "data": [{"value": [d["count"] for d in data], "name": "Track share"}],
        "itemStyle": {"color": color},
        "lineStyle": {"color": color, "width": 2},
        "areaStyle": {"color": color, "opacity": 0.18},
        "symbolSize": 4,
    }]
    return opt


def ring(percent: float, label: str, color: str = theme.ACCENT) -> dict:
    """Radial gauge for the two coverage percentages. Reusable widget."""
    return {
        "backgroundColor": "transparent",
        "series": [{
            "type": "gauge",
            "startAngle": 90, "endAngle": -270,
            "radius": "88%",
            "pointer": {"show": False},
            "progress": {"show": True, "overlap": False, "roundCap": True,
                         "clip": False, "itemStyle": {"color": color}},
            "axisLine": {"lineStyle": {"width": 14, "color": [[1, "#12141c"]]}},
            "splitLine": {"show": False}, "axisTick": {"show": False},
            "axisLabel": {"show": False},
            "data": [{
                "value": round(percent, 1), "name": label,
                "title": {"color": DIM, "fontSize": 12, "fontFamily": FONT, "offsetCenter": [0, "24%"]},
                "detail": {"valueAnimation": True, "offsetCenter": [0, "-8%"]},
            }],
            "detail": {"fontSize": 26, "color": TEXT, "fontFamily": MONO,
                       "fontWeight": 600, "formatter": "{value}%"},
        }],
    }
