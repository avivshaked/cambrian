import urllib.request,urllib.parse,json,time,sys,hashlib,datetime
q=sys.argv[1]; extra=sys.argv[2] if len(sys.argv)>2 else ''
url='https://api.semanticscholar.org/graph/v1/paper/search/bulk?'+urllib.parse.urlencode({'query':q,'fields':'title,year,venue,citationCount,externalIds'})+extra
time.sleep(3)
for i in range(4):
    try:
        r=urllib.request.urlopen(urllib.request.Request(url,headers={'User-Agent':'Mozilla/5.0'}),timeout=60); d=json.loads(r.read()); break
    except Exception as e:
        if '429' in str(e): time.sleep(15*(i+1)); continue
        raise
else:
    open('search-log.tsv','a',encoding='utf-8').write('\t'.join([datetime.date.today().isoformat(),'SemanticScholar-bulk',q,extra,'FAILED:429',''])+'\n'); print('FAILED'); sys.exit()
fn='raw/s2b-'+hashlib.md5(url.encode()).hexdigest()[:10]+'.json'; json.dump(d,open(fn,'w'))
open('search-log.tsv','a',encoding='utf-8').write('\t'.join([datetime.date.today().isoformat(),'SemanticScholar-bulk',q,extra+' (sorted locally by citations)',str(d.get('total')),fn])+'\n')
print('## S2bulk',q,extra,'->',d.get('total'))
for p in sorted(d['data'],key=lambda p:-(p.get('citationCount') or 0))[:int(sys.argv[3]) if len(sys.argv)>3 else 20]:
    print(' ',p['year'],p['citationCount'],(p['title'] or '')[:105],'|',p['venue'],'|',(p.get('externalIds') or {}).get('DOI'))
