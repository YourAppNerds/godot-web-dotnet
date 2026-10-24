import sys, re, collections
# usage: mapsum.py map [detail-archive]
# Sums wasm-ld --Map CODE/DATA input sizes (pre-wasm-opt) per archive / Godot dir.
sec = None
tot = collections.defaultdict(collections.Counter)
det = sys.argv[2] if len(sys.argv) > 2 else None
mem = collections.Counter()
SECS = ('CODE', 'DATA', 'TYPE', 'IMPORT', 'FUNCTION', 'ELEM', 'EXPORT', 'GLOBAL', 'TABLE', 'MEMORY', 'TAG', 'DATACOUNT')
rx = re.compile(r'\s*\S+\s+\S+\s+([0-9a-f]+)\s{9}(\S.*)$')
for line in open(sys.argv[1], encoding='utf-8', errors='replace'):
    p = line.split()
    if len(p) == 4 and (p[3] in SECS or p[3].startswith('CUSTOM')):
        sec = p[3]
        continue
    if sec not in ('CODE', 'DATA'):
        continue
    m = rx.match(line)
    if not m:
        continue
    size = int(m.group(1), 16)
    inp = m.group(2)
    if ':(' not in inp:
        continue
    path = inp.split(':(')[0].replace('\\', '/')
    arch = None
    if '.a(' in path:
        a, b = path.split('.a(', 1)
        arch = (a.split('/')[-1] + '.a', b.rstrip(')'))
        key = arch[0]
    elif 'godot-wt/rebase/' in path:
        parts = path.split('godot-wt/rebase/')[-1].split('/')
        key = 'godot:' + '/'.join(parts[:2] if parts[0] in ('modules', 'thirdparty', 'platform', 'servers', 'scene', 'drivers') else parts[:1])
    elif 'lto' in path.lower():
        key = 'lto:' + path[-40:]
    elif 'cache' in path or 'emscripten' in path:
        key = 'emscripten-sysroot'
    else:
        key = path[-60:]
    tot[sec][key] += size
    if det and arch and arch[0] == det:
        mem[arch[1]] += size
for sec in ('CODE', 'DATA'):
    s = sum(tot[sec].values())
    print(f'== {sec} total {s:,}')
    for k, v in tot[sec].most_common(50):
        print(f'{v:>12,}  {k}')
if det:
    print('== members of', det)
    for k, v in mem.most_common(40):
        print(f'{v:>10,} {k}')
