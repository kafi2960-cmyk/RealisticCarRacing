using UnityEditor;
using UnityEngine;

namespace RealisticCarRacing.EditorTools
{
    /// <summary>
    /// Import defaults for audio, by folder:
    /// <list type="bullet">
    /// <item>Assets/Audio/Music — streamed from disk, Vorbis (keeps long tracks out of memory).</item>
    /// <item>Assets/Audio/Engine and Assets/Audio/Loops — mono (they play in 3D), kept compressed in memory, Vorbis;
    /// these loop and are pitched continuously, and some are long. Phones: lower quality and 32 kHz.</item>
    /// <item>UI and anything else (short one-shots) — mono, decompressed on load (cheapest to trigger), Vorbis on
    /// desktop, ADPCM on phones (decodes almost for free).</item>
    /// </list>
    /// Only applies to clips being imported for the first time, so manual changes in the Inspector stick.
    /// </summary>
    public sealed class AudioImportRules : AssetPostprocessor
    {
        static readonly string[] MobilePlatforms = { "Android", "iOS" };

        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith("Assets/Audio/") || !assetImporter.importSettingsMissing)
                return;
            var importer = (AudioImporter)assetImporter;
            var settings = importer.defaultSampleSettings;
            settings.sampleRateSetting = AudioSampleRateSetting.OptimizeSampleRate;

            if (assetPath.StartsWith("Assets/Audio/Music/"))
            {
                settings.loadType = AudioClipLoadType.Streaming;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = 0.6f;
                importer.defaultSampleSettings = settings;
                return;
            }

            importer.forceToMono = true;
            if (assetPath.StartsWith("Assets/Audio/Engine/") || assetPath.StartsWith("Assets/Audio/Loops/"))
            {
                settings.loadType = AudioClipLoadType.CompressedInMemory;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = 0.7f;
                var mobile = settings;
                mobile.quality = 0.5f;
                mobile.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate;
                mobile.sampleRateOverride = 32000;
                foreach (string platform in MobilePlatforms)
                    importer.SetOverrideSampleSettings(platform, mobile);
            }
            else
            {
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = 0.5f;
                var mobile = settings;
                mobile.compressionFormat = AudioCompressionFormat.ADPCM;
                foreach (string platform in MobilePlatforms)
                    importer.SetOverrideSampleSettings(platform, mobile);
            }
            importer.defaultSampleSettings = settings;
        }
    }
}
