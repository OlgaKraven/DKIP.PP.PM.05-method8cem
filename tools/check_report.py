from pathlib import Path
from lxml import etree
from docx import Document
from docx.oxml.ns import qn
from docx.shared import Mm
import zipfile,json,hashlib,shutil
ROOT=Path(__file__).resolve().parents[1];source=ROOT/'downloads/Отчет о выполнении.docx'
ns={'w':'http://schemas.openxmlformats.org/wordprocessingml/2006/main'}
with zipfile.ZipFile(source) as z:x=etree.fromstring(z.read('word/document.xml'))
t=' '.join(x.xpath('//w:t/text()',namespaces=ns));assert all(s not in t.lower() for s in ['содержание','заключение','список источников','ревьюирование','пм.03'])
assert 'Ссылка на репозиторий проекта' in t
assert set(x.xpath('//w:color/@w:val',namespaces=ns))<={'000000','auto'}
tags=x.xpath('//w:sdtPr/w:tag/@w:val',namespaces=ns);figs=[s for s in tags if s.startswith('pp05_figure_')];assert len(figs)==12
ids=x.xpath('//w:sdtPr/w:id/@w:val',namespaces=ns);assert len(ids)==len(set(ids))
d=Document(source);assert len([p for p in d.paragraphs if p.style.name=='Heading 1'])==3
assert [len(t.rows) for t in d.tables if len(t.columns)==7]==[27]
for s in d.sections:
 assert abs(s.left_margin.mm-30)<.1 and abs(s.right_margin.mm-20)<.1
# Private test copy never enters any downloads or published package.
for c in d.element.xpath('.//w:sdt'):
 tag=c.find('w:sdtPr/w:tag',c.nsmap)
 if tag is not None and not tag.get(qn('w:val')).startswith('pp05_figure_'):
  ts=list(c.find(qn('w:sdtContent')).iter(qn('w:t')))
  if ts:
   ts[0].text='Проверочное заполнение для проверки переноса развёрнутого текста. '*5
   for s in ts[1:]:s.text=''
figure=d.element.xpath('.//w:sdt[w:sdtPr/w:tag[@w:val="pp05_figure_1"]]')[0]
p=d.add_paragraph();p.add_run().add_picture(str(ROOT/'assets/screenshots/puzzle.png'),width=Mm(150))
content=figure.find(qn('w:sdtContent'))
for e in list(content):content.remove(e)
for e in list(p._p):
 if e.tag==qn('w:r'):p._p.remove(e);content.append(e)
p._p.getparent().remove(p._p)
d.save(ROOT/'output/report-filled-private.docx')
master=ROOT.parents[1]/'BrainM/Навыки/practice-methodist/assets/Отчет о выполнении.docx';assert hashlib.sha256(master.read_bytes()).hexdigest()=='5b91286458395ae589ded859021f287ce50cca1148cc5b29226b6fb1cb5373c4'
shutil.copyfile(source,ROOT/'forms/Отчет о выполнении.docx')
r=dict(figure_slots=12,checks_rows=26,main_sections=3,clean=True,master_unchanged=True,sha256=hashlib.sha256(source.read_bytes()).hexdigest())
(ROOT/'output/report-check.json').write_text(json.dumps(r,indent=2),encoding='utf-8');print(json.dumps(r))
