"""Visual language for the AudioManager GUI - ported from mockup.html
(round 3, human-reviewed). Dark theme, IBM Plex pairing, sharp panels,
pill controls. The mockup's CSS classes are reused verbatim so panels,
tiles, chips, cards etc. can be composed with .classes('panel') etc.
"""

# Palette (single source of truth - charts import these)
BG = "#14161c"
PANEL = "#1c1f28"
PANEL_BORDER = "#2a2e3a"
TEXT = "#e6e8ee"
TEXT_DIM = "#9aa0ac"
ACCENT = "#5b8cff"
ACCENT2 = "#7fd1ae"
ACCENT3 = "#f2b84b"
ACCENT4 = "#e26d6d"
ACCENT5 = "#b98af0"
CHART_PALETTE = [ACCENT, ACCENT2, ACCENT3, ACCENT4, ACCENT5, "#5c6270",
                 "#5bb8ff", "#e2905b", "#8a7ff0", "#d1c37f", "#e26db8", "#7a9f6a"]

# ---------------------------------------------------------- mood reactivity
# The dominant genre in analysis-stats.json tints the whole UI: accent color,
# spotlight glow, brand gradient. Applied once at startup via apply_mood()
# (before any tab/chart module is imported - chart default args bind theme
# colors at import time).
MOOD_NAME = "Neutral"
GENRE_MOODS = {
    # genre keyword -> (accent, secondary accent for the brand gradient)
    "hip hop": ("#6d7bff", "#b98af0"),   # electric, punchy
    "rap": ("#6d7bff", "#b98af0"),
    "rock": ("#e2585b", "#f2b84b"),      # hot, driven
    "metal": ("#e2585b", "#9aa0ac"),
    "pop": ("#e26db8", "#8a7ff0"),       # bright, playful
    "electronic": ("#5bd0ff", "#8a7ff0"),
    "dance": ("#5bd0ff", "#e26db8"),
    "jazz": ("#e2a05b", "#d1c37f"),      # warm, smooth
    "soul": ("#e2a05b", "#e26db8"),
    "r&b": ("#e2a05b", "#e26db8"),
    "classical": ("#d1c37f", "#9aa0ac"),
    "musivation": ("#7fd1ae", "#5b8cff"),
    "motivation": ("#7fd1ae", "#f2b84b"),
}


def _hex_to_rgb(h: str) -> str:
    h = h.lstrip("#")
    return f"{int(h[0:2], 16)},{int(h[2:4], 16)},{int(h[4:6], 16)}"


def apply_mood(dominant_genre: str | None) -> None:
    """Retint the theme for the library's dominant genre. No-op for unknown
    genres. Must run before tab/chart modules are imported."""
    global ACCENT, ACCENT5, CHART_PALETTE, HEAD_HTML, MOOD_NAME
    if not dominant_genre:
        return
    key = dominant_genre.strip().lower()
    mood = GENRE_MOODS.get(key) or next(
        (v for k, v in GENRE_MOODS.items() if k in key), None)
    if mood is None:
        return
    new_accent, new_secondary = mood
    old_rgb = _hex_to_rgb(ACCENT)
    HEAD_HTML = (HEAD_HTML
                 .replace(ACCENT, new_accent)
                 .replace(ACCENT5, new_secondary)
                 .replace(old_rgb, _hex_to_rgb(new_accent)))
    CHART_PALETTE = [new_accent if c == ACCENT else new_secondary if c == ACCENT5 else c
                     for c in CHART_PALETTE]
    ACCENT = new_accent
    ACCENT5 = new_secondary
    MOOD_NAME = dominant_genre

