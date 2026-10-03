using UnityEditor;
using UnityEngine;

namespace SealGugu.Editor
{
    /// <summary>Keep hand-drawn atlas pixels and measured audio sample alignment intact.</summary>
    public sealed class GuguAssetImporter : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/Resources/Art/")) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.isReadable = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 4096;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            var platform = importer.GetDefaultPlatformTextureSettings();
            platform.format = TextureImporterFormat.RGBA32;
            platform.maxTextureSize = 4096;
            platform.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SetPlatformTextureSettings(platform);
        }

        private void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith("Assets/Resources/Audio/")) return;
            var importer = (AudioImporter)assetImporter;
            importer.forceToMono = false;
            importer.loadInBackground = false;
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.PCM;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            settings.quality = 1;
            settings.preloadAudioData = true;
            importer.defaultSampleSettings = settings;
        }
    }
}
