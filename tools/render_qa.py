from pathlib import Path
import fitz,json
from PIL import Image,ImageOps,ImageDraw
ROOT=Path(__file__).resolve().parents[1];out=ROOT/'output/render';out.mkdir(exist_ok=True)
stats=[]
for name,path in [('method',ROOT/'downloads/assignment.pdf'),('areas',ROOT/'downloads/guide.pdf'),('report',ROOT/'output/report.pdf')]:
 doc=fitz.open(path);rows=[];folder=out/name;folder.mkdir(exist_ok=True)
 for i,p in enumerate(doc):
  pix=p.get_pixmap(matrix=fitz.Matrix(1.35,1.35),alpha=False);pix.save(folder/f'{i+1:03d}.png')
  text=p.get_text();assert '\ufffd' not in text
  for word in p.get_text('words'):
   assert word[0]>=75 and word[2]<=542,(name,i,word)
  rows.append({'page':i+1,'chars':len(text),'links':len(p.get_links())})
 for first in range(0,len(doc),6):
  board=Image.new('RGB',(1200,1700),'#dfe1e5');dr=ImageDraw.Draw(board)
  for j in range(6):
   if first+j>=len(doc):break
   im=Image.open(folder/f'{first+j+1:03d}.png');im.thumbnail((390,800))
   x=(j%3)*400;y=(j//3)*850;board.paste(im,(x,y+25));dr.text((x+10,y+5),f'{name} {first+j+1}',fill='black')
  board.save(out/f'{name}-{first+1:03d}.jpg')
 stats.append({'document':name,'pages':len(doc),'page_info':rows})
(ROOT/'output/pdf-stats.json').write_text(json.dumps(stats,indent=2),encoding='utf-8')
print([(x['document'],x['pages']) for x in stats])
