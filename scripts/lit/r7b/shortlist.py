import json, urllib.request, urllib.parse, sys
sys.path.insert(0, '.')
from oa import get, log
ids = """W2150910103 W2060838974 W2099709825 W2396556143 W2781162795 W2008090220 W1974155027 W2128018657 W2099924544 W2046605554 W2128532168 W2112578130 W1991847733
W2166878789 W2055844092 W2134177354 W2113568392 W2042418243
W2005820963 W1978186124 W2139122922 W2137920389 W1972478018 W2628119471 W2891328915 W3005451904
W2115616265 W2127517246 W2071829276 W2037704886 W2151846090 W2067049032
W2345898870 W2788971066 W2133613966
W2105447517 W2122131085 W2127310700 W2137464715 W2003421447
W2079643039 W2114878190 W2582593439 W2105073200 W2089582139 W2279267934 W1489297152
W2170946334 W2259993563 W2077769416 W2012078045 W2095172582 W2112445376""".split()
out = []
for i in range(0, len(ids), 45):
    chunk = ids[i:i+45]
    url = 'https://api.openalex.org/works?' + urllib.parse.urlencode({'filter': 'openalex_id:' + '|'.join(chunk), 'per-page': 50})
    d = get(url); log('OpenAlex', '', 'openalex_id:<shortlist chunk %d>' % i, d['meta']['count'], 'shortlist')
    out += d['results']
json.dump(out, open('resp/shortlist.json', 'w', encoding='utf-8'))
for w in out:
    b = w.get('best_oa_location') or {}
    locs = [l.get('pdf_url') for l in (w.get('locations') or []) if l.get('pdf_url')]
    src = ((w.get('primary_location') or {}).get('source') or {}).get('display_name')
    au = [a['author']['display_name'] for a in w.get('authorships', [])[:3]]
    print(f"{w['id'].split('/')[-1]}|{w['publication_year']}|{'; '.join(au)}|{w['title'][:80]}|{src}|{w['biblio']}|cites={w['cited_by_count']}|retr={w['is_retracted']}|doi={w['doi']}|oa={w['open_access']['oa_status']}|pdfs={locs[:3]}")
