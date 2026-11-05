from pathlib import Path
import json,shutil
ROOT=Path(__file__).resolve().parents[1]
BASE='https://raw.githubusercontent.com/OlgaKraven/DKIP.PP.PM.05-method8cem/main/downloads/'
main='# Производственная практика ПМ.05\n\nПроектирование и разработка информационных систем\n\n'
main+='- [Задание и методические рекомендации]('+BASE+'assignment.pdf)\n- [Список предметных областей]('+BASE+'guide.pdf)\n- [Отчет о выполнении.docx]('+BASE+'Отчет%20о%20выполнении.docx)\n- [Тестовый проект]('+BASE+'tests.zip)\n- [Учебный образец]('+BASE+'demo.zip)\n- [Исходные производственные документы]('+BASE+'source.zip)\n'
(ROOT/'README.md').write_text(main,encoding='utf-8');(ROOT/'docs/index.md').write_text(main,encoding='utf-8')
(ROOT/'docs/examples').mkdir(exist_ok=True)
for i in range(1,5):
 p=ROOT/'docs/practice'/f'stage{i}.md';dst=ROOT/'docs/examples'/f'helpdesk{i}.md'
 if not dst.exists():dst.write_text('> Дополнительное чтение. Helpdesk показывает отдельные приёмы и не является обязательной второй работой ПП ПМ.05.\n\n'+p.read_text(encoding='utf-8'),encoding='utf-8')
 p.write_text('# Выполнение производственной практики\n\n[Полное задание и последовательный разбор]('+BASE+'assignment.pdf).\n\nОдна информационная система своего варианта: ER, производственные нормы, стоимость заказа, доступ и управление пользователями.\n',encoding='utf-8')
(ROOT/'docs/subject_areas.md').write_text((ROOT/'content/areas.md').read_text(encoding='utf-8'),encoding='utf-8')
config=(ROOT/'mkdocs.yml').read_text(encoding='utf-8').replace('ПП.ПМ.05 Соадминистрирование и автоматизация БД и серверов','ПП ПМ.05 Проектирование и разработка информационных систем')
start=config.index('  - Практика:') if '  - Практика:' in config else config.index('  - Выполнение:');end=config.index('\nmarkdown_extensions:',start)
config=config[:start]+'''  - Выполнение: practice/stage1.md
  - Дополнительные примеры Helpdesk:
      - Модель: examples/helpdesk1.md
      - Объекты БД: examples/helpdesk2.md
      - Хеширование и роли: examples/helpdesk3.md
      - CRUD и соединения: examples/helpdesk4.md
'''+config[end:]
(ROOT/'mkdocs.yml').write_text(config,encoding='utf-8')
# Keep the exact heading used by EatDoners to locate the figure section.
from zipfile import ZipFile,ZIP_DEFLATED
from lxml import etree
src=ROOT/'forms/source/приложение1.pptx';out=ROOT/'forms/Приложение к отчету.pptx'
with ZipFile(src) as z:parts={n:z.read(n) for n in z.namelist()}
key='ppt/slides/slide4.xml';e=etree.fromstring(parts[key]);ns={'a':'http://schemas.openxmlformats.org/drawingml/2006/main'}
nodes=e.xpath('//a:t[text()="Экспериментально-практическая работа"]',namespaces=ns);assert len(nodes)==1
run=nodes[0].getparent();rpr=run.find('{'+ns['a']+'}rPr');assert rpr is not None;rpr.set('sz','2800')
parts[key]=etree.tostring(e,xml_declaration=True,encoding='UTF-8',standalone=True)
with ZipFile(out,'w',ZIP_DEFLATED) as z:
 for n,data in parts.items():z.writestr(n,data)
with ZipFile(ROOT/'output/templates-update.zip','w',ZIP_DEFLATED) as z:z.write(out,'Приложение к отчету.pptx')
print('Updated current entries; preserved optional Helpdesk, production forms and the exact figure heading with readable size')
