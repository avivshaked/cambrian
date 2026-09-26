# Where a checkpoint's bytes go: the raw payload in 2 MB chunks, each chunk's xz size, and a crude
# label (the share of 8-byte words that look like doubles of ordinary magnitude).
import glob, lzma, struct, sys, zlib
arm, s = sys.argv[1], int(sys.argv[2])
run = sorted(glob.glob(f'runs/{arm}/2026*'))[-1]
data = open(f'{run}/checkpoints/{s:09d}.ckpt', 'rb').read()
raw = zlib.decompressobj(31).decompress(data[data.find(b'\x1f\x8b\x08', 44):])
C = 2 << 20
tot = 0
rows = []
for i in range(0, len(raw), C):
    chunk = raw[i:i + C]
    c = len(lzma.compress(chunk, preset=6))
    tot += c
    ascii_share = sum(1 for x in chunk[:65536] if 32 <= x < 127) / min(len(chunk), 65536)
    rows.append((i // C, c, ascii_share))
for k, c, a in rows:
    print(f'chunk {k:3d} ({k*2:3d}-{k*2+2:3d} MB raw): xz {c/1e6:5.2f} MB  printable {a:.0%}')
print(f'total xz of chunks {tot/1e6:.1f} MB over {len(raw)/1e6:.1f} MB raw')
