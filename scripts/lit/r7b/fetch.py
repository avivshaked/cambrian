import sys, os, subprocess, json, datetime
P = r'D:\Projects\experiments\evolution-simulator\research\papers'
LOG = 'fetch-log.tsv'
UA = 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) lit-review-fetch'
def fetch(name, urls):
    out = os.path.join(P, name + '.pdf')
    if os.path.exists(out):
        print('exists', name); return
    for u in urls:
        tmp = os.path.join('dl', name + '.part')
        os.makedirs('dl', exist_ok=True)
        r = subprocess.run(['curl', '-sL', '--max-time', '90', '-A', UA, '-o', tmp, '-w', '%{http_code} %{content_type} %{url_effective}', u], capture_output=True, text=True)
        info = r.stdout.strip()
        head = open(tmp, 'rb').read(8) if os.path.exists(tmp) else b''
        size = os.path.getsize(tmp) if os.path.exists(tmp) else 0
        ok = head.startswith(b'%PDF')
        with open(LOG, 'a', encoding='utf-8') as f:
            f.write(f"{datetime.date.today()}\t{name}\t{u}\t{info}\t{size}\t{'PDF' if ok else 'NOT-PDF:' + repr(head)}\n")
        print(name, u, info, size, 'PDF' if ok else repr(head))
        if ok:
            os.replace(tmp, out); return
        else:
            # keep a small sample of what came back for the gate record
            if os.path.exists(tmp):
                os.replace(tmp, os.path.join('dl', name + '.rejected'))
    print('FAILED', name)
if __name__ == '__main__':
    name = sys.argv[1]; fetch(name, sys.argv[2:])
