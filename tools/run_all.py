from pathlib import Path
import json,subprocess,zipfile,shutil,sys
ROOT=Path(__file__).resolve().parents[1];QA=ROOT/'output/clean';QA.mkdir(parents=True,exist_ok=True)
results=[]
for n in range(1,26):
 p=QA/f'v{n:02d}';p.mkdir(exist_ok=True)
 with zipfile.ZipFile(ROOT/'downloads'/f'v{n:02d}.zip') as z:
  assert z.testzip() is None;z.extractall(p)
 assert json.loads((p/'dataset.json').read_text(encoding='utf-8'))['number']==n
 with zipfile.ZipFile(ROOT/'downloads/tests.zip') as z:z.extractall(p)
 # Apply exactly the worked example implementation in an independent copy, never in the distributed unfinished project.
 for f in ['Users.cs','Calculations.cs']:shutil.copyfile(ROOT/'demo'/f,p/f)
 log=[]
 for command in [['dotnet','run','--','--init'],['dotnet','run','--project','tests/Verifier.csproj']]:
  r=subprocess.run(command,cwd=p,capture_output=True,encoding='utf-8',errors='replace');log.append(r.stdout+r.stderr)
  if r.returncode:raise RuntimeError(f'v{n:02d} '+log[-1])
 (ROOT/'output'/f'v{n:02d}-checks.txt').write_text('\n'.join(log),encoding='utf-8')
 assert 'PASS=21; FAIL=0' in log[-1]
 results.append({'variant':n,'checks':21,'passed':True})
 print(f'v{n:02d}: 21/21',flush=True)
(ROOT/'output/all-checks.json').write_text(json.dumps(results,indent=2),encoding='utf-8')
print('TOTAL: 525 passed checks across 25 clean downloads',flush=True)
