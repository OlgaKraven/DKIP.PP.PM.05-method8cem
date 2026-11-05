from pathlib import Path
import json,base64,zipfile
from lxml import etree
ROOT=Path(__file__).resolve().parents[1]
data=json.loads((ROOT/'output/forms-read.json').read_text(encoding='utf-8-sig'))
(ROOT/'forms/source').mkdir(exist_ok=True)
for key,a in data['files'].items():
 p=ROOT/'forms/source'/(key+('.pptx' if key=='приложение1' else '.docx'));p.write_bytes(base64.b64decode(a['data']))
 with zipfile.ZipFile(p) as z:
  ts=[]
  for n in z.namelist():
   if n=='word/document.xml' or n.startswith('ppt/slides/slide') and n.endswith('.xml'):
    e=etree.fromstring(z.read(n));ts.append(n+'\n'+' '.join(e.xpath('//*[local-name()="t"]/text()')))
  (ROOT/'output'/(key+'-text.txt')).write_text('\n'.join(ts),encoding='utf-8')
 print(key,p.stat().st_size)
