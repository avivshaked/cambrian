"""Contrast check for the nursery room's screens, with numbers rather than by eye.

For each screen it renders the page at the given size, reads every piece of text's colour, opacity
and box from the live page (contrast-probe.html), and reads the background behind each box from
the screenshot render.ps1 made at the same size. It reports:

  * each panel's median background and each ink tier's ratio against it;
  * every text below 4.5:1 against what is actually behind it, and the count below 7:1.

Rule (a8, relaying the owner, 2026-10-02): every text pixel at least 4.5:1 against what is behind
it; numbers read at a glance at least 7:1.

Usage, from design/nursery/:  python contrast.py [--size 1920x1080] [--only 3-run] [--list 12]
Render first:                 pwsh -File render.ps1   (same size)
Built screens:                python contrast.py --base http://127.0.0.1:5179/ [--pages run.html?id=x,...]
                              (the room running; the shots go to scratch/room-shots/)
"""
import argparse, http.server, json, re, subprocess, sys, threading, urllib.error, urllib.parse, urllib.request
from pathlib import Path
import numpy as np
from PIL import Image

HERE = Path(__file__).resolve().parent
ROOT = HERE.parent.parent
EDGE = r'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe'


def lum(rgb):
    c = np.asarray(rgb, dtype=float) / 255
    c = np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)
    return c[..., 0] * 0.2126 + c[..., 1] * 0.7152 + c[..., 2] * 0.0722


def ratio(a, b):
    la, lb = lum(a), lum(b)
    return (max(la, lb) + 0.05) / (min(la, lb) + 0.05)


def parse_color(s):
    m = re.match(r'rgba?\(([^)]+)\)', s)
    if m:
        v = [float(x) for x in re.split(r'[ ,/]+', m.group(1).strip()) if x]
        return v[:3], (v[3] if len(v) > 3 else 1.0)
    s = s.lstrip('#')
    if len(s) == 3:
        s = ''.join(ch * 2 for ch in s)
    return [int(s[i:i + 2], 16) for i in (0, 2, 4)], 1.0


def edge(args, timeout=180):
    arglist = ','.join("'" + x.replace("'", "''") + "'" for x in args)
    subprocess.run(['pwsh', '-NoProfile', '-Command', f"Start-Process -FilePath '{EDGE}' -ArgumentList @({arglist}) -Wait -WindowStyle Hidden"],
                   check=True, timeout=timeout)


def same_origin(base):
    """A built screen comes from the room's server, and the probe can read a frame only from its own
    origin. So the probe is served from a port on 127.0.0.1 that passes every other request through
    to the room, and screen and probe share that origin. Returns the port's address."""
    base = base.rstrip('/')

    class Pass(http.server.BaseHTTPRequestHandler):
        def do_GET(self):
            if self.path.split('?')[0] == '/contrast-probe.html':
                body, kind, code = (HERE / 'contrast-probe.html').read_bytes(), 'text/html; charset=utf-8', 200
            else:
                try:
                    with urllib.request.urlopen(base + self.path, timeout=30) as r:
                        body, kind, code = r.read(), r.headers.get('Content-Type', 'application/octet-stream'), r.status
                except urllib.error.HTTPError as e:
                    body, kind, code = e.read(), e.headers.get('Content-Type', 'text/plain'), e.code
            self.send_response(code)
            self.send_header('Content-Type', kind)
            self.send_header('Content-Length', str(len(body)))
            self.end_headers()
            self.wfile.write(body)

        def log_message(self, *a):
            pass

    srv = http.server.ThreadingHTTPServer(('127.0.0.1', 0), Pass)
    threading.Thread(target=srv.serve_forever, daemon=True).start()
    return f'http://127.0.0.1:{srv.server_address[1]}'


