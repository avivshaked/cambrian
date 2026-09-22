"""Read a Windows kernel triage minidump (the PAGEDU64 files under C:/Windows/Minidump) without
a debugger. Standard library only.

    python scripts/read-minidump.py scratch/crash/092226-10390-01.dmp [--drivers]

Prints the bugcheck and its parameters, the loaded-driver table (with --drivers), the driver
whose image holds the faulting address, the registers the faulting code had, every return
address on the saved stack mapped to its driver, and the unloaded-driver list. For bugcheck
0x3B the third parameter is the address of the CONTEXT at the fault and it sits on the saved
stack, so the fault's own registers are read from there rather than KeBugCheckEx's.

Offsets are the triage-dump layout WinDbg documents (DUMP_HEADER64 at 0, TRIAGE_DUMP64 at
0x2000, driver entries of 0x90 bytes each: a name offset into the string pool, three list
links, DllBase at +0x38, SizeOfImage at +0x48, TimeDateStamp at +0x88). They were checked on
the 2026-09-22 dump by reading ntoskrnl's base, size and stamp back against the file on
disk (logbook/specs/crash-2026-09-22.md).
"""
import datetime
import struct
import sys
from pathlib import Path

path = Path(sys.argv[1])
show_drivers = '--drivers' in sys.argv
d = path.read_bytes()
if d[:8] != b'PAGEDU64':
    sys.exit('not a 64-bit kernel dump: signature %r' % d[:8])

u16 = lambda o: struct.unpack_from('<H', d, o)[0]
u32 = lambda o: struct.unpack_from('<I', d, o)[0]
u64 = lambda o: struct.unpack_from('<Q', d, o)[0]


def stamp(t):
    if t < 2_000_000_000:
        return datetime.datetime.utcfromtimestamp(t).strftime('%Y-%m-%d %H:%M')
    return 'hashed %08x' % t


# DUMP_HEADER64
print('%s: build %d.%d, %d processors' % (path.name, u32(0x8), u32(0xC), u32(0x34)))
code = u32(0x38)
params = [u64(0x40 + 8 * i) for i in range(4)]
print('bugcheck 0x%X  params %s' % (code, ' '.join('%016x' % x for x in params)))

# TRIAGE_DUMP64 at 0x2000
T = 0x2000
names = ['ServicePackBuild', 'SizeOfDump', 'ValidOffset', 'ContextOffset', 'ExceptionOffset', 'MmOffset',
         'UnloadedDriversOffset', 'PrcbOffset', 'ProcessOffset', 'ThreadOffset', 'CallStackOffset',
         'SizeOfCallStack', 'DriverListOffset', 'DriverCount', 'StringPoolOffset', 'StringPoolSize',
         'BrokenDriverOffset', 'TriageOptions']
tri = {n: u32(T + 4 * k) for k, n in enumerate(names)}
tri['TopOfStack'] = u64(T + 0x48)


def string_at(off):
    return d[off + 4: off + 4 + 2 * u32(off)].decode('utf-16-le', 'replace')


# The process block is a copy of the EPROCESS; its 15-byte image name is the one printable run.
proc = d[tri['ProcessOffset']:tri['ThreadOffset']]
import re
run = re.findall(rb'[\x20-\x7e]{4,15}', proc)
print('process: %s' % (run[0].decode() if run else '?'))

# Loaded drivers
drivers = []
for k in range(tri['DriverCount']):
    e = tri['DriverListOffset'] + k * 0x90
    drivers.append((u64(e + 0x38), u32(e + 0x48), string_at(u32(e)), u32(e + 0x88)))
drivers.sort()


def owner(addr):
    for base, size, name, _ in drivers:
        if base <= addr < base + size:
            return '%s+0x%x' % (name, addr - base)
    return None


print('%d drivers loaded' % len(drivers))
if show_drivers:
    for base, size, name, ts in drivers:
        print('  %016x %8x %-28s %s' % (base, size, name, stamp(ts)))

regs = ['Rax', 'Rcx', 'Rdx', 'Rbx', 'Rsp', 'Rbp', 'Rsi', 'Rdi', 'R8', 'R9', 'R10', 'R11', 'R12', 'R13', 'R14', 'R15']
cs, css, top = tri['CallStackOffset'], tri['SizeOfCallStack'], tri['TopOfStack']


def in_stack(va):
    return top <= va < top + css


def show_context(off, label):
    rip, rsp = u64(off + 0xF8), u64(off + 0x98)
    print('%s: rip %016x (%s) rsp %016x' % (label, rip, owner(rip), rsp))
    for k, r in enumerate(regs):
        v = u64(off + 0x78 + 8 * k)
        print('  %-3s %016x %s' % (r, v, owner(v) or ''))
    return rsp


show_context(tri['ContextOffset'], 'context at the bugcheck')
start = top
if code == 0x3B:
    print('faulting address %016x -> %s' % (params[1], owner(params[1])))
    if in_stack(params[2]):
        rsp = show_context(cs + (params[2] - top), 'context at the fault')
        if in_stack(rsp):
            start = rsp

print('return addresses on the saved stack from %016x (stack %016x..%016x):' % (start, top, top + css))
for va in range(start, top + css - 7, 8):
    o = owner(u64(cs + (va - top)))
    if o:
        print('  %016x %s' % (va, o))

uo = tri['UnloadedDriversOffset']
if uo:
    n = u32(uo)
    print('unloaded drivers: %d (most recent first)' % n)
    off = uo + 8
    for _ in range(min(n, 64)):
        name = d[off + 16: off + 40].decode('utf-16-le', 'replace').rstrip('\x00')
        print('  %016x-%016x %s' % (u64(off + 40), u64(off + 48), name))
        off += 56
