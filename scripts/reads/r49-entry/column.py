# One founder's 5 m matter column from the field dump nearest its landing, with the bed under
# its first recorded position: which layer reads its fmat, and does the column's mean match fmcol.
import glob, json, struct, sys
run = sorted(glob.glob('runs/r49-s1/2026*'))[-1]
lay = json.load(open(run + '/fields/layout.json'))
m = lay['matter']; NX, NY, NZ, C = m['cellsX'], m['cellsY'], m['cellsZ'], m['cellMetres']
def floats(p):
    b = open(p, 'rb').read(); return struct.unpack('<%df' % (len(b) // 4), b)
bed = floats(run + '/fields/bed.f32'); BZ = lay['bed']['cellsZ']
def case(fid, t_dump, x, z, y, fmat, fmcol):
    cells = floats(run + '/fields/%09d.matter.f32' % t_dump)
    ix, iz = int(x // C), int(z // C)
    col = [cells[(iy * NX + ix) * NZ + iz] for iy in range(NY)]
    live = [(iy, v) for iy, v in enumerate(col) if v != 0]
    b = bed[int(x) * BZ + int(z)]
    print(f'founder {fid}: x {x} z {z} first y {y}, bed under it {b:.2f} m, matter column ({ix},{iz})')
    for iy, v in live:
        tag = ' <- fmat' if abs(v / 125 - fmat) / fmat < 0.02 or abs(v - fmat) / fmat < 0.02 else ''
        print(f'   layer {iy:2d} [{-(iy+1)*C:4d},{-iy*C:4d}] m  value {v:.6g}  /125 m3 {v/125:.6g}{tag}')
    vals = [v for _, v in live]
    print(f'   live cells {len(vals)}, mean {sum(vals)/len(vals):.6g} (/125: {sum(vals)/len(vals)/125:.6g}); fmat {fmat}, fmcol {fmcol}')
case(4422, 6200, 77.5, 32.5, -44.19, 0.0116730165, 0.0119169485)
