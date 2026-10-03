# V14.0.0 asset inventory

All game artwork, music, team sound effects and chart timestamps come from the
deployed edition 13 source at web-repository commit
`1665a124f2a60c380989dd53af80a943386e2d7e`.

`Tools/prepare-assets.py` imports only the artwork and sounds used by edition 13.
Run it with `--source /path/to/rintrint.github.io/gamejam` if rebuilding the asset
package from that source snapshot. It requires Python 3, Pillow, NumPy and
SoundFile/libsndfile. It accepts `--audio-tools` or `BREATH_MEDIA_TOOLS` to point
at the source project's cached audio-analysis dependencies. The script
does not resample, redraw, recolour or retime the source assets.

- 26 original-size raster textures are in `Assets/Resources/Art`. WebP files are
  decoded losslessly into RGBA PNG. The pipeline compares every RGBA byte after
  conversion. Atlas and sprite rectangles remain in original image coordinates.
- 3 songs and 17 team audio files are copied byte-for-byte into `SourceAudio`
  outside Unity's imported Assets folder; SHA-256 checks enforce unchanged originals.
  `Assets/Resources/Audio` contains 19 gaplessly decoded 32-bit float WAV files:
  the three songs and 16 used sound effects. The older `scenes/audio/menu.mp3`
  recording is **archived-only** in `SourceAudio`; it is excluded from Unity's
  imported resources and build because both V13 and the native menu loop the
  currently selected song. Its original hash remains in the inventory.
  The decoder is the same SoundFile/libsndfile used for the source chart analysis.
  WAV files are re-read and every PCM sample is compared for exact equality with
  the decoded source. There is no resampling, extra trimming, gain change or downmix.
  This avoids the additional 58.5–64 ms of encoder padding observed when Unity
  imports the three original MP3 files directly.
- The source song chart and art/audio manifests are in `Assets/Resources/Data`.
  `asset-inventory.json` records source and Unity file hashes, resource keys and
  texture dimensions, PCM hashes, channel counts and sample rates. The third song
  is decoded from the already trimmed 190.5-second MP3;
  the accidentally repeated intro has not been reintroduced.
- `Validation/audio-pcm.json` records decoded frame counts and verifies all three
  song durations against the original chart to within one sample. Resource keys
  remain unchanged because Unity resource loading omits file extensions.
- Audio import uses PCM with the original sample rate and no downmix. The game
  uses measured sound-effect lead-in data from `audio-manifest.json`.
  Gapless PCM aligns imported song samples with the original web chart analysis;
  this does **not** assert zero hardware latency. Output devices, audio buffers,
  displays and input devices still have latency. Use the game's retained audio
  calibration and timing-offset controls for the actual device setup.
- Texture import is readable RGBA32, no mipmaps, no resizing, clamp/bilinear,
  uncompressed. Runtime effects keep original atlas geometry and colour data.
- Original artwork provenance notes are retained in `AssetProvenance`.

## Fonts

The Windows build embeds official static Traditional Chinese font files so it
does not depend on fonts installed on a player's computer:

- `Fonts/NotoSansTC.otf` is unmodified **Noto Sans CJK TC Regular** from
  <https://github.com/notofonts/noto-cjk/blob/main/Sans/OTF/TraditionalChinese/NotoSansCJKtc-Regular.otf>.
- `Fonts/NotoSerifTC.otf` is unmodified **Noto Serif CJK TC Regular** from
  <https://github.com/notofonts/noto-cjk/blob/main/Serif/OTF/TraditionalChinese/NotoSerifCJKtc-Regular.otf>.

Both use the SIL Open Font License 1.1. The complete licenses and copyright
notices are alongside the fonts in `Assets/Resources/Fonts`.

## Editable credits

In Unity choose **Tools → 海豹咕咕 → 編輯 CREDITS**. The selected
`Assets/Resources/GameCredits.asset` exposes 小組 and 名單 in the Inspector.
Each group member can add a list item and type their own name and role, without
editing C#. Save the asset and enter Play Mode to review the credits; the next
Windows build includes the saved list.
