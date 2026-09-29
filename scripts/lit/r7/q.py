import sys, json, urllib.request, urllib.parse, time, hashlib, os, datetime
BASE = os.path.dirname(os.path.abspath(__file__))
LOG = os.path.join(BASE, 'search-log.tsv')
UA = 'Mozilla/5.0 (literature review script)'
def get(url, tries=5):
    for i in range(tries):
        try:
            req = urllib.request.Request(url, headers={'User-Agent': UA})
            with urllib.request.urlopen(req, timeout=60) as r:
                return json.loads(r.read().decode('utf-8'))
        except urllib.error.HTTPError as e:
            if e.code == 429:
                time.sleep(20 * (i + 1)); continue
            raise
    raise RuntimeError('rate limited')
def log(db, q, filt, n, fn):
    with open(LOG, 'a', encoding='utf-8') as f:
        f.write('\t'.join([datetime.date.today().isoformat(), db, q, filt, str(n), fn]) + '\n')
def oa(search, filt='', sort='relevance_score:desc', per=25, tag=None):
    params = {'per-page': str(per), 'sort': sort}
    if search: params['search'] = search
    if filt: params['filter'] = filt
    url = 'https://api.openalex.org/works?' + urllib.parse.urlencode(params)
    d = get(url)
    fn = 'raw/oa-' + hashlib.md5(url.encode()).hexdigest()[:10] + '.json'
    json.dump(d, open(os.path.join(BASE, fn), 'w', encoding='utf-8'))
    n = d['meta']['count']
    log('OpenAlex', search or '', filt + ' sort=' + sort, n, fn)
    print(f'## OA "{search}" [{filt}] sort={sort} -> {n}')
    for w in d['results']:
        doi = (w.get('doi') or '').replace('https://doi.org/', '')
        src = ((w.get('primary_location') or {}).get('source') or {}).get('display_name')
        print(f"  {w['publication_year']} | c={w['cited_by_count']} | {w['title'][:110] if w['title'] else ''} | {src} | {doi} | {w['id'].split('/')[-1]}")
    return d
def s2(query, limit=20, extra=''):
    fields = 'title,year,venue,citationCount,influentialCitationCount,externalIds,openAccessPdf,publicationTypes'
    url = 'https://api.semanticscholar.org/graph/v1/paper/search?' + urllib.parse.urlencode({'query': query, 'limit': str(limit), 'fields': fields}) + extra
    time.sleep(4)
    try:
        d = get(url, tries=4)
    except Exception as e:
        log('SemanticScholar', query, 'limit=%d %s' % (limit, extra), 'FAILED:'+str(e), '')
        print('S2 FAILED', query, e); return None
    fn = 'raw/s2-' + hashlib.md5(url.encode()).hexdigest()[:10] + '.json'
    json.dump(d, open(os.path.join(BASE, fn), 'w', encoding='utf-8'))
    n = d.get('total', 0)
    log('SemanticScholar', query, 'limit=%d %s' % (limit, extra), n, fn)
    print(f'## S2 "{query}" {extra} -> {n}')
    for p in d.get('data', []):
        doi = (p.get('externalIds') or {}).get('DOI')
        print(f"  {p['year']} | c={p['citationCount']} ic={p['influentialCitationCount']} | {p['title'][:110]} | {p['venue']} | {doi} | oa={'Y' if p.get('openAccessPdf') and p['openAccessPdf'].get('url') else 'N'}")
    return d
if __name__ == '__main__':
    kind = sys.argv[1]
    if kind == 'oa':
        oa(sys.argv[2], sys.argv[3] if len(sys.argv) > 3 else '', sys.argv[4] if len(sys.argv) > 4 else 'relevance_score:desc', int(sys.argv[5]) if len(sys.argv) > 5 else 25)
    elif kind == 's2':
        s2(sys.argv[2], int(sys.argv[3]) if len(sys.argv) > 3 else 20, sys.argv[4] if len(sys.argv) > 4 else '')
