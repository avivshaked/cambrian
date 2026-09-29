import sys, json, urllib.request, urllib.parse, time, os, datetime, hashlib
LOG = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'search-log.tsv')
def get(url, tries=5):
    for i in range(tries):
        try:
            req = urllib.request.Request(url, headers={'User-Agent': 'lit-review-script/1.0'})
            with urllib.request.urlopen(req, timeout=60) as r:
                return json.loads(r.read().decode('utf-8'))
        except urllib.error.HTTPError as e:
            if e.code == 429:
                time.sleep(5 * (i + 1)); continue
            raise
    raise RuntimeError('rate limited')
def log(db, q, filt, n, tag):
    new = not os.path.exists(LOG)
    with open(LOG, 'a', encoding='utf-8') as f:
        if new: f.write('date\tdb\ttag\tquery\tfilters\tcount\n')
        f.write(f"{datetime.date.today()}\t{db}\t{tag}\t{q}\t{filt}\t{n}\n")
def oa(q, filt='', sort='cited_by_count:desc', per=25, tag=''):
    params = {'per-page': per}
    if q: params['search'] = q
    if filt: params['filter'] = filt
    if sort: params['sort'] = sort
    url = 'https://api.openalex.org/works?' + urllib.parse.urlencode(params)
    d = get(url)
    n = d['meta']['count']
    log('OpenAlex', q, f"{filt} sort={sort}", n, tag)
    fn = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'resp', f"oa-{tag}.json")
    os.makedirs(os.path.dirname(fn), exist_ok=True)
    json.dump(d, open(fn, 'w', encoding='utf-8'))
    print(f"## {tag}: '{q}' [{filt}] sort={sort} -> {n}")
    for w in d['results']:
        oa_url = (w.get('best_oa_location') or {}).get('pdf_url')
        venue = ((w.get('primary_location') or {}).get('source') or {}).get('display_name')
        au = (w.get('authorships') or [{}])
        fa = au[0].get('author', {}).get('display_name', '?') if au else '?'
        print(f"{w['publication_year']}|{w['cited_by_count']}|{fa}|{w['title'][:110] if w['title'] else ''}|{venue}|{w.get('doi')}|{w['id'].split('/')[-1]}|{'OA' if oa_url else '-'}|{w.get('type')}|retr={w.get('is_retracted')}")
if __name__ == '__main__':
    q, filt, sort, tag = sys.argv[1], sys.argv[2], sys.argv[3], sys.argv[4]
    per = int(sys.argv[5]) if len(sys.argv) > 5 else 25
    oa(q, filt, sort if sort != '-' else None, per, tag)
