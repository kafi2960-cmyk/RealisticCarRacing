using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RealisticCarRacing.Managers
{
    /// <summary>
    /// Applies the saved graphics preferences at startup and whenever the graphics menu changes them: quality preset,
    /// render-scale override, frame-rate cap and the performance overlay. Render-scale overrides go to a runtime copy
    /// of the preset's URP asset, so the project's assets are never modified.
    /// </summary>
    public static class GraphicsSettingsService
    {
        public static readonly int[] FrameRateOptions = { 30, 60, 120, -1 };

        static UniversalRenderPipelineAsset runtimeCopy;
        static int runtimeCopyLevel = -1;
        static PerformanceOverlay overlay;
        // QualitySettings.renderPipeline replaces the *current level's* asset, so the originals are remembered and
        // put back on quit (an editor play session must never leave a temporary asset in the project settings).
        static RenderPipelineAsset[] originalAssets;

        public static event Action Applied;

        /// <summary>The saved preferences; a missing or out-of-range preset (new or older save) resolves to the device recommendation.</summary>
        public static GraphicsPreferences Preferences
        {
            get
            {
                var prefs = GameSession.Profile.graphics;
                if (prefs.preset < 0 || prefs.preset >= PresetCount)
                    prefs.preset = Mathf.Min(DeviceTierDetector.Recommend(), PresetCount - 1);
                return prefs;
            }
        }

        public static int PresetCount => QualitySettings.names.Length;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Initialize()
        {
            CaptureOriginalAssets();
            if (GameSession.Profile.graphics.preset < 0)
            {
                _ = Preferences; // resolves the recommendation
                GameSession.Save();
            }
            PerformanceBenchmark.GraphicsSettingsHook = level =>
            {
                Preferences.preset = level;
                Apply();
            };
            Apply();
        }

        /// <summary>Default frame-rate cap: 30 on the Low preset on phones (battery and heat), otherwise 60.</summary>
        public static int DefaultFrameRate(int preset) => Application.isMobilePlatform && preset == 0 ? 30 : 60;

        public static float PresetRenderScale(int preset)
        {
            CaptureOriginalAssets();
            return originalAssets[Mathf.Clamp(preset, 0, PresetCount - 1)] is UniversalRenderPipelineAsset urp ? urp.renderScale : 1f;
        }

        /// <summary>The preset's own URP asset (never the runtime copy).</summary>
        public static RenderPipelineAsset PresetAsset(int preset)
        {
            CaptureOriginalAssets();
            return originalAssets[Mathf.Clamp(preset, 0, PresetCount - 1)];
        }

        static void CaptureOriginalAssets()
        {
            if (originalAssets != null)
                return;
            originalAssets = new RenderPipelineAsset[PresetCount];
            for (int i = 0; i < originalAssets.Length; i++)
                originalAssets[i] = QualitySettings.GetRenderPipelineAssetAt(i);
            Application.quitting += RestoreOriginalAssets;
        }

        static void RestoreOriginalAssets()
        {
            if (originalAssets == null)
                return;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < originalAssets.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = originalAssets[i];
            }
            QualitySettings.SetQualityLevel(current, false);
        }

        public static float EffectiveRenderScale => Preferences.renderScale > 0f ? Preferences.renderScale : PresetRenderScale(Preferences.preset);

        public static int EffectiveFrameRate => Preferences.frameRateLimit == 0 ? DefaultFrameRate(Preferences.preset) : Preferences.frameRateLimit;

        public static void SetPreset(int preset)
        {
            Preferences.preset = Mathf.Clamp(preset, 0, PresetCount - 1);
            Preferences.renderScale = 0f; // a new preset brings its own scale
            Save();
        }

        public static void SetRenderScale(float scale)
        {
            Preferences.renderScale = Mathf.Clamp(Mathf.Round(scale * 20f) / 20f, 0.5f, 1f);
            Save();
        }

        /// <summary>Back to the preset's own resolution scale.</summary>
        public static void ResetRenderScale()
        {
            Preferences.renderScale = 0f;
            Save();
        }

        public static void SetFrameRate(int limit)
        {
            Preferences.frameRateLimit = limit;
            Save();
        }

        public static void SetShowPerformance(bool show)
        {
            Preferences.showPerformance = show;
            Save();
        }

        static void Save()
        {
            GameSession.Save();
            Apply();
        }

        public static void Apply()
        {
            var prefs = Preferences;
            int preset = Mathf.Clamp(prefs.preset, 0, PresetCount - 1);
            if (QualitySettings.GetQualityLevel() != preset)
                QualitySettings.SetQualityLevel(preset, true);
            ApplyRenderScale(preset, prefs.renderScale);

            int limit = EffectiveFrameRate;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = limit;

            if (prefs.showPerformance && overlay == null)
                overlay = PerformanceOverlay.Create();
            else if (!prefs.showPerformance && overlay != null)
            {
                UnityEngine.Object.Destroy(overlay.gameObject);
                overlay = null;
            }
            Applied?.Invoke();
        }

        static void ApplyRenderScale(int preset, float scale)
        {
            CaptureOriginalAssets();
            var original = originalAssets[preset];
            if (scale <= 0f || original is not UniversalRenderPipelineAsset source)
            {
                QualitySettings.renderPipeline = original; // the preset's own asset
                return;
            }
            if (runtimeCopy == null || runtimeCopyLevel != preset)
            {
                if (runtimeCopy != null)
                {
                    // The level that used the old copy goes back to its own asset before the copy is destroyed.
                    int current = QualitySettings.GetQualityLevel();
                    QualitySettings.SetQualityLevel(runtimeCopyLevel, false);
                    QualitySettings.renderPipeline = originalAssets[runtimeCopyLevel];
                    QualitySettings.SetQualityLevel(current, false);
                    UnityEngine.Object.Destroy(runtimeCopy);
                }
                runtimeCopy = UnityEngine.Object.Instantiate(source);
                runtimeCopy.name = $"{source.name} (runtime scale)";
                runtimeCopyLevel = preset;
            }
            runtimeCopy.renderScale = scale;
            QualitySettings.renderPipeline = runtimeCopy;
        }

        /// <summary>The URP asset currently rendering (runtime copy or the preset's asset).</summary>
        public static UniversalRenderPipelineAsset ActivePipeline => GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
    }
}
