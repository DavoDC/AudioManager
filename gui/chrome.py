"""Page chrome shared by every tab: global error handler, error banner,
build footer, FIXTURE banner and the window.__debug surface.

Everything here is plain HTML/JS injected once per page by main.py.
"""
from __future__ import annotations

import html

from gui import buildinfo, demo, security

ERROR_JS = r"""
(function(){
  var TOKEN = __TOKEN__;
  function show(msg){
    try{
      var b=document.getElementById('error-banner');
      if(b){b.hidden=false;b.textContent='Something broke in the page: '+msg;}
    }catch(e){}
  }
  function report(kind,msg){
    console.error('[app-error] '+kind+': '+msg);
    show(msg);
    try{fetch('/__log',{method:'POST',headers:{'Content-Type':'application/json','X-AM-Token':TOKEN},
      body:JSON.stringify({kind:kind,message:String(msg)})});}catch(e){}
  }
  window.addEventListener('error',function(e){report('error',(e&&e.message)||'script error');});
  window.addEventListener('unhandledrejection',function(e){
    report('rejection',(e&&e.reason&&(e.reason.message||e.reason))||'unhandled rejection');});
  window.__debug = {
    version: 1,
    state: function(){return fetch('/state.json').then(function(r){return r.json();});},
    tabState: function(t){return fetch('/state/'+t+'.json').then(function(r){return r.json();});},
    setTab: function(k){var b=document.getElementById('nav-'+k);if(!b)return false;b.click();return true;},
    forceStatus: function(text,kind){
      var s=document.getElementById('am-status');
      if(!s)return false;
      s.hidden=!text;s.textContent=text||'';s.setAttribute('data-kind',kind||'info');return true;},
    help: function(){return 'window.__debug: state(), tabState(tab), setTab(key), forceStatus(text, kind), help()';}
  };
  var boot=null;
  setInterval(function(){
    fetch('/api/build',{cache:'no-store'}).then(function(r){return r.json();}).then(function(j){
      if(boot===null){boot=j.boot_id;}
      else if(j.boot_id!==boot){location.reload();}
    }).catch(function(){});
  },3000);
})();
"""

CSS = """
#error-banner{position:fixed;top:0;left:0;right:0;z-index:9999;background:#7a1f1f;color:#fff;
  padding:8px 14px;font-size:13px;}
#error-banner[hidden]{display:none;}
#am-status{position:fixed;bottom:34px;right:14px;z-index:9000;background:var(--panel,#222);
  border:1px solid var(--panel-border,#444);padding:6px 10px;border-radius:4px;font-size:12px;}
#am-status[hidden]{display:none;}
#am-status[data-kind=error]{border-color:#c0392b;}
#fixture-banner{background:#5a4a00;color:#fff;font-size:12px;padding:4px 14px;text-align:center;}
#build-footer{position:fixed;bottom:6px;right:14px;z-index:8000;font-size:11px;
  color:var(--text-dim,#888);font-family:'IBM Plex Mono',monospace;pointer-events:none;}
"""


def head_html() -> str:
    js = ERROR_JS.replace("__TOKEN__", '"' + security.TOKEN + '"')
    return f"<style>{CSS}</style><script>{js}</script>"


def body_html() -> str:
    """Banner, status line, FIXTURE strip and footer, in one string."""
    fixture = demo.fixture_name()
    line = html.escape(buildinfo.format_line(buildinfo.describe(), fixture))
    parts = [
        '<div id="error-banner" role="alert" data-testid="error-banner" hidden></div>',
        '<div id="am-status" role="status" data-testid="am-status" hidden></div>',
    ]
    if fixture:
        parts.append(f'<div id="fixture-banner" data-testid="fixture-banner">'
                     f'FIXTURE {html.escape(fixture)}: demo data, the exe is disabled</div>')
    parts.append(f'<div id="build-footer" data-testid="build-footer">{line}</div>')
    return "".join(parts)
