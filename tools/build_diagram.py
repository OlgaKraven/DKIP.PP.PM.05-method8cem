from pathlib import Path
import re,xml.etree.ElementTree as E
ROOT=Path(__file__).resolve().parents[1]
tables={}
for line in (ROOT/'demo/schema.sql').read_text(encoding='utf-8').splitlines():
 m=re.match(r'CREATE TABLE IF NOT EXISTS (\w+)\((.*)\);',line);assert m,line
 name,body=m.groups();parts=re.split(r',(?=[a-zA-Z_])',body)
 fields=[p.split(' CHECK')[0].split(' DEFAULT')[0] for p in parts if not p.startswith(('PRIMARY','FOREIGN','UNIQUE','CHECK'))]
 fields=[p.replace('PRIMARY KEY','PK').replace('AUTO_INCREMENT','AI').replace('NOT NULL','').strip() for p in fields]
 fk=re.findall(r'FOREIGN KEY\(([^)]+)\) REFERENCES (\w+)\(([^)]+)\)',body)
 pk=re.findall(r'PRIMARY KEY\(([^)]+)\)',body)
 tables[name]=(fields,fk,pk)
mx=E.Element('mxfile',host='app.diagrams.net')
groups=[('Заказы',['customers','orders','order_lines','products','production_orders']),('Спецификация',['products','material_norms','materials','operation_norms','operations']),('Доступ и аудит',['users','orders','order_audit'])]
for page,names in groups:
 d=E.SubElement(mx,'diagram',name=page);model=E.SubElement(d,'mxGraphModel',dx='1000',dy='1200',page='1',pageScale='1',pageWidth='827',pageHeight='1169');r=E.SubElement(model,'root')
 E.SubElement(r,'mxCell',id='0');E.SubElement(r,'mxCell',id='1',parent='0')
 layouts={
 'Заказы':{'customers':(40,60),'orders':(430,60),'products':(40,430),'order_lines':(430,430),'production_orders':(430,800)},
 'Спецификация':{'products':(235,50),'material_norms':(40,410),'operation_norms':(430,410),'materials':(40,800),'operations':(430,800)},
 'Доступ и аудит':{'users':(40,60),'orders':(430,60),'order_audit':(40,450)}}
 for i,name in enumerate(names):
  fields,fks,pks=tables[name];label=name+'\n'+'\n'.join(fields)
  if pks:label+='\nPK ('+pks[0]+')'
  for col,target,targetcol in fks:label+='\nFK '+col+' → '+target+'.'+targetcol
  if name=='order_audit':label+='\norder_id: исторический код, без FK'
  c=E.SubElement(r,'mxCell',id=name,value=label,style='rounded=0;whiteSpace=wrap;html=0;align=left;verticalAlign=top;spacing=12;fontFamily=Arial;fontSize=13;fillColor=#ffffff;strokeColor=#1c1c1c;',vertex='1',parent='1')
  x,y=layouts[page][name]
  E.SubElement(c,'mxGeometry',x=str(x),y=str(y),width='310',height='250' if name!='users' else '280',**{'as':'geometry'})
 for name in names:
  for col,target,targetcol in tables[name][1]:
   if target not in names:continue
   sx,sy=layouts[page][target];tx,ty=layouts[page][name]
   anchors='exitX=1;exitY=0.7;entryX=0;entryY=0.7;' if sy==ty and sx<tx else 'exitX=0;exitY=0.7;entryX=1;entryY=0.7;' if sy==ty else 'exitX=0.5;exitY=1;entryX=0.5;entryY=0;' if sy<ty else 'exitX=0.5;exitY=0;entryX=0.5;entryY=1;'
   value='1 : 0..1' if name=='production_orders' else '1 : N · '+col
   if name=='production_orders':anchors='exitX=1;exitY=0.7;entryX=1;entryY=0.6;'
   c=E.SubElement(r,'mxCell',id=name+'_'+col,value=value,style='edgeStyle=orthogonalEdgeStyle;rounded=0;html=0;endArrow=none;fontSize=12;labelBackgroundColor=#ffffff;'+anchors,edge='1',parent='1',source=target,target=name)
   geo=E.SubElement(c,'mxGeometry',relative='1',**{'as':'geometry'})
   if name=='production_orders':
    arr=E.SubElement(geo,'Array',**{'as':'points'});E.SubElement(arr,'mxPoint',x='775',y='235');E.SubElement(arr,'mxPoint',x='775',y='950')
E.indent(mx)
E.ElementTree(mx).write(ROOT/'demo/ER.drawio',encoding='utf-8',xml_declaration=True)
print('Built three-page editable model with all actual schema fields, keys and relationships')