HEAD_HTML = """
<link rel="preconnect" href="https://fonts.googleapis.com">
<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
<link href="https://fonts.googleapis.com/css2?family=IBM+Plex+Sans:wght@400;500;600;700&family=IBM+Plex+Mono:wght@400;500;600;700&display=swap" rel="stylesheet">
<style>
  :root{
    --bg:#14161c; --panel:#1c1f28; --panel-border:#2a2e3a;
    --text:#e6e8ee; --text-dim:#9aa0ac; --accent:#5b8cff;
    --accent2:#7fd1ae; --accent3:#f2b84b; --accent4:#e26d6d; --accent5:#b98af0;
    --font-body:'IBM Plex Sans',sans-serif;
    --font-mono:'IBM Plex Mono',monospace;
    --radius-panel:3px; --radius-control:4px; --radius-pill:999px;
  }
  body{font-family:var(--font-body);background:var(--bg)!important;color:var(--text);}
  .nicegui-content{padding:0;}
  /* left nav */
  .am-nav{width:210px;min-width:210px;background:#10121a;border-right:1px solid var(--panel-border);padding:20px 0;height:100vh;overflow-y:auto;position:sticky;top:0;}
  .am-brand{padding:0 20px 20px;font-weight:600;font-size:15px;color:var(--text);border-bottom:1px solid var(--panel-border);margin-bottom:10px;}
  .am-brand span{color:var(--accent);}
  .tab-link{display:block;width:100%;text-align:left;background:none;border:none;padding:10px 20px;color:var(--text-dim);font-size:14px;border-left:3px solid transparent;cursor:pointer;font-family:inherit;}
  .tab-link:hover{color:var(--text);}
  .tab-link.active{color:var(--text);background:#181b24;border-left-color:var(--accent);}
  .tab-link .badge{float:right;font-size:9px;color:var(--accent3);border:1px solid var(--accent3);border-radius:3px;padding:1px 4px;}
  .am-main{flex:1;padding:24px 28px;overflow-y:auto;height:100vh;min-width:0;}
  header.page{display:flex;justify-content:space-between;align-items:baseline;margin-bottom:20px;flex-wrap:wrap;gap:10px;width:100%;}
  header.page h1{font-size:20px;margin:0;font-weight:600;color:var(--text);}
  header.page .meta{color:var(--text-dim);font-size:12px;}
  .totals{display:grid;grid-template-columns:repeat(auto-fit,minmax(150px,1fr));gap:14px;margin-bottom:22px;width:100%;}
  .stat-tile{background:var(--panel);border:1px solid var(--panel-border);border-radius:var(--radius-panel);padding:14px 16px;}
  .stat-tile .label{font-size:11px;color:var(--text-dim);text-transform:uppercase;letter-spacing:.05em;}
  .stat-tile .value{font-family:var(--font-mono);font-size:22px;font-weight:600;margin-top:4px;color:var(--text);}
  .stat-tile .delta{display:block;font-size:11px;margin-top:3px;font-weight:500;color:var(--text-dim);}
  .delta-pos{color:var(--accent2)!important;}
  .delta-neg{color:var(--accent4)!important;}
  .am-grid2{display:grid;grid-template-columns:1.1fr 1fr;gap:16px;width:100%;margin-bottom:16px;}
  .am-grid2e{display:grid;grid-template-columns:1fr 1fr;gap:16px;width:100%;margin-bottom:16px;}
  .panel{background:var(--panel);border:1px solid var(--panel-border);border-radius:var(--radius-panel);padding:16px;}
  .panel-title{font-size:13px;font-weight:600;margin:0 0 4px;color:var(--text-dim);text-transform:uppercase;letter-spacing:.04em;display:flex;justify-content:space-between;align-items:center;flex-wrap:wrap;gap:6px;width:100%;}
  .note{color:var(--text-dim);font-size:11px;margin-top:14px;line-height:1.5;}
  .am-btn{background:var(--accent);color:#0c0e13;border:none;border-radius:var(--radius-pill);padding:9px 18px;font-size:13px;font-weight:600;cursor:pointer;font-family:inherit;}
  .am-btn:disabled{opacity:.45;cursor:not-allowed;}
  .am-btn.secondary{background:transparent;border:1px solid var(--panel-border);color:var(--text);}
  .am-btn.small{padding:5px 12px;font-size:11px;}
  .am-btn.danger{background:transparent;border:1px solid var(--accent4);color:var(--accent4);}
  .chip{background:#12141c;border:1px solid var(--panel-border);border-radius:14px;padding:5px 12px;font-size:12px;color:var(--text-dim);cursor:pointer;display:inline-block;}
  .chip.active{color:var(--text);border-color:var(--accent);}
  .freshness-bar{display:flex;align-items:center;justify-content:space-between;gap:12px;background:var(--panel);border:1px solid var(--panel-border);border-radius:var(--radius-panel);padding:10px 14px;margin-bottom:18px;flex-wrap:wrap;width:100%;}
  .fresh-info{font-size:12px;color:var(--text-dim);}
  .fresh-info b{color:var(--text);font-weight:600;}
  .fresh-dot{color:var(--accent2);}
  .fresh-dot.stale{color:var(--accent3);}
  .toggle-pair{display:flex;gap:4px;}
  .toggle-pair button{background:#12141c;border:1px solid var(--panel-border);color:var(--text-dim);font-size:10px;padding:3px 10px;border-radius:var(--radius-pill);cursor:pointer;font-family:inherit;}
  .toggle-pair button.active{color:var(--text);border-color:var(--accent);background:#181b2e;}
  .am-select{background:#12141c;color:var(--text);border:1px solid var(--panel-border);border-radius:4px;font-size:12px;padding:4px 8px;font-family:inherit;}
  table.am-table{width:100%;border-collapse:collapse;font-size:13px;color:var(--text);}
  .am-table th,.am-table td{text-align:left;padding:6px 6px;border-bottom:1px solid var(--panel-border);}
  .am-table th{color:var(--text-dim);font-weight:500;font-size:11px;text-transform:uppercase;}
  .am-table td.num{font-family:var(--font-mono);text-align:right;color:var(--text-dim);}
  .am-table.acquire-table{font-size:15px;}
  .am-table.acquire-table th,.am-table.acquire-table td{padding:10px 12px;}
  .am-table.acquire-table tr:nth-child(even){background:rgba(255,255,255,.02);}
  .am-table.acquire-table a{color:var(--accent);}
  .am-table.acquire-table input[type=checkbox]{width:18px;height:18px;cursor:pointer;}
  tr.batch-header td{padding-top:14px;font-weight:600;color:var(--accent);font-size:12px;}
  .console{background:#0c0e13;border:1px solid var(--panel-border);border-radius:var(--radius-panel);padding:12px;font-family:var(--font-mono);font-size:12px;color:var(--accent2);overflow:auto;white-space:pre-wrap;}
  .console .dim{color:var(--text-dim);}
  /* library */
  .toolbar-row{display:flex;justify-content:space-between;align-items:center;flex-wrap:wrap;gap:10px;margin-bottom:12px;width:100%;}
  .view-toggle button{background:#12141c;border:1px solid var(--panel-border);color:var(--text-dim);font-size:12px;padding:6px 14px;border-radius:var(--radius-pill);cursor:pointer;font-family:inherit;}
  .view-toggle button.active{color:var(--text);border-color:var(--accent);}
  .card-grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(150px,1fr));gap:14px;width:100%;}
  .track-card{background:var(--panel);border:1px solid var(--panel-border);border-radius:var(--radius-panel);overflow:hidden;}
  .cover-art{aspect-ratio:1;display:flex;align-items:center;justify-content:center;font-family:var(--font-mono);font-weight:700;color:rgba(255,255,255,.92);letter-spacing:.02em;text-shadow:0 1px 3px rgba(0,0,0,.4);}
  .cover-art img{width:100%;height:100%;object-fit:cover;display:block;}
  .cover-art.sm{width:32px;height:32px;border-radius:3px;font-size:11px;flex-shrink:0;}
  .cover-art.md{width:56px;height:56px;border-radius:4px;font-size:16px;flex-shrink:0;}
  .cover-art.cover-lg{font-size:30px;}
  .track-card .info{padding:8px 10px;}
  .track-card .t-title{font-size:12px;font-weight:600;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;color:var(--text);}
  .track-card .t-artist{font-size:11px;color:var(--text-dim);white-space:nowrap;overflow:hidden;text-overflow:ellipsis;}
  .track-card .t-meta{font-size:10px;color:var(--text-dim);margin-top:3px;}
  .status-bar{height:4px;}
  .lowres-badge{position:absolute;top:6px;right:6px;font-size:9px;background:rgba(0,0,0,.55);color:var(--accent3);padding:1px 6px;border-radius:var(--radius-pill);font-weight:600;}
  .pagination{display:flex;justify-content:space-between;align-items:center;margin-top:14px;flex-wrap:wrap;gap:10px;font-size:12px;color:var(--text-dim);width:100%;}
  .pages span{padding:4px 9px;border:1px solid var(--panel-border);border-radius:4px;cursor:pointer;margin-right:4px;display:inline-block;color:var(--text-dim);}
  .pages span.active{color:var(--text);border-color:var(--accent);}
  /* integration */
  .stepper{display:flex;gap:6px;margin-bottom:16px;flex-wrap:wrap;}
  .step{display:flex;align-items:center;gap:8px;font-size:12px;color:var(--text-dim);background:#12141c;border:1px solid var(--panel-border);border-radius:var(--radius-pill);padding:6px 14px;}
  .step .n{display:inline-flex;align-items:center;justify-content:center;width:18px;height:18px;border-radius:50%;background:#2a2e3a;color:var(--text-dim);font-size:11px;font-weight:600;}
  .step.active{color:var(--text);border-color:var(--accent);background:#181b2e;}
  .step.active .n{background:var(--accent);color:#0c0e13;}
  .step.done .n{background:var(--accent2);color:#0c0e13;}
  .review-card{display:grid;grid-template-columns:56px 1fr auto;gap:14px;align-items:center;background:var(--panel);border:1px solid var(--panel-border);border-radius:var(--radius-panel);padding:12px 14px;width:100%;}
  .review-card.declined{opacity:.55;}
  .rc-title{font-size:14px;font-weight:600;color:var(--text);}
  .rc-artist{font-size:12px;color:var(--text-dim);margin-bottom:6px;}
  .rc-route{font-size:11px;font-family:var(--font-mono);color:var(--accent2);word-break:break-all;}
  .rc-route .arrow{color:var(--text-dim);}
  .rc-reason{font-size:11px;color:var(--text-dim);margin-top:3px;}
  .rc-tags{display:flex;gap:6px;margin-top:7px;flex-wrap:wrap;}
  .tag-change{font-size:10px;font-family:var(--font-mono);padding:2px 7px;border-radius:var(--radius-pill);background:#12141c;border:1px solid var(--panel-border);color:var(--text-dim);}
  .rc-badges{display:flex;gap:6px;margin-top:6px;flex-wrap:wrap;}
  .rc-badge{font-size:9px;text-transform:uppercase;letter-spacing:.04em;padding:2px 7px;border-radius:var(--radius-pill);font-weight:600;}
  .rc-badge.newfolder{color:var(--accent);background:rgba(91,140,255,.13);}
  .rc-badge.dupe{color:var(--accent3);background:rgba(242,184,75,.13);}
  .rc-badge.clean{color:var(--accent2);background:rgba(127,209,174,.12);}
  .rc-badge.err{color:var(--accent4);background:rgba(226,109,109,.13);}
  .rc-decision{display:flex;flex-direction:column;gap:6px;min-width:104px;}
  .rc-decision button{font-size:11px;padding:6px 10px;border-radius:var(--radius-control);cursor:pointer;font-family:inherit;border:1px solid var(--panel-border);background:#12141c;color:var(--text-dim);}
  .rc-decision button.accept.on{background:rgba(127,209,174,.15);border-color:var(--accent2);color:var(--accent2);}
  .rc-decision button.decline.on{background:rgba(226,109,109,.15);border-color:var(--accent4);color:var(--accent4);}
  .progress-track{height:8px;background:#12141c;border:1px solid var(--panel-border);border-radius:var(--radius-pill);overflow:hidden;width:100%;}
  .progress-track .fill{height:100%;background:linear-gradient(90deg,var(--accent),var(--accent2));transition:width .3s;}
  .progress-track .fill.fail{background:var(--accent4);}
  .progress-row{display:flex;align-items:center;gap:10px;padding:5px 8px;border-radius:var(--radius-control);font-size:12px;width:100%;}
  .progress-row .st{font-size:10px;text-transform:uppercase;letter-spacing:.04em;width:84px;flex-shrink:0;}
  .st-done{color:var(--accent2);}
  .st-moving{color:var(--accent3);}
  .st-queued{color:var(--text-dim);}
  .st-failed{color:var(--accent4);}
  .st-notrun{color:var(--accent3);}
  .simulate-banner{background:rgba(230,180,60,.12);border:1px solid var(--accent3);color:var(--accent3);
    font-size:11px;font-weight:600;text-transform:uppercase;letter-spacing:.04em;
    padding:7px 12px;border-radius:var(--radius-control);margin-bottom:12px;width:100%;}
  .libchecker-strip{font-size:12px;font-weight:600;padding:8px 12px;border-radius:var(--radius-control);
    margin-bottom:12px;width:100%;}
  .libchecker-strip.clean{background:rgba(127,209,174,.12);border:1px solid var(--accent2);color:var(--accent2);}
  .libchecker-strip.dirty{background:rgba(226,109,109,.13);border:1px solid var(--accent4);color:var(--accent4);}
  .libchecker-strip.skip{background:rgba(242,184,75,.12);border:1px solid var(--accent3);color:var(--accent3);}
  .pr-name{color:var(--text);word-break:break-all;}
  /* rule cards / services */
  .rule-card{background:var(--panel);border:1px solid var(--panel-border);border-radius:var(--radius-panel);padding:14px 16px;margin-bottom:12px;width:100%;}
  .rule-name{font-size:14px;font-weight:600;color:var(--text);}
  .rule-desc{color:var(--text-dim);font-size:12px;margin-top:4px;}
  .rule-meta{display:flex;gap:22px;margin-top:12px;font-size:11px;color:var(--text-dim);text-transform:uppercase;letter-spacing:.03em;}
  .rule-meta b{display:block;color:var(--text);font-size:13px;text-transform:none;margin-top:2px;font-weight:600;}
  .status-badge{font-size:10px;padding:2px 8px;border-radius:10px;font-weight:600;text-transform:uppercase;}
  .status-badge.active{color:var(--accent2);background:rgba(127,209,174,.12);}
  .status-badge.draft{color:var(--accent3);background:rgba(242,184,75,.12);}
  .stretch-badge{font-size:9px;color:var(--accent3);border:1px solid var(--accent3);border-radius:3px;padding:1px 5px;margin-left:8px;}
  .gap-note{border-left:3px solid var(--accent3);background:rgba(242,184,75,.06);padding:8px 12px;font-size:12px;color:var(--text-dim);border-radius:0 var(--radius-panel) var(--radius-panel) 0;}
  .gap-note b{color:var(--accent3);}
  /* error modal */
  .err-modal{background:var(--panel);border:1px solid var(--panel-border);border-radius:var(--radius-panel);color:var(--text);min-width:560px;max-width:820px;}
  .err-title{font-size:15px;font-weight:600;color:var(--accent4);}
  .err-meaning{font-size:13px;color:var(--text);background:rgba(226,109,109,.08);border:1px solid rgba(226,109,109,.25);border-radius:var(--radius-control);padding:10px 12px;width:100%;}
  .err-details{max-height:260px;overflow:auto;width:100%;}
  q-dialog .q-card{background:var(--panel);}
  .spin{display:inline-block;width:12px;height:12px;border:2px solid var(--text-dim);border-top-color:var(--accent);border-radius:50%;animation:amspin .8s linear infinite;vertical-align:-2px;margin-right:6px;}
  @keyframes amspin{to{transform:rotate(360deg);}}

  /* ================= Fluid motion layer ================= */
  body{background:
    radial-gradient(1100px 700px at 88% -12%, rgba(91,140,255,.075), transparent 60%),
    radial-gradient(900px 650px at -8% 112%, rgba(185,138,240,.055), transparent 60%),
    var(--bg)!important;
    background-attachment:fixed;}
  .am-nav{background:linear-gradient(180deg,#10121a 0%,#0e1017 100%);}
  /* living surfaces: lift + border warm + pointer spotlight */
  .panel,.stat-tile,.rule-card,.review-card,.track-card,.service-card,.freshness-bar{
    position:relative;
    background:linear-gradient(180deg,rgba(255,255,255,.02),rgba(255,255,255,0) 45%),var(--panel);
    transition:transform .25s cubic-bezier(.2,.7,.3,1),border-color .25s ease,box-shadow .25s ease;
  }
  .panel:hover,.stat-tile:hover,.rule-card:hover,.review-card:hover,.service-card:hover{
    transform:translateY(-2px);
    border-color:#3a4154;
    box-shadow:0 12px 32px -14px rgba(0,0,0,.65),0 0 0 1px rgba(91,140,255,.06);
  }
  .track-card:hover{transform:translateY(-3px);border-color:#3a4154;
    box-shadow:0 14px 34px -14px rgba(0,0,0,.7);}
  .panel::after,.stat-tile::after,.review-card::after,.track-card::after,.rule-card::after{
    content:"";position:absolute;inset:0;border-radius:inherit;pointer-events:none;opacity:0;
    transition:opacity .35s ease;
    background:radial-gradient(460px circle at var(--mx,50%) var(--my,50%),rgba(91,140,255,.09),transparent 45%);
  }
  .panel:hover::after,.stat-tile:hover::after,.review-card:hover::after,
  .track-card:hover::after,.rule-card:hover::after{opacity:1;}
  .stat-tile .value{transition:color .25s ease;}
  .stat-tile:hover .value{color:#cfe0ff;}
  /* nav life */
  .tab-link{transition:color .18s ease,background .18s ease,padding-left .18s ease,border-color .18s ease;}
  .tab-link:hover{padding-left:26px;background:#151823;}
  .tab-link.active{box-shadow:inset 12px 0 22px -18px rgba(91,140,255,.55);}
  .am-brand span{background:linear-gradient(90deg,var(--accent),var(--accent5));
    -webkit-background-clip:text;background-clip:text;color:transparent;}
  /* controls */
  .am-btn,.chip,.toggle-pair button,.view-toggle button,.rc-decision button,.pages span,.q-btn{
    transition:transform .15s ease,border-color .2s ease,color .2s ease,background .2s ease,box-shadow .2s ease;}
  .chip:hover,.toggle-pair button:hover,.view-toggle button:hover,.pages span:hover{
    border-color:var(--accent);color:var(--text);transform:translateY(-1px);}
  .chip.active{box-shadow:0 0 12px -4px rgba(91,140,255,.5);}
  /* album art breath */
  .cover-art{overflow:hidden;}
  .cover-art img{transition:transform .4s cubic-bezier(.2,.7,.3,1);}
  .track-card:hover .cover-art img{transform:scale(1.07);}
  .review-card:hover .cover-art img{transform:scale(1.08);}
  /* pulse + progress shine */
  .fresh-dot{display:inline-block;animation:ampulse 2.4s ease-in-out infinite;}
  @keyframes ampulse{0%,100%{opacity:1;}50%{opacity:.35;}}
  .progress-track .fill{background:linear-gradient(90deg,var(--accent),var(--accent2),var(--accent));
    background-size:200% 100%;animation:amflow 2.2s linear infinite;}
  @keyframes amflow{to{background-position:-200% 0;}}
  .progress-track .fill.fail{background:var(--accent4);animation:none;}
  .step{transition:border-color .25s ease,background .25s ease,color .25s ease;}
  .step.active{box-shadow:0 0 16px -6px rgba(91,140,255,.55);}
  /* tab entrance */
  .tab-enter{animation:tabin .4s cubic-bezier(.2,.7,.3,1);}
  @keyframes tabin{from{opacity:0;transform:translateY(12px);}to{opacity:1;transform:none;}}
  /* stagger stat tiles on entrance */
  .tab-enter .stat-tile{animation:tabin .5s cubic-bezier(.2,.7,.3,1) backwards;}
  .tab-enter .stat-tile:nth-child(1){animation-delay:.02s;}
  .tab-enter .stat-tile:nth-child(2){animation-delay:.06s;}
  .tab-enter .stat-tile:nth-child(3){animation-delay:.10s;}
  .tab-enter .stat-tile:nth-child(4){animation-delay:.14s;}
  .tab-enter .stat-tile:nth-child(5){animation-delay:.18s;}
  .tab-enter .stat-tile:nth-child(6){animation-delay:.22s;}
  .tab-enter .stat-tile:nth-child(7){animation-delay:.26s;}
  .tab-enter .stat-tile:nth-child(8){animation-delay:.30s;}
  /* slim dark scrollbars */
  ::-webkit-scrollbar{width:10px;height:10px;}
  ::-webkit-scrollbar-track{background:transparent;}
  ::-webkit-scrollbar-thumb{background:#262b38;border-radius:999px;border:2px solid var(--bg);}
  ::-webkit-scrollbar-thumb:hover{background:#3a4154;}
  @media (prefers-reduced-motion: reduce){
    *,*::before,*::after{animation-duration:.001s!important;transition-duration:.001s!important;}
  }
</style>
<script>
  // Pointer-tracking spotlight: one delegated listener feeds --mx/--my to the
  // hovered surface; CSS paints the radial highlight. GPU-cheap, no reflow.
  document.addEventListener('mousemove', (e) => {
    const el = e.target.closest('.panel,.stat-tile,.review-card,.track-card,.rule-card');
    if (!el) return;
    const r = el.getBoundingClientRect();
    el.style.setProperty('--mx', (e.clientX - r.left) + 'px');
    el.style.setProperty('--my', (e.clientY - r.top) + 'px');
  }, {passive: true});
</script>
"""
