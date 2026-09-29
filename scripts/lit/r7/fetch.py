import sys,urllib.request,os,time,fitz,json,datetime
name,url,kw=sys.argv[1],sys.argv[2],sys.argv[3].lower().split('|')
dst=os.path.join('dl',name+'.pdf')
hdr={'User-Agent':'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0 Safari/537.36','Accept':'application/pdf,*/*'}
status=''
try:
    r=urllib.request.urlopen(urllib.request.Request(url,headers=hdr),timeout=90)
    data=r.read(); final=r.geturl()
except urllib.error.HTTPError as e:
    b=e.read()[:3000].decode('utf-8','replace').lower(); data=b''
    status=('GATED(cloudflare challenge) ' if ('just a moment' in b or 'cf-chl' in b) else 'HTTP-ERR ')+str(e.code); final=url
except Exception as e:
    data=b''; status='HTTP-ERR '+str(e)[:80]; final=url
if data[:4]==b'%PDF':
    open(dst,'wb').write(data)
    try:
        doc=fitz.open(dst); t=' '.join(doc[i].get_text() for i in range(min(2,doc.page_count))).lower().replace('\n',' ')
        hits=[k for k in kw if k in t]
        status=f'PDF pages={doc.page_count} kwhits={len(hits)}/{len(kw)}'
        print(status); print('  P1:',t[:300])
    except Exception as e: status='PDF-UNREADABLE '+str(e)
else:
    s=data[:3000].decode('utf-8','replace').lower()
    gate=('cloudflare' in status.lower()) or ('403' in status) or any(g in s for g in ['just a moment','captcha','cf-chl','challenge','proof-of-work','pow','enable javascript'])
    status = status or ('GATED/CHALLENGE' if gate else 'NOT-PDF (%s bytes, starts %r)'%(len(data),data[:60]))
    print(status)
open('retrieval-log.tsv','a',encoding='utf-8').write('\t'.join([datetime.datetime.now().isoformat(timespec='seconds'),name,url,final,status])+'\n')
