from pathlib import Path
from zipfile import ZipFile,ZIP_DEFLATED
import shutil
root=Path(__file__).resolve().parents[1]
with ZipFile(root/'downloads/tests.zip','w',ZIP_DEFLATED) as z:
 for f in ['Verifier.csproj','Program.cs']:z.write(root/'test-project'/f,'tests/'+f)
for folder in [root/'demo']:
 (folder/'tests').mkdir(exist_ok=True)
 for f in ['Verifier.csproj','Program.cs']:shutil.copyfile(root/'test-project'/f,folder/'tests'/f)
print('Packaged a separate .NET test project')
