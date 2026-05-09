#!/bin/bash
# PreToolUse guard - blocks dangerous operations, em dashes, and out-of-scope writes
# Uses ONE Python call for all checks (fast startup, encoding-safe)
# Exit 0 = allow, Exit 2 = block with message

PY=$(command -v python3 || command -v python) || exit 0

$PY -c "
import sys,json,re,os
sys.stdout.reconfigure(encoding='utf-8')
sys.stderr.reconfigure(encoding='utf-8')
d=json.loads(sys.stdin.buffer.read())
tn=d.get('tool_name','')
ti=d.get('tool_input',{})

def block(msg):
    print(msg, file=sys.stderr)
    sys.exit(2)

# Allowed write roots (forward-slash normalised, lowercase)
# Documents intentionally excluded - write there manually only
ALLOWED_WRITE_ROOTS = [
    'c:/users/david/githubrepos',
    '/c/users/david/githubrepos',
    'c:/users/david/.claude',
    '/c/users/david/.claude',
    '/tmp',
]

def path_allowed(p):
    p = p.replace(chr(92), '/').lower().rstrip('/')
    # Detect path traversal attempts
    if '..' in p or p.count('/') > 100:  # More than 100 path segments suggests traversal
        return False
    return any(p.startswith(r) for r in ALLOWED_WRITE_ROOTS)

if tn=='CronCreate':
    prompt=ti.get('prompt','')
    # Whitelist safe prompt patterns - only known-safe autonomous operations
    safe_patterns=[
        '<<autonomous-loop',  # autonomous-loop sentinel
        'check-budget',        # budget check
        'ScheduleWakeup',      # reschedule operation
        'push origin',         # safe git operation
        '/loop',               # loop skill
    ]
    # Allow if matches safe pattern or is short/benign
    is_safe = any(p in prompt for p in safe_patterns)
    if not is_safe and len(prompt) > 50:
        block('BLOCKED: CronCreate prompt must use whitelisted safe patterns (<<autonomous-loop, check-budget, ScheduleWakeup, /loop). Arbitrary prompts cannot be validated outside session context.')

elif tn=='Bash':
    cmd=ti.get('command','')
    # Git safety - Claude never pushes, only user pushes from host
    # Check only the executable command part, before any -m or message flag
    cmd_part = cmd.split('-m')[0] if '-m' in cmd else cmd.split('<<')[0] if '<<' in cmd else cmd
    # Match 'git push' even with flags like git -c, git -o, git --git-dir, etc.
    # Pattern: git ... push (as a subcommand, not part of other words like "push.default")
    if re.search(r'\bgit\b.*\spush(\s|$)',cmd_part):
        block('BLOCKED: Claude cannot push. User pushes from host after reviewing commits locally.')
    if re.search(r'git\s+.*--no-verify',cmd):
        block('BLOCKED: --no-verify not allowed.')
    if re.search(r'git\s+push\s+.*--(force|force-with-lease)',cmd):
        block('BLOCKED: Force push not allowed.')
    if re.search(r'git\s+reset\s+--hard',cmd):
        block('BLOCKED: git reset --hard is destructive.')
    # File destruction
    if re.search(r'rm\s+-rf\s+(/c/Users|/home|~|C:\\\\|/workspace)',cmd,re.I):
        block('BLOCKED: rm -rf on home/user dirs not allowed.')
    if re.search(r'\brmdir\s+/s\b|\brd\s+/s\b',cmd,re.I):
        block('BLOCKED: rmdir /s not allowed.')
    if 'Remove-Item' in cmd and '-Recurse' in cmd and ('/Users' in cmd or chr(92)+'Users' in cmd):
        block('BLOCKED: Remove-Item -Recurse on user dirs not allowed.')
    # Windows system damage
    if re.search(r'\breg\s+(add|delete|import)\b',cmd,re.I):
        block('BLOCKED: Registry modification not allowed in yolo mode.')
    if re.search(r'\bsc\s+(stop|delete|config|create)\b',cmd,re.I):
        block('BLOCKED: Service control not allowed in yolo mode.')
    if re.search(r'\bnet\s+(user|localgroup|accounts)\b',cmd,re.I):
        block('BLOCKED: User/group management not allowed in yolo mode.')
    if re.search(r'\bschtasks\s+/create\b',cmd,re.I):
        block('BLOCKED: Creating scheduled tasks not allowed in yolo mode.')
    # Network pipe-to-shell (download and execute)
    if re.search(r'(curl|wget|iwr|Invoke-WebRequest)\s+.+\|\s*(bash|sh|python|python3|powershell|pwsh)',cmd,re.I):
        block('BLOCKED: Pipe-to-shell from network download not allowed.')

