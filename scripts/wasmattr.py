import sys, re, collections
# usage: wasmattr.py <wasm with name section> <wasm-ld map>
# Attributes final (post wasm-opt) function body sizes to archives using the link map.

def leb(b, i):
    r = s = 0
    while True:
        x = b[i]; i += 1
        r |= (x & 0x7f) << s; s += 7
        if x < 0x80:
            return r, i

wasm = open(sys.argv[1], 'rb').read()
i = 8
nimports_func = 0
bodies = []
names = {}
while i < len(wasm):
    sid = wasm[i]; i += 1
    size, i = leb(wasm, i)
    end = i + size
    if sid == 2:  # imports
        n, j = leb(wasm, i)
        for _ in range(n):
            l, j = leb(wasm, j); j += l
            l, j = leb(wasm, j); j += l
            kind = wasm[j]; j += 1
            if kind == 0:
                _, j = leb(wasm, j); nimports_func += 1
            elif kind == 1:
                j += 1; fl, j = leb(wasm, j); _, j = leb(wasm, j)
                if fl & 1: _, j = leb(wasm, j)
            elif kind == 2:
                fl, j = leb(wasm, j); _, j = leb(wasm, j)
                if fl & 1: _, j = leb(wasm, j)
            elif kind == 3:
                j += 2
            elif kind == 4:
                j += 1; _, j = leb(wasm, j)
    elif sid == 10:
        n, j = leb(wasm, i)
        for _ in range(n):
            l, j2 = leb(wasm, j)
            bodies.append(l + (j2 - j)); j = j2 + l
    elif sid == 0:
        l, j = leb(wasm, i); nm = wasm[j:j + l].decode(); j += l
        if nm == 'name':
            while j < end:
                sub = wasm[j]; j += 1
                ssz, j = leb(wasm, j); send = j + ssz
                if sub == 1:
                    n, k = leb(wasm, j)
                    for _ in range(n):
                        idx, k = leb(wasm, k); l2, k = leb(wasm, k)
                        names[idx] = wasm[k:k + l2].decode('utf-8', 'replace'); k += l2
                j = send
    i = end

# map: symbol -> archive (from the CODE section of the wasm-ld map)
sym2key = {}
cur = None
rx = re.compile(r'\s*\S+\s+\S+\s+([0-9a-f]+)\s{9}(\S.*)$')
rs = re.compile(r'\s*\S+\s+\S+\s+([0-9a-f]+)\s{17}(\S.*)$')
sec = None
for line in open(sys.argv[2], encoding='utf-8', errors='replace'):
    p = line.split()
    if len(p) == 4 and p[3] in ('TYPE','IMPORT','FUNCTION','TABLE','MEMORY','TAG','GLOBAL','EXPORT','ELEM','DATACOUNT','CODE','DATA'):
        sec = p[3]; continue
    if sec != 'CODE':
        continue
    m = rx.match(line)
    if m and ':(' in m.group(2):
        path = m.group(2).split(':(')[0].replace('\\', '/')
        if '.a(' in path:
            cur = path.split('.a(')[0].split('/')[-1] + '.a'
        else:
            cur = 'obj:' + path.split('/')[-1]
        sym2key.setdefault(m.group(2).split(':(', 1)[1].rstrip().rstrip(')'), cur)
        continue
    m = rs.match(line)
    if m and cur:
        sym2key.setdefault(m.group(2).strip(), cur)

tot = collections.Counter(); unk = 0
for k, sz in enumerate(bodies):
    nm = names.get(nimports_func + k)
    key = sym2key.get(nm)
    if key is None:
        key = '?unmapped'
    tot[key] += sz
print(f'functions {len(bodies)} code bytes {sum(bodies):,} mapped names {len(sym2key)}')
MONO = ('libmonosgen', 'libmono-', 'libicu', 'libSystem.', 'libz.a', 'wasm-bundled', 'libbrotli')
mono = sum(v for k, v in tot.items() if k.startswith(MONO))
print(f'Mono runtime pack code (final): {mono:,}')
for k, v in tot.most_common(40):
    print(f'{v:>12,}  {k}')
