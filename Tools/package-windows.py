"""Package the complete non-development player and verify its ZIP and file hashes."""
import argparse
import hashlib
import json
from pathlib import Path
import zipfile

p = argparse.ArgumentParser()
p.add_argument('--project', type=Path, required=True)
args = p.parse_args()
root = args.project.resolve()
build = root / 'Builds/Windows'
for required in ['SealGugu.exe', 'UnityPlayer.dll',
                 'SealGugu_Data/Managed/Assembly-CSharp.dll', 'README.txt']:
    if not (build / required).is_file():
        raise SystemExit('Incomplete Windows build: ' + required)
if not (build / 'MonoBleedingEdge').is_dir():
    raise SystemExit('Missing Mono runtime')
if not any((build/'SealGugu_Data'/name).is_file() for name in ['data.unity3d', 'globalgamemanagers']):
    raise SystemExit('Missing Unity scene/player data (compressed or uncompressed)')
assembly = (build / 'SealGugu_Data/Managed/Assembly-CSharp.dll').read_bytes()
for diagnostic in [b'GuguCapture', b'NativeSoak', b'NativeAudioParity', b'NativeInputParity', b'PreviewScene']:
    if diagnostic in assembly:
        raise SystemExit('Development-only code found in release assembly: ' + diagnostic.decode())
files = sorted(f for f in build.rglob('*') if f.is_file())
archive = root / 'Releases/SealGugu-V14.0.0-Windows-x64.zip'
archive.parent.mkdir(parents=True, exist_ok=True)
with zipfile.ZipFile(archive, 'w', zipfile.ZIP_DEFLATED, compresslevel=6) as z:
    for f in files:
        z.write(f, str(Path('SealGugu-V14.0.0') / f.relative_to(build)))
with zipfile.ZipFile(archive) as z:
    failure = z.testzip()
    if failure:
        raise SystemExit('ZIP CRC failure: ' + failure)
    if len(z.namelist()) != len(files):
        raise SystemExit('ZIP file count mismatch')
report = dict(success=True, version='V14.0.0', platform='Windows x64',
              archive=archive.name, fileCount=len(files), bytes=archive.stat().st_size,
              sha256=hashlib.file_digest(archive.open('rb'), 'sha256').hexdigest(),
              developmentEntryPointsAbsent=True, zipCrcVerified=True)
(root / 'Validation/windows-package.json').write_text(json.dumps(report, indent=2)+'\n', encoding='utf-8')
print(json.dumps(report, indent=2))
