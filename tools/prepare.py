from pathlib import Path
import json, shutil, hashlib, subprocess, zipfile
from openpyxl import load_workbook
ROOT=Path(__file__).resolve().parents[1]
QA=ROOT/'output';QA.mkdir(exist_ok=True)
for d in ['content','downloads','assets/source','assets/fonts','assets/screenshots','demo/wwwroot','forms/template','tools']:(ROOT/d).mkdir(parents=True,exist_ok=True)
old=Path('C:/Users/gvadoskr/Documents/Codex/2026-11-02/add-eatdoners-mcp-server-thread-01a0bff5/work/audit_2027/attachments/isip/БУ')
live=Path('C:/Users/gvadoskr/Yandex.Disk/2026-2027/ЛППР/ДЭ/КОДы - 2027 год/09.02.07 Информационные системы и программирование/Специалист по информационным системам/Прил_ОЗ_КИМ_09.02.07-5-2027.zip')
with zipfile.ZipFile(live) as z:
    for i in z.infolist():
        if i.filename.startswith('БУ/Задание ') and i.filename.endswith('.rar'):
            previous=old.parent/i.filename
            assert previous.read_bytes()==z.read(i),i.filename
source=old/'Задание 1/Прил_1_ОЗ_КИМ_09.02.07-5-2027'
rows={}
for p in source.iterdir():
    if p.suffix not in ['.xlsx','.json']:continue
    shutil.copyfile(p,ROOT/'assets/source'/p.name)
    if p.suffix=='.xlsx':
        wb=load_workbook(p,data_only=False)
        rows[p.name]={s.title:[[str(v) if v is not None else '' for v in r] for r in s.values if any(v is not None for v in r)] for s in wb}
(QA/'source-rows.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2),encoding='utf-8')
security=old/'Задание 4/Прил_4_ОЗ_КИМ_09.02.07-5-2027/Приложение 1.txt'
(QA/'original-security.txt').write_text(security.read_text(encoding='utf-8'),encoding='utf-8')
prev=ROOT.parent/'DKIP.PP.03-method'
for p in (prev/'assets/fonts').iterdir():shutil.copyfile(p,ROOT/'assets/fonts'/p.name)
raw=(prev/'tools/render_approved.py').read_text(encoding='utf-8').replace('UP PM.03','PP PM.05').replace('УП ПМ.03','ПП ПМ.05').replace('ПМ.03','ПМ.05')
(ROOT/'tools/render_approved.py').write_text(raw,encoding='utf-8')
shutil.copyfile(prev/'tools/report_parts.py',ROOT/'tools/report_parts.py')
master=ROOT.parents[1]/'BrainM/Навыки/practice-methodist/assets/Отчет о выполнении.docx'
assert hashlib.sha256(master.read_bytes()).hexdigest()=='5b91286458395ae589ded859021f287ce50cca1148cc5b29226b6fb1cb5373c4'
shutil.copyfile(master,ROOT/'forms/template/Отчет о выполнении.docx')
with zipfile.ZipFile(master) as z:(QA/'master-parts.json').write_text(json.dumps({n:hashlib.sha256(z.read(n)).hexdigest() for n in z.namelist()},indent=2),encoding='utf-8')
print('Current exam archives match retained originals; copied five workbooks, JSON, fonts and unchanged report master')
for name,sheets in rows.items():
 print(name)
 for name,rs in sheets.items():
  for row in rs:print(' | '.join(v for v in row if v))
