from pathlib import Path
import json,shutil,zipfile,re
from decimal import Decimal,ROUND_HALF_UP
ROOT=Path(__file__).resolve().parents[1]
def write(p,v):p.write_text(json.dumps(v,ensure_ascii=False,indent=2),encoding='utf-8')
titles=[]
for line in (ROOT/'docs/subject_areas.md').read_text(encoding='utf-8').splitlines():
 m=re.match(r'\| (\d+) \| (.*?) \|',line)
 if m:titles.append(m[2])
if not titles:titles=json.loads((ROOT/'content/variants.json').read_text(encoding='utf-8'))
else:write(ROOT/'content/variants.json',titles)
customers=ROOT/'assets/source/Заказчики.json'
source=json.loads(customers.read_text(encoding='utf-8-sig'));assert len({x['id'] for x in source})==len(source)
demo=dict(database='pp05_demo',number=0,title='Стол «Самобранка» · учебный образец',quantity=2,
 products=[dict(id=1,code='НФ-00000006',name='Стол кухонный «Самобранка»'),dict(id=2,code='LAB-02',name='Полка · дополнительное изделие')],
 materials=[dict(id=i+1,name=n,unit=u,price=p) for i,(n,u,p) in enumerate([('Евровинт 6,5х5','тыс. шт',595),('Мебельная деталь 500х800','шт',95),('Мебельная деталь 600х800','шт',140),('Опора','шт',245),('Столешница круглая','шт',3250)])],
 operations=[dict(id=i+1,name=n,rate=r) for i,(n,r) in enumerate([('Сборка модулей',1400),('Распил ДСП, МДФ и листового материала',450),('Упаковка',950)])],
 material_norms=[dict(product=1,material=i+1,norm=n) for i,n in enumerate([.012,2,4,4,1])]+[dict(product=2,material=2,norm=1),dict(product=2,material=3,norm=1)],
 operation_norms=[dict(product=1,operation=i+1,hours=h) for i,h in enumerate([.75,1.5,.5])]+[dict(product=2,operation=2,hours=.25)],
 units='Стоимость материалов: руб за единицу нормы; евровинт — руб/тыс. шт. Все операции: руб/час; норма — часов на одно изделие. Округлить итог заказа до копеек один раз.')
write(ROOT/'demo/dataset.json',demo);shutil.copyfile(customers,ROOT/'demo/customers.json')
def total(d,q=None):
 m=sum(Decimal(str(n['norm']))*Decimal(str(d['materials'][n['material']-1]['price'])) for n in d['material_norms'] if n['product']==1)
 o=sum(Decimal(str(n['hours']))*Decimal(str(d['operations'][n['operation']-1]['rate'])) for n in d['operation_norms'] if n['product']==1)
 return str(((m+o)*(q or d['quantity'])).quantize(Decimal('.01'),rounding=ROUND_HALF_UP))