elif tn in ('Write','Edit','NotebookEdit'):
    fp=ti.get('file_path','') or ti.get('notebook_path','')
    c=ti.get('content','')+ti.get('new_string','')
    bn=os.path.basename(fp)
    # Settings.json protection - prevent disabling safety hooks (workspace only)
    # Allow code repos (StreamPilot, RivalsVidMaker, etc) to have their own settings.json
    if bn=='settings.json' and ('Claude_Workspace' in fp or 'Claude_Workspace' in os.path.normpath(fp)):
        if ('.claude' in fp or '.claude' in os.path.normpath(fp)):
            block('BLOCKED: settings.json is protected. Use /update-config skill instead, or edit manually with careful review.')
    # Path scope enforcement - must be first check
    if fp and not path_allowed(fp):
        block('BLOCKED: Write/Edit outside allowed scope: '+fp+chr(10)+'Allowed roots: GitHubRepos and .claude only.')
    if chr(8212) in c or chr(8211) in c:
        block('BLOCKED: Em/en dashes found. Use regular dashes (-) instead.')
    # Sensitive file protection - block writes to files that look like secrets
    if re.search(r'\.(env|pem|key|crt|credentials)$',fp,re.I):
        block('BLOCKED: Writing to sensitive file: '+fp+chr(10)+'To update secrets, edit via user config or password manager outside Claude.')
    # Config protection - prevent weakening linter/formatter configs
    protected_configs = {'.eslintrc','.eslintrc.js','.eslintrc.json','eslint.config.js','eslint.config.mjs',
        '.prettierrc','.prettierrc.js','.prettierrc.json','prettier.config.js','prettier.config.mjs',
        'biome.json','biome.jsonc','.ruff.toml','ruff.toml','.editorconfig',
        'pyproject.toml','tsconfig.json','jsconfig.json','.flake8','setup.cfg'}
    if bn in protected_configs:
        print(f'WARNING: Modifying config file {bn}. Fix the code, not the config.', file=sys.stderr)
    # File bloat warning on Write (new files)
    if tn=='Write' and c:
        line_count=c.count(chr(10))+1
        if line_count>500:
            print(f'WARNING: Creating {line_count}-line file. Consider splitting into smaller files.', file=sys.stderr)
    # Block TASKS.md creation in repos that have IDEAS.md
    if bn.upper()=='TASKS.MD' and 'Claude_Workspace' not in fp:
        ideas=os.path.join(os.path.dirname(fp),'IDEAS.md')
        if os.path.isfile(ideas):
            block('BLOCKED: This repo already has IDEAS.md. Add tasks there instead of creating TASKS.md.')
    # MEMORY.md discipline: no feedback file names in MEMORY.md, any format.
    if bn=='MEMORY.md' and c:
        for line in c.splitlines():
            if re.search(r'feedback_[a-z_]+\.md',line):
                block('BLOCKED: MEMORY.md discipline - never reference individual feedback files by name (any format). Add cross-cutting rules to enforced-rules.md; use ls+grep for navigation. See enforced-rules.md MEMORY.md discipline.')
" || exit 2
