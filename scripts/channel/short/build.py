"""A film's Short, built outside Resolve with ffmpeg from the film's 4K clips (README.md beside this file).
python -I build.py <job.json> 916   -> <out>/out-916/short-916.mp4 (1080x1920, YouTube Shorts)
python -I build.py <job.json> 45    -> <out>/out-45/short-45.mp4 (1080x1350, LinkedIn); then captions.py burns the words
The job file names the segment table (source frames, the CDL slope the cut gives the shot, the narration and its
in-point), each segment's crop centre as a share of the 3840 frame width, and the output folder under scratch/."""
import subprocess, sys, os, csv, json
from PIL import Image, ImageDraw, ImageFont

R = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', '..')).replace(os.sep, '/') + '/'
FFMPEG = 'ffmpeg'
FPS = 30
FMT = {'916': (1080, 1920, 1215), '45': (1080, 1350, 1728)}  # out w, out h, crop width in the 3840x2160 source


def run(args):
    r = subprocess.run(args, capture_output=True, text=True)
    if r.returncode:
        sys.exit('ffmpeg failed:\n' + ' '.join(args) + '\n' + r.stderr[-2000:])


def endcard_text(w, h, path, fade=False):
    img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    if fade:  # a soft dark fade under the words, from 0.58 of the height to the bottom
        y0 = int(h * 0.58)
        for y in range(y0, h):
            a = int(150 * min(1.0, (y - y0) / (h * 0.16)))
            d.line([(0, y), (w, y)], fill=(0, 8, 14, a))
    f = ImageFont.truetype(R + 'unity/Assets/Theatre/UI/Fonts/IBMPlexSans-Regular.ttf', int(w * 0.085))
    t = 'Watch the full film'
    tw = d.textlength(t, font=f)
    y = int(h * 0.74)
    d.text(((w - tw) / 2, y), t, font=f, fill=(235, 242, 245, 255))
    img.save(path)


def main(job, fmt):
    J = json.load(open(job, encoding='utf-8'))
    CENTRE = {int(k): tuple(v) for k, v in J['centre'].items()}  # (start, end) share of width; equal values hold still
    W, H, CW = FMT[fmt]
    out = R + J['out'] + f'out-{fmt}/'
    os.makedirs(out, exist_ok=True)
    rows = [r for r in csv.reader(open(R + J['segments'], encoding='utf-8'), delimiter='\t') if not r[0].startswith('#')]
    parts = []
    for n, src, inf, nf, slope, aud, ain, note in rows:
        n, inf, nf, slope, ain = int(n), int(inf), int(nf), float(slope), float(ain)
        v = out + f'v{n}.mov'
        trim = f'trim=start_frame={inf}:end_frame={inf + nf},setpts=PTS-STARTPTS'
        grade = f',format=gbrp10le,colorchannelmixer=rr={slope}:gg={slope}:bb={slope}' if slope != 1.0 else ''
        if n in CENTRE:
            c0, c1 = CENTRE[n]
            T = nf / FPS
            x = f"'max(0,min(iw-{CW},({c0}+({c1}-{c0})*t/{T})*iw-{CW}/2))'"
            vf = f'{trim}{grade},crop={CW}:2160:{x}:0,scale={W}:{H}:flags=lanczos,format=yuv422p10le'
            run([FFMPEG, '-nostdin', '-v', 'error', '-y', '-i', R + src, '-vf', vf, '-an',
                 '-c:v', 'prores_ks', '-profile:v', '3', v])
        elif fmt == '45':  # end card, 4:5: the centre crop holds the wordmark whole (0.30-0.70 of the width)
            png = out + 'endcard-text.png'
            endcard_text(W, H, png, fade=True)
            fc = (f'[0:v]{trim}{grade},crop={CW}:2160:(iw-{CW})/2:0,scale={W}:{H}:flags=lanczos[c];'
                  f'[c][1:v]overlay=0:0,format=yuv422p10le')
            run([FFMPEG, '-nostdin', '-v', 'error', '-y', '-i', R + src, '-loop', '1', '-i', png,
                 '-filter_complex', fc, '-frames:v', str(nf), '-an', '-c:v', 'prores_ks', '-profile:v', '3', v])
        else:  # end card, 9:16: the whole frame over a blurred fill, and the call to the film
            png = out + 'endcard-text.png'
            endcard_text(W, H, png)
            # the wordmark sits in the middle 0.30-0.70 of the width, near the top: a square of the frame's middle
            sq = 1600
            fc = (f'[0:v]{trim}{grade},split[a][b];'
                  f'[a]scale=-2:{H},crop={W}:{H},boxblur=40:2,eq=brightness=-0.15[bg];'
                  f'[b]crop={sq}:{sq}:(iw-{sq})/2:0,scale={W}:{W}:flags=lanczos[fg];'
                  f'[bg][fg]overlay=0:(H-h)/2-{H // 14}[c];[c][1:v]overlay=0:0,format=yuv422p10le')
            run([FFMPEG, '-nostdin', '-v', 'error', '-y', '-i', R + src, '-loop', '1', '-i', png,
                 '-filter_complex', fc, '-frames:v', str(nf), '-an', '-c:v', 'prores_ks', '-profile:v', '3', v])
        a = out + f'a{n}.wav'
        dur = nf / FPS
        if aud != '-':
            run([FFMPEG, '-nostdin', '-v', 'error', '-y', '-ss', f'{ain:.3f}', '-t', f'{dur:.4f}', '-i', R + aud,
                 '-af', f'apad,atrim=0:{dur:.4f}', '-ar', '48000', '-ac', '2', a])
        else:
            run([FFMPEG, '-nostdin', '-v', 'error', '-y', '-f', 'lavfi', '-i', 'anullsrc=r=48000:cl=stereo',
                 '-t', f'{dur:.4f}', a])
        parts.append((v, a))
        print('segment', n, note, 'done', flush=True)
    with open(out + 'v.txt', 'w') as f:
        f.writelines(f"file '{p[0]}'\n" for p in parts)
    with open(out + 'a.txt', 'w') as f:
        f.writelines(f"file '{p[1]}'\n" for p in parts)
    final = out + f'short-{fmt}.mp4'
    run([FFMPEG, '-nostdin', '-v', 'error', '-y', '-f', 'concat', '-safe', '0', '-i', out + 'v.txt',
         '-f', 'concat', '-safe', '0', '-i', out + 'a.txt', '-map', '0:v', '-map', '1:a',
         '-c:v', 'libx264', '-preset', 'slow', '-crf', '16', '-pix_fmt', 'yuv420p', '-profile:v', 'high',
         '-c:a', 'aac', '-b:a', '256k', '-movflags', '+faststart', final])
    print('wrote', final)


if __name__ == '__main__':
    main(sys.argv[1], sys.argv[2])
