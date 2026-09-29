import sys, json, urllib.request, urllib.parse, time, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from oa import log
D = os.path.dirname(os.path.abspath(__file__))
def get(url):
    for i in range(8):
        try:
            req = urllib.request.Request(url, headers={'User-Agent': 'lit-review-script/1.0'})
            with urllib.request.urlopen(req, timeout=60) as r:
                return json.loads(r.read().decode('utf-8'))
        except urllib.error.HTTPError as e:
            if e.code == 429:
                time.sleep(6 * (i + 1)); continue
            raise
    raise RuntimeError('rate limited')
F = 'title,year,venue,citationCount,influentialCitationCount,externalIds,openAccessPdf,publicationTypes'
def s2(q, tag, limit=20, extra=''):
    url = 'https://api.semanticscholar.org/graph/v1/paper/search?' + urllib.parse.urlencode({'query': q, 'limit': limit, 'fields': F}) + extra
    time.sleep(3)
    d = get(url)
    log('SemanticScholar', q, f"limit={limit}{extra}", d.get('total'), tag)
    json.dump(d, open(os.path.join(D, 'resp', f's2-{tag}.json'), 'w', encoding='utf-8'))
    print(f"## {tag}: '{q}' -> total {d.get('total')}")
    for p in d.get('data', []):
        doi = (p.get('externalIds') or {}).get('DOI')
        oa = (p.get('openAccessPdf') or {}).get('url')
        print(f"{p.get('year')}|{p.get('citationCount')}/{p.get('influentialCitationCount')}|{(p.get('title') or '')[:110]}|{p.get('venue')}|{doi}|{'OA:'+oa if oa else '-'}")
if __name__ == '__main__':
    s2(sys.argv[1], sys.argv[2], int(sys.argv[3]) if len(sys.argv) > 3 else 20, sys.argv[4] if len(sys.argv) > 4 else '')
