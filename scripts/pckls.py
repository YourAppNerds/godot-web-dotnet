import struct,sys,collections
f=open(sys.argv[1],'rb');d=f.read()
assert d[:4]==b'GDPC'
fmt,ma,mi,pa,flags=struct.unpack_from('<5I',d,4)
base,diro=struct.unpack_from('<QQ',d,24)
print('fmt',fmt,ma,mi,pa,'flags',flags)
off=diro if fmt>=3 else 24+8+64
n,=struct.unpack_from('<I',d,off);off+=4
ent=[]
for i in range(n):
  l,=struct.unpack_from('<I',d,off);off+=4
  p=d[off:off+l].rstrip(b'\0').decode();off+=l
  o,s=struct.unpack_from('<QQ',d,off);off+=16+16+4
  ent.append((p,s))
ext=collections.Counter()
for p,s in ent:
  import os
  e=os.path.splitext(p)[1] or p.split('/')[-1]
  ext[e]+=s
for e,s in ext.most_common(): print(f'{s:>10} {e}')
print('total',sum(s for _,s in ent),'files',n)
if len(sys.argv)>2:
  for p,s in sorted(ent,key=lambda x:-x[1]): print(s,p)
