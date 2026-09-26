"""Byte-preserving text edits for this repo's source files.

Reads and writes bytes, so a file's BOM (or lack of one) and its line endings survive an
edit untouched. `simHash` is a digest of the bytes of every .cs under Assets/Evosim
(CLAUDE.md), and this working tree is already mixed CRLF/LF, so an editor that "helpfully"
normalises either one changes the identity of a build for no reason anybody can see.
"""
import io
import sys


def read(path):
    b = io.open(path, 'rb').read()
    bom = b[:3] == b'\xef\xbb\xbf'
    text = b[3:].decode('utf-8') if bom else b.decode('utf-8')
    crlf = '\r\n' in text
    return text, bom, crlf


def write(path, text, bom, crlf):
    if crlf and '\r\n' not in text:
        raise SystemExit('refusing to write LF into a CRLF file: ' + path)
    data = text.encode('utf-8')
    if bom:
        data = b'\xef\xbb\xbf' + data
    io.open(path, 'wb').write(data)


def sub(path, old, new, count=1):
    text, bom, crlf = read(path)
    eol = '\r\n' if crlf else '\n'
    old = old.replace('\r\n', '\n').replace('\n', eol)
    new = new.replace('\r\n', '\n').replace('\n', eol)
    n = text.count(old)
    if n != count:
        raise SystemExit('%s: found %d occurrences of the anchor, wanted %d' % (path, n, count))
    write(path, text.replace(old, new, count), bom, crlf)
    print('edited %s (%s, %s)' % (path, 'BOM' if bom else 'no BOM', 'CRLF' if crlf else 'LF'))
