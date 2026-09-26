# What a checkpoint adds over the one before it: inflate two consecutive payloads, then compress
# the second alone and with the first in front of it (xz with a window larger than both), and
# report the difference as the second's cost given the first.
import glob, lzma, sys, zlib, time
arm, a, b = sys.argv[1], int(sys.argv[2]), int(sys.argv[3])
run = sorted(glob.glob(f'runs/{arm}/2026*'))[-1]
def payload(second):
    path = f'{run}/checkpoints/{second:09d}.ckpt'
    data = open(path, 'rb').read()
    at = data.find(b'\x1f\x8b\x08', 44)
    d = zlib.decompressobj(31)
    raw = d.decompress(data[at:])
    return len(data), raw
fa, A = payload(a); fb, B = payload(b)
print(f'{arm} checkpoints at {a} and {b} s: files {fa/1e6:.1f} and {fb/1e6:.1f} MB; raw payloads {len(A)/1e6:.1f} and {len(B)/1e6:.1f} MB')
filters = [{'id': lzma.FILTER_LZMA2, 'preset': 6, 'dict_size': 1 << 28}]
t = time.time()
cA = len(lzma.compress(A, format=lzma.FORMAT_RAW, filters=filters))
cB = len(lzma.compress(B, format=lzma.FORMAT_RAW, filters=filters))
cAB = len(lzma.compress(A + B, format=lzma.FORMAT_RAW, filters=filters))
print(f'xz alone: {cA/1e6:.2f} and {cB/1e6:.2f} MB; the second given the first: {(cAB-cA)/1e6:.2f} MB '
      f'({(cAB-cA)/cB:.0%} of it alone); {time.time()-t:.0f} s')
same = sum(1 for i in range(0, min(len(A), len(B)), 4096) if A[i:i+4096] == B[i:i+4096])
print(f'4 kB blocks equal at the same offset: {same} of {min(len(A), len(B)) // 4096 + 1}')