answers=[dict(n=0,total=total(demo))]
readme='''# Производственные заказы ПМ.05

Учебная база MySQL предоставляется контейнером. Требуются Docker Desktop с запущенным Linux engine и .NET SDK 10. В PowerShell из папки проекта:

```
docker compose up -d --wait
dotnet restore
dotnet run -- --init
dotnet run
```

Открыть http://127.0.0.1:5055 . База — на порту 33405. Учебные записи: admin / Admin-05! и student / Student-05!. Пароли предназначены только для выданной локальной базы.
Инициализация выполняется один раз. Для повторного запуска достаточно docker compose up -d --wait и dotnet run. Сохранённые заказы и блокировки остаются в томе Docker. Остановка: Ctrl+C в терминале приложения, затем docker compose stop. Не удалять том с работой.

В варианте сначала выполнить TODO по методике. Образец в demo.zip показывает законченный приём, а не вторую обязательную работу. В собственном проекте подготовить ER.pdf, привести schema.sql и objects.sql к своей модели, реализовать Calculations.cs и методы Users.cs, выполнить проверки, заполнить отчёт и README. Все данные dataset.json относятся только к этому варианту.

SQL-запрос из файла: dotnet run -- --query checks.sql . Повторный импорт: dotnet run -- --import . Сначала остановить веб-приложение Ctrl+C.
Тестовый проект скачивается отдельно как tests.zip; распаковать папку tests рядом с ProductionLab.csproj. Запуск: dotnet run --project tests/Verifier.csproj . Тесты создают и удаляют свои временные записи qa05_*, не меняют учебные записи admin и student. Запускать на своей локальной учебной базе после реализации TODO.
'''
files=['ProductionLab.csproj','Program.cs','Database.cs','Users.cs','Calculations.cs','schema.sql','objects.sql','compose.yaml','dataset.json','customers.json','wwwroot/index.html','wwwroot/puzzle.svg','.gitignore']
for n,title in enumerate(titles,1):
 d=json.loads(json.dumps(demo));d.update(database=f'pp05_v{n:02d}',number=n,title=f'Вариант {n:02d} · Производственные заказы',quantity=n%4+1,context=title)
 # Teaching values are explicit individual inputs, not purported values of the official example.
 d['products'][0].update(code=f'V{n:02d}-01',name=f'Модуль производственный V{n:02d}')
 d['products'][1].update(code=f'V{n:02d}-02',name=f'Комплект V{n:02d}')
 for i,m in enumerate(d['materials']):m['price']+=n*(i+1)
 d['material_norms'][0]['norm']=round(.012+n*.001,3)
 d['operation_norms'][0]['hours']=round(.75+(n%3)*.1,2)
 for o in d['operations']:o['rate']+=n*10
 path=ROOT/'variants'/f'v{n:02d}';path.mkdir(parents=True,exist_ok=True)
 for f in files:
  dest=path/f;dest.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(ROOT/'demo'/f,dest)
 write(path/'dataset.json',d)
 raw=(path/'compose.yaml').read_text(encoding='utf-8').replace('pp05-demo',f'pp05-v{n:02d}')
 (path/'compose.yaml').write_text(raw,encoding='utf-8')
 for f,methods in [('Calculations.cs',['OrderTotal']),('Users.cs',['Login','Add','Edit'])]:
  text=(path/f).read_text(encoding='utf-8')
  for method in methods:
   pattern=r'(public static [^\n]+\b'+method+r'\([^\n]*\)\s*\{)'
   m=re.search(pattern,text);assert m,(method,f)
   start=m.end();level=1;pos=start
   while level:
    if text[pos]=='{':level+=1
    elif text[pos]=='}':level-=1
    pos+=1
   text=text[:start]+'\n        // TODO: реализовать по разбору методики.\n        throw new NotImplementedException();\n    '+text[pos-1:]
  (path/f).write_text(text,encoding='utf-8')
 (path/'README.md').write_text(readme,encoding='utf-8')
 (path/'checks.sql').write_text('SELECT id,name,material_cost,operation_cost FROM v_product_cost ORDER BY id;',encoding='utf-8')
 # Bootstrap users must be ready before the student implements administrative registration.
 database=(path/'Database.cs').read_text(encoding='utf-8').replace('Users.Add(c,new UserInput("admin","Admin-05!","Учебный администратор","Administrator",false));','SeedAccount(c,"admin","Admin-05!","Учебный администратор","Administrator");').replace('Users.Add(c,new UserInput("student","Student-05!","Учебный пользователь","User",false));','SeedAccount(c,"student","Student-05!","Учебный пользователь","User");')
 database=database.replace('    public static int ImportCustomers(','''    static void SeedAccount(MySqlConnection c,string login,string password,string name,string role)
    {
        Exec(c,"INSERT INTO users(login,password_hash,full_name,role) VALUES(@l,@h,@n,@r)",("@l",login),("@h",Users.Hash(password)),("@n",name),("@r",role));
    }
    public static int ImportCustomers(''')
 (path/'Database.cs').write_text(database,encoding='utf-8')
 answers.append(dict(n=n,total=total(d),changed=total(d,d['quantity']+1),quantity=d['quantity']))
 with zipfile.ZipFile(ROOT/'downloads'/f'v{n:02d}.zip','w',zipfile.ZIP_DEFLATED) as z:
  for f in files+['README.md','checks.sql']:z.write(path/f,f)
(ROOT/'demo/README.md').write_text(readme.replace('В варианте сначала выполнить TODO по методике.','В этом образце методы уже реализованы.'),encoding='utf-8')
with zipfile.ZipFile(ROOT/'downloads/demo.zip','w',zipfile.ZIP_DEFLATED) as z:
 for f in files+['README.md','ER.drawio','ER.pdf']:z.write(ROOT/'demo'/f,f)
write(ROOT/'output/answers.json',answers)
print('Built 25 separate unfinished projects, complete teaching demo; source customers:',len(source),'demo total:',total(demo))
