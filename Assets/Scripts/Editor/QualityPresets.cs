using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace RealisticCarRacing.EditorTools
{
    /// <summary>
    /// Defines the LOW / MEDIUM / HIGH graphics presets: one quality level and one URP asset each, available on
    /// every platform. Scene-side scaling (view distance, trees, skyline, bloom) lives in EnvironmentQualityScaler
    /// under the same level names. Re-running is safe.
    /// </summary>
    public static class QualityPresets
    {
        public static readonly string[] Names = { "Low", "Medium", "High" };

        const string SettingsFolder = "Assets/Settings";

        struct Preset
        {
            public float renderScale;
            public int msaa;
            public float shadowDistance;
            public int cascades;
            public Vector3 cascadeSplits;    // fractions of the shadow distance (2 cascades use x only)
            public int shadowResolution;
            public bool softShadows;
            public int softShadowQuality;   // 1 low, 2 medium, 3 high
            public int additionalLightsMode; // URP LightRenderingMode: 1 per-pixel, 2 per-vertex
            public int additionalLightsPerObject;
            public bool additionalLightShadows;
            public bool probeBlending;
            public float lodBias;
            public int textureMipLimit;
            public AnisotropicFiltering anisotropic;
            public int streamingBudgetMB;
            public string renderer;          // which renderer data the asset uses
        }

        static readonly Preset[] Presets =
        {
            new Preset { renderScale = 0.75f, msaa = 1, shadowDistance = 45f, cascades = 1, cascadeSplits = Vector3.zero, shadowResolution = 1024, softShadows = false, softShadowQuality = 1,
                additionalLightsMode = 2, additionalLightsPerObject = 2, additionalLightShadows = false, probeBlending = false,
                lodBias = 0.6f, textureMipLimit = 1, anisotropic = AnisotropicFiltering.Disable, streamingBudgetMB = 128, renderer = "Mobile_Renderer" },
            new Preset { renderScale = 0.9f, msaa = 2, shadowDistance = 90f, cascades = 2, cascadeSplits = new Vector3(0.22f, 0f, 0f), shadowResolution = 2048, softShadows = true, softShadowQuality = 1,
                additionalLightsMode = 1, additionalLightsPerObject = 4, additionalLightShadows = false, probeBlending = false,
                lodBias = 1f, textureMipLimit = 0, anisotropic = AnisotropicFiltering.Enable, streamingBudgetMB = 256, renderer = "Mobile_Renderer" },
            // Most of the resolution goes to the first 40 m, where the cars and their contact shadows are.
            new Preset { renderScale = 1f, msaa = 4, shadowDistance = 180f, cascades = 4, cascadeSplits = new Vector3(0.05f, 0.14f, 0.38f), shadowResolution = 2048, softShadows = true, softShadowQuality = 3,
                additionalLightsMode = 1, additionalLightsPerObject = 8, additionalLightShadows = true, probeBlending = true,
                lodBias = 1.6f, textureMipLimit = 0, anisotropic = AnisotropicFiltering.ForceEnable, streamingBudgetMB = 512, renderer = "PC_Renderer" }
        };

        public static void Apply()
        {
            var assets = CreateRenderPipelineAssets();
            ConfigureQualityLevels(assets);
            for (int i = 0; i < Names.Length; i++)
                ConfigureRenderPipelineAsset(assets[i], Presets[i]);
            ConfigureLevelSettings();
            AssetDatabase.SaveAssets();
        }

        static RenderPipelineAsset[] CreateRenderPipelineAssets()
        {
            // The URP template ships Mobile_RPAsset and PC_RPAsset; they become Medium and High, and Low is a copy of Medium.
            Rename("Mobile_RPAsset", "Medium_RPAsset");
            Rename("PC_RPAsset", "High_RPAsset");
            string low = $"{SettingsFolder}/Low_RPAsset.asset";
            if (AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(low) == null)
                AssetDatabase.CopyAsset($"{SettingsFolder}/Medium_RPAsset.asset", low);

            var assets = new RenderPipelineAsset[Names.Length];
            for (int i = 0; i < Names.Length; i++)
                assets[i] = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>($"{SettingsFolder}/{Names[i]}_RPAsset.asset");
            return assets;
        }

        static void Rename(string from, string to)
        {
            string path = $"{SettingsFolder}/{from}.asset";
            if (AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(path) != null &&
                AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>($"{SettingsFolder}/{to}.asset") == null)
                AssetDatabase.RenameAsset(path, to);
        }

        static void ConfigureQualityLevels(RenderPipelineAsset[] assets)
        {
            var qualitySettings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);
            var levels = qualitySettings.FindProperty("m_QualitySettings");
            while (levels.arraySize < Names.Length)
                levels.InsertArrayElementAtIndex(0); // duplicates the lowest level as a starting point
            while (levels.arraySize > Names.Length)
                levels.DeleteArrayElementAtIndex(levels.arraySize - 1);

            for (int i = 0; i < Names.Length; i++)
            {
                var level = levels.GetArrayElementAtIndex(i);
                level.FindPropertyRelative("name").stringValue = Names[i];
                level.FindPropertyRelative("customRenderPipeline").objectReferenceValue = assets[i];
                level.FindPropertyRelative("excludedTargetPlatforms").ClearArray(); // every preset is selectable everywhere
            }

            // Starting preset before device detection: phones/tablets Medium, desktops High, web Low.
            var defaults = qualitySettings.FindProperty("m_PerPlatformDefaultQuality");
            for (int i = 0; i < defaults.arraySize; i++)
            {
                var entry = defaults.GetArrayElementAtIndex(i);
                string platform = entry.FindPropertyRelative("first").stringValue;
                entry.FindPropertyRelative("second").intValue = platform switch
                {
                    "Standalone" => 2,
                    "WebGL" => 0,
                    _ => 1
                };
            }
            qualitySettings.FindProperty("m_CurrentQuality").intValue = 2;
            qualitySettings.ApplyModifiedPropertiesWithoutUndo();
        }

        static void ConfigureRenderPipelineAsset(RenderPipelineAsset asset, Preset preset)
        {
            var so = new SerializedObject(asset);
            void Set(string name, object value)
            {
                var property = so.FindProperty(name);
                if (property == null)
                {
                    Debug.LogWarning($"QualityPresets: {asset.name} has no property {name}");
                    return;
                }
                switch (value)
                {
                    case float f: property.floatValue = f; break;
                    case int n: property.intValue = n; break; // enum values (MSAA, shadow resolution...) are written as values, not indices
                    case bool b: property.boolValue = b; break;
                    case Vector3 v: property.vector3Value = v; break;
                }
            }

            Set("m_RenderScale", preset.renderScale);
            Set("m_MSAA", preset.msaa);
            Set("m_SupportsHDR", true); // keep the same tonemapped look on every preset
            Set("m_ShadowDistance", preset.shadowDistance);
            Set("m_ShadowCascadeCount", preset.cascades);
            if (preset.cascades == 2)
                Set("m_Cascade2Split", preset.cascadeSplits.x);
            if (preset.cascades == 4)
                Set("m_Cascade4Split", preset.cascadeSplits);
            Set("m_MainLightShadowmapResolution", preset.shadowResolution);
            Set("m_SoftShadowsSupported", preset.softShadows);
            Set("m_SoftShadowQuality", preset.softShadowQuality);
            Set("m_AdditionalLightsRenderingMode", preset.additionalLightsMode);
            Set("m_AdditionalLightsPerObjectLimit", preset.additionalLightsPerObject);
            Set("m_AdditionalLightShadowsSupported", preset.additionalLightShadows);
            Set("m_ReflectionProbeBlending", preset.probeBlending);
            Set("m_ReflectionProbeBoxProjection", preset.probeBlending);
            Set("m_UseSRPBatcher", true);

            // Point the asset at the right renderer (Forward for Low/Medium, Forward+ with SSAO for High).
            var renderer = AssetDatabase.LoadAssetAtPath<ScriptableObject>($"{SettingsFolder}/{preset.renderer}.asset");
            var list = so.FindProperty("m_RendererDataList");
            if (renderer != null && list != null && list.arraySize > 0)
                list.GetArrayElementAtIndex(0).objectReferenceValue = renderer;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
        }

        static void ConfigureLevelSettings()
        {
            int previous = QualitySettings.GetQualityLevel();
            for (int i = 0; i < Names.Length; i++)
            {
                var preset = Presets[i];
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.lodBias = preset.lodBias;
                QualitySettings.maximumLODLevel = 0;
                QualitySettings.enableLODCrossFade = i > 0;
                QualitySettings.globalTextureMipmapLimit = preset.textureMipLimit;
                QualitySettings.anisotropicFiltering = preset.anisotropic;
                QualitySettings.realtimeReflectionProbes = false; // all probes are baked
                QualitySettings.streamingMipmapsActive = true;
                QualitySettings.streamingMipmapsMemoryBudget = preset.streamingBudgetMB;
                QualitySettings.streamingMipmapsMaxLevelReduction = 2;
                QualitySettings.softParticles = false;
                QualitySettings.antiAliasing = 0; // URP handles MSAA per asset
                QualitySettings.vSyncCount = 0;   // frame pacing is set by the graphics settings service
                QualitySettings.skinWeights = SkinWeights.TwoBones;
            }
            QualitySettings.SetQualityLevel(Mathf.Clamp(previous, 0, Names.Length - 1), false);
        }
    }
}
