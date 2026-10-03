"""Compare Unity's imported waveform against the original chart decoder PCM.

Run AudioAlignment.DumpImportedAudio in Unity first. Requires numpy/scipy and
soundfile (the same decoder used to analyze the HTML soundtrack).
"""
import argparse
import json
from pathlib import Path
import sys

cached = Path.home() / '.cache/breath-audio-tools'
if cached.exists():
    sys.path.insert(0, str(cached))
import numpy as np
import soundfile as sf
from scipy.signal import correlate, correlation_lags

parser = argparse.ArgumentParser()
parser.add_argument('--project', type=Path, required=True)
parser.add_argument('--dump', type=Path, required=True)
parser.add_argument('--output', type=Path, required=True)
parser.add_argument('--require-aligned', action='store_true')
args = parser.parse_args()
chart = json.loads((args.project / 'Assets/Resources/Data/chart.json').read_text(encoding='utf-8-sig'))
results = []
for track in chart['tracks']:
    meta = json.loads((args.dump / (track['id'] + '.json')).read_text())
    imported = np.fromfile(args.dump / (track['id'] + '.f32'), dtype='<f4').reshape(-1, meta['channels'])
    original = args.project / 'SourceAudio' / track['src'].removeprefix('assets/')
    with sf.SoundFile(original) as source:
        rate = source.samplerate
        reference = source.read(frames=rate * 10, dtype='float32', always_2d=True)
        frames = source.frames
    assert rate == meta['sampleRate'] and reference.shape[1] == imported.shape[1]
    # Interior four-second segment, excluding possible intro silence. Full
    # correlation is narrowed to +/- 200 ms, independently of known MP3 delay.
    a = imported[rate:rate*5].mean(axis=1).astype('float64')
    b = reference[rate:rate*5].mean(axis=1).astype('float64')
    corr = correlate(a, b, method='fft')
    lags = correlation_lags(len(a), len(b))
    window = np.abs(lags) <= round(rate*.2)
    lag = int(lags[window][np.argmax(corr[window])])
    if lag > 0:
        aligned_a, aligned_b = a[lag:], b[:-lag]
    elif lag < 0:
        aligned_a, aligned_b = a[:lag], b[-lag:]
    else:
        aligned_a, aligned_b = a, b
    result = dict(track=track['id'], sampleRate=rate,
                  importedFrames=meta['samples'], referenceFrames=frames,
                  lengthDifferenceSamples=meta['samples']-frames,
                  lagSamples=lag, lagMs=lag/rate*1000,
                  waveformCorrelation=float(np.corrcoef(aligned_a, aligned_b)[0,1]),
                  aligned=abs(lag)<=1 and abs(meta['samples']-frames)<=1)
    results.append(result)
report = dict(success=all(r['aligned'] for r in results), tracks=results,
              method='Unity AudioClip.GetData vs source SoundFile float32; positive lag means Unity waveform is late')
args.output.parent.mkdir(parents=True, exist_ok=True)
args.output.write_text(json.dumps(report, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
print(json.dumps(report, ensure_ascii=False, indent=2))
if args.require_aligned and not report['success']:
    sys.exit(1)
