"""Audit all V13 sound assets and onset/normalization metadata against Unity."""
import argparse
import hashlib
import json
from pathlib import Path
import re

p=argparse.ArgumentParser()
p.add_argument('--html', type=Path, required=True)
p.add_argument('--project', type=Path, required=True)
a=p.parse_args()
manifest=json.loads((a.html/'assets/scenes/audio-manifest.json').read_text(encoding='utf-8-sig'))
code=(a.project/'Assets/Scripts/Runtime/GuguAudio.cs').read_text(encoding='utf-8-sig')
metadata={k:(float(l),float(peak)) for k,l,peak in re.findall(r'\{"([^"]+)",new SampleInfo\(([\d.]+)f,([\d.]+)f\)\}',code)}
records=[]
for key,data in manifest.items():
    if key=='menu':
        continue # Both V13 and Unity use the selected track as menu music.
    original=a.html/'assets/scenes'/data['file']
    backup=a.project/'SourceAudio/scenes'/data['file']
    wav=a.project/'Assets/Resources/Audio/scenes'/Path(data['file']).with_suffix('.wav')
    original_hash=hashlib.sha256(original.read_bytes()).hexdigest()
    backup_hash=hashlib.sha256(backup.read_bytes()).hexdigest()
    assert original_hash==backup_hash==data['sha256'],key+' source audio changed'
    assert wav.is_file() and metadata[key]==(data['lead'],data['peak']),key+' missing or changed import metadata'
    records.append(dict(key=key,source=data['source'],sourceSha256=original_hash,lead=data['lead'],peak=data['peak'],present=True))
assert len(records)==16 and len(metadata)==16
report=dict(success=True,effects=records,menu='Selected song in both V13 and Unity',
            intentionalChange='Eat feedback moves from successful fish hit to every active lane press; 300ms, volume .28, no input cooldown, no duplicate on hit.')
(a.project/'Validation/html-audio-assets.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps(dict(success=True,effects=len(records),allSourceHashesMatch=True,allOnsetAndPeakMetadataMatch=True)))