def probe(page, w, h, origin=None):
    # Edge's launcher hands off to a child and returns, so --dump-dom's stdout never arrives; the
    # probe logs its result to the console instead, --enable-logging writes the console to
    # chrome_debug.log in the profile, and Start-Process -Wait waits for the whole tree.
    prof = ROOT / 'scratch' / 'edge-nursery-probe'
    prof.mkdir(parents=True, exist_ok=True)
    log = prof / 'chrome_debug.log'
    log.unlink(missing_ok=True)
    q = f'?p={page}&w={w}&h={h}'
    if origin:
        url = origin + '/contrast-probe.html' + q + '&src=' + urllib.parse.quote('/' + page, safe='')
    else:
        url = 'file:///' + (HERE / 'contrast-probe.html').as_posix() + q
    args = ['--headless=new', '--disable-gpu', '--hide-scrollbars', '--no-first-run', '--allow-file-access-from-files',
            f'--user-data-dir={prof}', f'--window-size={w},{h + 200}', '--virtual-time-budget=4000',
            '--enable-logging', '--v=0', '--dump-dom', url]
    edge(args)
    text = log.read_text(encoding='utf-8', errors='replace') if log.exists() else ''
    m = re.search(r'PROBE(\{.*?\})PROBE', text)
    if not m:
        sys.exit(f'{page}: the probe logged nothing to {log}')
    return json.loads(m.group(1))


def tank(w, h):
    """The tank's own measure: 4-tank.html?measure draws masks of the focus body, each tank-mate and
    the snow, and logs the focus body's median luminance against the water around it and the
    brightest mote against the dimmest visible tank-mate."""
    prof = ROOT / 'scratch' / 'edge-nursery-probe'
    prof.mkdir(parents=True, exist_ok=True)
    log = prof / 'chrome_debug.log'
    log.unlink(missing_ok=True)
    url = 'file:///' + (HERE / '4-tank.html').as_posix() + '?measure'
    args = ['--headless=new', '--disable-gpu', '--hide-scrollbars', '--no-first-run', '--allow-file-access-from-files',
            f'--user-data-dir={prof}', f'--window-size={w},{h}', '--virtual-time-budget=6000', '--enable-logging', '--v=0', '--dump-dom', url]
    arglist = ','.join("'" + x.replace("'", "''") + "'" for x in args)
    subprocess.run(['pwsh', '-NoProfile', '-Command', f"Start-Process -FilePath '{EDGE}' -ArgumentList @({arglist}) -Wait -WindowStyle Hidden"], check=True, timeout=300)
    text = log.read_text(encoding='utf-8', errors='replace') if log.exists() else ''
    m = re.search(r'TANK(\[.*?\])TANK', text)
    if not m:
        sys.exit(f'tank: the measure logged nothing to {log}')
    print(f'== 4-tank scene at {w}x{h} (rule: focus body at least 5x the water; no mote brighter than a tank-mate) ==')
    for v in json.loads(m.group(1)):
        mates = 'no tank-mate in view' if v['mateMedianMin'] is None else f"dimmest of {v['matesVisible']} tank-mates {v['mateMedianMin']:.4f}"
        ok1 = v['ratio'] >= 5
        ok2 = v['mateMedianMin'] is None or v['moteMax'] < v['mateMedianMin']
        print(f"  {v['view']:<5} body {v['bodyWidthShare'] * 100:.0f}% of the view's width · focus {v['focusMedian']:.4f} / water {v['waterMedian']:.4f} = {v['ratio']:.1f}x {'ok' if ok1 else 'LOW'}"
              f" · brightest mote {v['moteMax']:.4f} vs {mates} {'ok' if ok2 else 'TOO BRIGHT'}")


