"""Word-by-word captions for a Short, burned in with libass (README.md beside this file).
python -I captions.py <job.json> 45 [font]   -> <out>/out-45/captions.ass and short-45-captions.mp4
The words and their times come from the narration's alignment (the job file maps each take to its .align.json and
where the take's zero sits in it), placed on the Short's own timeline by the segment table: a word belongs to a
segment when it starts inside the stretch of its take that the segment plays. Each phrase of up to three words
shows whole; the word being spoken is in the accent colour."""
import csv, json, os, subprocess, sys

R = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', '..')).replace(os.sep, '/') + '/'
FPS = 30
ALIGN = {}  # take -> (alignment, where the take's zero sits in the aligned file), from the job file
FMT = {'45': (1080, 1350), '916': (1080, 1920)}
MAXWORDS, MAXCHARS = 3, 17
HOLD = 0.35          # a phrase stays this long after its last word ends, never into the next phrase
WHITE, ACCENT = '&H00FFFFFF&', '&H004DD3FF&'   # ASS colours are &HAABBGGRR: the accent is #FFD34D


def words_of(take):
    aj, zero = ALIGN[take]
    d = json.load(open(R + aj, encoding='utf-8'))
    out = []
    for s in d['sentences']:  # the script's own spelling where the word count matches, as make_srt.py does
        ws = [w for w in d['words'] if s['start'] - 0.01 <= w['start'] and w['end'] <= s['end'] + 0.01]
        toks = s['text'].split()
        if len(toks) == len(ws):
            ws = [dict(w, text=t) for w, t in zip(ws, toks)]
        for i, w in enumerate(ws):
            out.append(dict(text=w['text'], start=w['start'] - zero, end=w['end'] - zero, last=i == len(ws) - 1))
    return out


def phrases(ws):
    """Clauses end at punctuation, a sentence's end or a pause; each clause splits into the fewest even groups of
    at most three words and twenty characters, larger groups first, so no word is left alone at a clause's end."""
    clauses, cur = [], []
    for w in ws:
        if cur and w['start'] - cur[-1]['end'] > 0.45:
            clauses.append(cur); cur = []
        cur.append(w)
        if w['last'] or w['text'][-1] in ',;:':
            clauses.append(cur); cur = []
    if cur: clauses.append(cur)
    out = []
    for c in clauses:
        n = len(c)
        for k in range(-(-n // MAXWORDS), n + 1):
            sizes = [n // k + (1 if i < n % k else 0) for i in range(k)]
            groups, i = [], 0
            for s in sizes:
                groups.append(c[i:i + s]); i += s
            if all(len(' '.join(x['text'] for x in g)) <= MAXCHARS for g in groups):
                break
        out += groups
    return out


def tc(s):
    cs = int(round(s * 100)); h, cs = divmod(cs, 360000); m, cs = divmod(cs, 6000); sec, cs = divmod(cs, 100)
    return f'{h}:{m:02d}:{sec:02d}.{cs:02d}'


def main(job, fmt, font):
    J = json.load(open(job, encoding='utf-8'))
    ALIGN.update({k: tuple(v) for k, v in J['align'].items()})
    W, H = FMT[fmt]
    out = R + J['out'] + f'out-{fmt}/'
    rows = [r for r in csv.reader(open(R + J['segments'], encoding='utf-8'), delimiter='\t') if not r[0].startswith('#')]
    t0, words = 0.0, []
    for n, src, inf, nf, slope, aud, ain, note in rows:
        dur = int(nf) / FPS
        if aud != '-':
            a = float(ain)
            for w in words_of(os.path.basename(aud)):
                if 0 <= w['start'] - a < dur:  # placed on the Short; a word cut by the segment's end ends with it
                    words.append(dict(w, start=t0 + w['start'] - a, end=t0 + min(w['end'] - a, dur), seg_end=t0 + dur))
        t0 += dur
    ph = phrases(words)
    ev = []
    for k, p in enumerate(ph):
        nxt = ph[k + 1][0]['start'] if k + 1 < len(ph) else 1e9
        stop = min(p[-1]['end'] + HOLD, nxt, p[-1]['seg_end'])
        for i, w in enumerate(p):
            a = p[0]['start'] if i == 0 else w['start']
            b = p[i + 1]['start'] if i + 1 < len(p) else stop
            txt = ' '.join((f'{{\\c{ACCENT}}}{x["text"]}{{\\c{WHITE}}}' if j == i else x['text']) for j, x in enumerate(p))
            ev.append((a, b, txt))
    size = round(W * 0.11)
    head = f"""[Script Info]
ScriptType: v4.00+
PlayResX: {W}
PlayResY: {H}
WrapStyle: 0
ScaledBorderAndShadow: yes

[V4+ Styles]
Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding
Style: Word,{font},{size},{WHITE},{WHITE},&H00101010&,&H80000000&,-1,0,0,0,100,100,0,0,1,{round(size * 0.09)},{round(size * 0.05)},2,70,70,{round(H * 0.20)},1

[Events]
Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text
"""
    with open(out + 'captions.ass', 'w', encoding='utf-8') as f:
        f.write(head)
        for a, b, t in ev:
            f.write(f'Dialogue: 0,{tc(a)},{tc(b)},Word,,0,0,0,,{t}\n')
    print(len(words), 'words,', len(ph), 'phrases, longest', max(len(' '.join(x['text'] for x in p)) for p in ph), 'chars')
    # run from out/ so the filter's path needs no drive-letter escaping
    r = subprocess.run(['ffmpeg', '-nostdin', '-v', 'error', '-y', '-i', f'short-{fmt}.mp4', '-vf', 'subtitles=captions.ass',
                        '-c:v', 'libx264', '-preset', 'slow', '-crf', '16', '-pix_fmt', 'yuv420p', '-profile:v', 'high',
                        '-c:a', 'copy', '-movflags', '+faststart', f'short-{fmt}-captions.mp4'],
                       cwd=out, capture_output=True, text=True)
    if r.returncode:
        sys.exit(r.stderr[-2000:])
    print('wrote', out + f'short-{fmt}-captions.mp4')


if __name__ == '__main__':
    main(sys.argv[1], sys.argv[2], sys.argv[3] if len(sys.argv) > 3 else 'Segoe UI Black')
