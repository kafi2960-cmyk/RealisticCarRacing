using UnityEditor;
using UnityEngine;

namespace RealisticCarRacing.EditorTools
{
    /// <summary>
    /// Import defaults for audio added later, by folder:
    /// <list type="bullet">
    /// <item>Assets/Audio/Music — streamed from disk, Vorbis (keeps long tracks out of memory).</item>
    /// <item>Assets/Audio/Engine — loaded compressed in memory, Vorbis; engines loop and are pitched continuously.</item>
    /// <item>anything else (SFX) — mono, decompressed on load (cheap to play, tiny clips), ADPCM on phones.</item>
    /// </list>
    /// Only applies to clips being imported for the first time, so manual changes in the Inspector stick.
    /// </summary>
    public sealed class AudioImportRules : AssetPostprocessor
    {
        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith("Assets/Audio/") || !assetImporter.importSettingsMissing)
                return;
            var importer = (AudioImporter)assetImporter;
            var settings = importer.defaultSampleSettings;

            if (assetPath.StartsWith("Assets/Audio/Music/"))
            {
                settings.loadType = AudioClipLoadType.Streaming;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = 0.6f;
            }
            else if (assetPath.StartsWith("Assets/Audio/Engine/"))
            {
                settings.loadType = AudioClipLoadType.CompressedInMemory;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = 0.7f;
            }
            else
            {
                importer.forceToMono = true;
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = 0.5f;
                var mobile = settings;
                mobile.compressionFormat = AudioCompressionFormat.ADPCM;
                foreach (string platform in new[] { "Android", "iOS" })
                    importer.SetOverrideSampleSettings(platform, mobile);
            }
            settings.sampleRateSetting = AudioSampleRateSetting.OptimizeSampleRate;
            importer.defaultSampleSettings = settings;
        }
    }
}