def main():
    sys.stdout.reconfigure(encoding='utf-8')
    ap = argparse.ArgumentParser()
    ap.add_argument('--size', default='1920x1080')
    ap.add_argument('--only', default='')
    ap.add_argument('--list', type=int, default=12, help='how many failing texts to list per screen')
    ap.add_argument('--tank', action='store_true', help="measure the tank scene's brightness ranking instead")
    ap.add_argument('--base', default='', help='measure the built screens a running room serves, e.g. http://127.0.0.1:5179/')
    ap.add_argument('--pages', default='registry.html,run.html,recipe.html',
                    help='with --base: the addresses under it to measure, comma-separated, query strings allowed')
    a = ap.parse_args()
    w, h = (int(x) for x in a.size.split('x'))
    if a.tank:
        return tank(w, h)
    suffix = '' if (w, h) == (1920, 1080) else f'-{w}x{h}'
    if a.base:
        # The built screens are shot here rather than by render.ps1, into scratch/: they show live data.
        pages = [p for p in a.pages.split(',') if p]
        shots = ROOT / 'scratch' / 'room-shots'
        shots.mkdir(parents=True, exist_ok=True)
        origin = same_origin(a.base)
    else:
        pages = sorted(p.stem for p in HERE.glob('[0-9]-*.html'))
        if a.only:
            pages = [p for p in pages if p == a.only]
    worst_all = []
    for page in pages:
        if a.base:
            src = a.base.rstrip('/') + '/' + page
            shot = shots / (re.sub(r'[^A-Za-z0-9.-]+', '_', page) + f'-{w}x{h}.png')
            edge(['--headless=new', '--disable-gpu', '--hide-scrollbars', '--no-first-run',
                  f'--user-data-dir={ROOT / "scratch" / "edge-nursery"}', f'--window-size={w},{h}',
                  '--virtual-time-budget=4000', f'--screenshot={shot}', src])
            img = np.asarray(Image.open(shot).convert('RGB'))
            d = probe(page, w, h, origin)
        else:
            shot = HERE / 'shots' / f'{page}{suffix}.png'
            img = np.asarray(Image.open(shot).convert('RGB'))
            d = probe(page, w, h)
        print(f'\n== {page} at {w}x{h} ==')
        meds = []
        for p in d['panels']:
            x0, y0 = max(p['x'] + 8, 0), max(p['y'] + 8, 0)
            x1, y1 = min(p['x'] + p['w'] - 8, img.shape[1]), min(p['y'] + p['h'] - 8, img.shape[0])
            if x1 <= x0 or y1 <= y0:
                continue
            meds.append(np.median(img[y0:y1, x0:x1].reshape(-1, 3), axis=0))
        if meds:
            bg = np.median(np.array(meds), axis=0)
            print(f'panel median background rgb({bg[0]:.0f},{bg[1]:.0f},{bg[2]:.0f}), luminance {lum(bg) * 100:.2f}%')
            print('  ' + '  '.join(f'{k} {ratio(parse_color(v)[0], bg):.1f}:1' for k, v in d['ink'].items() if v))
        fails, below7, n = [], 0, 0
        for r in d['rows']:
            x0, y0 = max(r['x'], 0), max(r['y'], 0)
            x1, y1 = min(r['x'] + r['w'], img.shape[1]), min(r['y'] + r['h'], img.shape[0])
            if x1 <= x0 or y1 <= y0:
                continue
            region = img[y0:y1, x0:x1].reshape(-1, 3).astype(float)
            # The background behind a line of text is the part of its box least like the text's own
            # colour: glyphs are a minority of the box, light on dark or dark on a plate alike.
            rgb, alpha = parse_color(r['c'])
            dist = np.abs(region - np.asarray(rgb, dtype=float)).sum(axis=1)
            back = np.median(region[dist >= np.percentile(dist, 50)], axis=0)
            alpha *= r['o']
            fg = np.asarray(rgb) * alpha + back * (1 - alpha)
            cr = ratio(fg, back)
            n += 1
            if cr < 7:
                below7 += 1
            if cr < 4.5:
                fails.append((cr, r))
        fails.sort(key=lambda f: f[0])
        print(f'texts {n} · below 4.5:1 {len(fails)} · below 7:1 {below7}')
        for cr, r in fails[:a.list]:
            print(f'  {cr:4.1f}:1  {r["px"]:4.1f}px  {r["c"]:<24} op {r["o"]:<5}  "{r["t"]}"')
        worst_all += [(cr, page, r['t']) for cr, r in fails]
    print(f'\nall screens: {len(worst_all)} texts below 4.5:1')


if __name__ == '__main__':
    main()
