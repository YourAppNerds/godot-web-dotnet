import sys, gzip, brotli, os
# usage: sizes.py file...
for f in sys.argv[1:]:
    d = open(f,'rb').read()
    g = len(gzip.compress(d, 9)); b = len(brotli.compress(d, quality=11))
    print(f"{os.path.basename(f):60s} raw {len(d):>11,} gz {g:>11,} br {b:>11,}")
