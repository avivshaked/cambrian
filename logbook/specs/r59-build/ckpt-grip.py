"""Two checkpoints of the smoke: whether their states are the same bytes, and the world's grip
joules (the GRIP section after LEAF, a little-endian double) in each.

Usage: python ckpt-grip.py <a.ckpt> <b.ckpt>
Exits 0 when the two are the same bytes, 1 when not.
"""
import zlib
import struct
import sys

GZIP_MAGIC = bytes([0x1F, 0x8B, 0x08])


def payload(path):
    """The state inside a version-6 checkpoint: its one gzip member, after the plain header."""
    raw = open(path, "rb").read()
    at = raw.find(GZIP_MAGIC)
    if at < 0:
        sys.exit(f"{path}: no gzip member")
    # One member, then the header's trailing fields: decompress the member alone. The header
    # itself names the run's own arm and run, so two arms' files differ there by design.
    return zlib.decompressobj(wbits=31).decompress(raw[at:])


def grip_joules(data):
    leaf = data.find(b"LEAF")
    if leaf < 0:
        return None
    at = data.find(b"GRIP", leaf)
    if at < 0:
        return None
    return struct.unpack_from("<d", data, at + 4)[0]


def body_grips(data):
    """Every body's GRIP block (Creature.WriteState), checked by its array lengths: bodies that
    can grip, grips held now, grips formed and broken, link-seconds held."""
    bodies = held = formed = broken = 0
    seconds = 0.0
    at = data.find(b"GRIP")
    while at >= 0:
        p = at + 4
        try:
            n = struct.unpack_from("<i", data, p)[0]
            if not 1 <= n <= 64:
                raise ValueError
            p += 4
            flags = data[p:p + n]
            if any(f > 1 for f in flags):
                raise ValueError
            p += n
            for width in (3, 9, 3, 1):
                if struct.unpack_from("<i", data, p)[0] != width * n:
                    raise ValueError
                p += 4 + 8 * width * n
            _low, _owed, f, b, s = struct.unpack_from("<ddqqd", data, p)
            bodies += 1
            held += sum(flags)
            formed += f
            broken += b
            seconds += s
        except (ValueError, struct.error):
            pass
        at = data.find(b"GRIP", at + 4)
    return bodies, held, formed, broken, seconds


def main():
    a, b = (payload(p) for p in sys.argv[1:3])
    for name, data in (("a", a), ("b", b)):
        bodies, held, formed, broken, seconds = body_grips(data)
        print(f"{name}: {len(data)} bytes, world grip joules {grip_joules(data)}; {bodies} bodies can grip, "
              f"{held} grips held, {formed} formed, {broken} broken, {seconds:.1f} link-seconds held")
    if a == b:
        print("checkpoints identical")
        return 0
    n = min(len(a), len(b))
    first = next((i for i in range(n) if a[i] != b[i]), n)
    print(f"checkpoints differ: first byte {first} of {len(a)} and {len(b)}")
    return 1


if __name__ == "__main__":
    sys.exit(main())
