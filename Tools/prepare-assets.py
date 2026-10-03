"""Copy only edition 13's assets into the native Unity project.

Usage: python Tools/prepare-assets.py --source ../rintrint.github.io/gamejam
WebP is losslessly decoded to RGBA PNG; source atlas dimensions/UVs are unchanged.
No resampling, recolouring or chart retiming is performed. Gapless MP3 decoding
uses the same libsndfile/SoundFile pipeline as the source chart analysis. Unity
imports float32 WAV instead of adding MP3 encoder delay/padding a second time.
"""
from pathlib import Path
import argparse
import hashlib
import json
import os
import shutil
import sys
from PIL import Image

ART = [
    'tide/ocean.png', 'tide/seal.png', 'tide/props.png',
    'scenes/hunger-strip.png', 'duet/effects-atlas.png', 'duet/seal-eating.png',
    'drift/swim.webp', 'drift/fisher.webp', 'drift/splash.webp', 'drift/angel.webp',
    'drift/inhale-atlas.png', 'floe/menu-ocean.png',
    'floe/swim-thin.png', 'floe/swim-medium.webp', 'floe/swim-fat.webp',
    'floe/Start-Botton.webp', 'floe/Start-Botton-Click.webp',
    'floe/Credits-Botton.webp', 'floe/Credits-Botton-Click.webp',
    'floe/Exit-Botton.webp', 'floe/Exit-Botton-Click.webp',
    'floe/Home-Botton.webp', 'floe/Back-Botton.webp',
    'gugu/friend-outlined.png', 'gugu/breath-crisis.png', 'gugu/breathless-expressions.png',
]
DATA = {
    'floe/chart.json': 'chart.json',
    'floe/swim-forms.json': 'swim-forms.json',
    'scenes/audio-manifest.json': 'audio-manifest.json',
    'drift/art.json': 'art-drift.json', 'gugu/art.json': 'art-gugu.json',
    'drift/audio-edit.json': 'audio-edit.json',
}
PROVENANCE = [
    'tide/prompts.json', 'scenes/art-provenance.json', 'duet/art-provenance.json',
    'drift/inhale-provenance.json', 'floe/menu-provenance.json',
    'gugu/expression-provenance.md', 'gugu/art-provenance.md', 'gugu/crisis-provenance.md',
]

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--source', type=Path, required=True)
    parser.add_argument('--project', type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument('--audio-tools', type=Path, default=Path(os.environ.get('BREATH_MEDIA_TOOLS', Path.home() / '.cache/breath-audio-tools')))
    args = parser.parse_args()
    if args.audio_tools.exists():
        sys.path.insert(0, str(args.audio_tools))
    import numpy as np
    import soundfile as sf
    source = args.source.resolve() / 'assets'
    project = args.project.resolve()
    destination = project / 'Assets' / 'Resources'
    inventory = []
    for relative in ART:
        src = source / relative
        dest = destination / 'Art' / Path(relative).with_suffix('.png')
        dest.parent.mkdir(parents=True, exist_ok=True)
        if src.suffix.lower() == '.webp':
            with Image.open(src) as im:
                im.convert('RGBA').save(dest, optimize=True)
            with Image.open(src) as original, Image.open(dest) as decoded:
                assert original.convert('RGBA').tobytes() == decoded.convert('RGBA').tobytes(), src
        else:
            shutil.copy2(src, dest)
        with Image.open(dest) as im:
            dimensions = list(im.size)
        inventory.append(dict(source='assets/' + relative, resource='Art/' + str(Path(relative).with_suffix('')).replace('\\', '/'),
                              sourceSha256=sha(src), unitySha256=sha(dest), dimensions=dimensions))
    tracks = json.loads((source / 'floe/chart.json').read_text(encoding='utf-8'))['tracks']
    samples = json.loads((source / 'scenes/audio-manifest.json').read_text(encoding='utf-8'))
    audio = [track['src'].removeprefix('assets/') for track in tracks]
    audio += ['scenes/' + sample['file'] for sample in samples.values()]
    track_by_path = {track['src'].removeprefix('assets/'): track for track in tracks}
    audio_root = (destination / 'Audio').resolve()
    removals, audio_report, archived = [], [], []
    def remove_obsolete(obsolete):
        resolved = obsolete.resolve()
        assert resolved.is_relative_to(audio_root), f'Unsafe cleanup path: {resolved}'
        if obsolete.exists():
            assert obsolete.is_file(), f'Unexpected non-file import: {obsolete}'
            obsolete.unlink()
        removals.append(str(obsolete.relative_to(project)).replace('\\', '/'))
    for relative in audio:
        src = source / relative
        backup = project / 'SourceAudio' / relative
        backup.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(src, backup)
        assert sha(src) == sha(backup), src
        if relative == 'scenes/audio/menu.mp3':
            # V13 PulseAudio.menu() and native GuguAudio.Menu() both loop the
            # selected track. This older scene-menu recording is archival only.
            entry = dict(source='assets/' + relative, sourceSha256=sha(src),
                         sourceBackup=str(backup.relative_to(project)).replace('\\', '/'),
                         archivedOnly=True, reason='V13 menu plays the selected song; legacy menu recording is unused')
            archived.append(entry)
            for obsolete in (audio_root / relative, audio_root / (relative + '.meta'),
                             audio_root / Path(relative).with_suffix('.wav'), audio_root / Path(relative).with_suffix('.wav.meta')):
                remove_obsolete(obsolete)
            continue
        dest = audio_root / Path(relative).with_suffix('.wav')
        dest.parent.mkdir(parents=True, exist_ok=True)
        original, rate = sf.read(src, dtype='float32', always_2d=True)
        sf.write(dest, original, rate, format='WAV', subtype='FLOAT')
        decoded, decoded_rate = sf.read(dest, dtype='float32', always_2d=True)
        assert rate == decoded_rate and np.array_equal(original, decoded), f'PCM mismatch: {src}'
        pcm_hash = hashlib.sha256(original.astype('<f4', copy=False).tobytes()).hexdigest()
        frames, channels = original.shape
        info = dict(source='assets/' + relative, frames=frames, channels=channels, sampleRate=rate,
                    duration=frames / rate, pcmSha256=pcm_hash, wavFloat32Exact=True)
        if relative in track_by_path:
            track = track_by_path[relative]
            difference = abs(frames - track['duration'] * rate)
            assert difference <= 1.000001, f'Chart/audio length mismatch: {relative}: {difference} samples'
            info.update(trackId=track['id'], chartDuration=track['duration'], chartDifferenceSamples=difference)
        audio_report.append(info)
        inventory.append(dict(source='assets/' + relative, resource='Audio/' + str(Path(relative).with_suffix('')).replace('\\', '/'),
                              sourceSha256=sha(src), sourceBackup=str(backup.relative_to(project)).replace('\\', '/'),
                              unitySha256=sha(dest), frames=frames, channels=channels, sampleRate=rate,
                              pcmSha256=pcm_hash, conversion='gapless float32 PCM; sample-for-sample equality verified'))
        # Only remove the obsolete import after its exact source is safely backed up
        # and the replacement WAV has passed PCM and chart-duration validation.
        for obsolete in (audio_root / relative, audio_root / (relative + '.meta')):
            remove_obsolete(obsolete)
    for relative, name in DATA.items():
        dest = destination / 'Data' / name
        dest.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source / relative, dest)
    for relative in PROVENANCE:
        dest = args.project / 'Documentation' / 'AssetProvenance' / relative
        dest.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source / relative, dest)
    manifest = destination / 'Data' / 'asset-inventory.json'
    manifest.write_text(json.dumps(dict(sourceEdition=13, assets=inventory, archivedOnly=archived), ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    validation = project / 'Validation'
    validation.mkdir(parents=True, exist_ok=True)
    (validation / 'audio-pcm.json').write_text(json.dumps(dict(success=True, decoder='SoundFile ' + sf.__version__ + '; libsndfile ' + sf.__libsndfile_version__,
        files=audio_report, archivedOnly=archived, obsoleteUnityImports=removals), ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(f'Prepared {len(ART)} images, {len(audio_report)} gapless float32 WAVs, {len(archived)} archived-only original, {len(DATA)} manifests. Verified all RGBA pixels, source hashes and PCM samples.')
    for index, track in enumerate(tracks):
        print(f'Song {index + 1}: {track["id"]}, {track["duration"]:.6f}s, {track["src"]}')

if __name__ == '__main__':
    main()
