using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace RealisticCarRacing.EditorTools
{
    /// <summary>
    /// Applies the cross-platform player settings (Windows, macOS, Android, iOS) in one step.
    /// Safe to run repeatedly. Menu: RealisticCarRacing &gt; Setup &gt; Apply Project Settings.
    /// Command line: -executeMethod RealisticCarRacing.EditorTools.ProjectSetup.Apply
    /// </summary>
    public static class ProjectSetup
    {
        // Placeholder: change to a reverse-DNS id you own before publishing.
        const string ApplicationId = "com.realisticcarracing.game";
        const float PhysicsStepSeconds = 1f / 60f;

        [MenuItem("RealisticCarRacing/Setup/Apply Project Settings")]
        public static void Apply()
        {
            PlayerSettings.companyName = "RealisticCarRacing";
            PlayerSettings.productName = "Realistic Car Racing";
            PlayerSettings.bundleVersion = "0.1.0";

            foreach (var target in new[] { NamedBuildTarget.Standalone, NamedBuildTarget.Android, NamedBuildTarget.iOS })
                PlayerSettings.SetApplicationIdentifier(target, ApplicationId);

            // Desktop: Mono builds for both Windows and macOS from any OS (Windows IL2CPP needs a Windows machine).
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);

            // Mobile: IL2CPP and 64-bit ARM, as required by Google Play and the App Store.
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)26; // Android 8.0
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.iOS.targetOSVersionString = "15.0";

            // Racing is landscape-only on phones and tablets.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;

            // 60 Hz physics keeps WheelCollider suspension stable without overloading mobile CPUs.
            SetFixedTimestep(PhysicsStepSeconds);

            QualityPresets.Apply();
            ConfigurePerformanceSettings();

            // CPU/GPU frame timings for the in-game performance overlay and benchmark.
            PlayerSettings.enableFrameTimingStats = true;

            AssetDatabase.SaveAssets();
            Debug.Log("RealisticCarRacing: project settings applied.");
        }

        /// <summary>Physics, audio and build settings that are the same on every graphics preset.</summary>
        static void ConfigurePerformanceSettings()
        {
            EnsureLayer(8, "Vehicles");
            EnsureLayer(9, "Background");

            // Cook MeshColliders at build time instead of during scene load.
            PlayerSettings.bakeCollisionMeshes = true;
            // Physics queries (camera, AI) never need triggers; reuse collision callback objects (less garbage).
            var dynamics = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/DynamicsManager.asset")[0]);
            dynamics.FindProperty("m_QueriesHitTriggers").boolValue = false;
            dynamics.FindProperty("m_ReuseCollisionCallbacks").boolValue = true;
            dynamics.ApplyModifiedPropertiesWithoutUndo();
            // Cap catch-up physics steps after a hitch (at most 6 steps per frame) so slow devices don't spiral.
            var time = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TimeManager.asset")[0]);
            var maximum = time.FindProperty("Maximum Allowed Timestep");
            if (maximum != null && maximum.propertyType == SerializedPropertyType.Float)
            {
                maximum.floatValue = 0.1f;
                time.ApplyModifiedPropertiesWithoutUndo();
            }

            // Audio: fewer voices than the defaults (the game has no audio assets yet; see AudioImportRules for clips).
            var audio = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/AudioManager.asset")[0]);
            audio.FindProperty("m_RealVoiceCount").intValue = 24;
            audio.FindProperty("m_VirtualVoiceCount").intValue = 64;
            audio.FindProperty("m_DSPBufferSize").intValue = 1024; // "Good latency": lower CPU cost than "Best latency"
            audio.ApplyModifiedPropertiesWithoutUndo();
        }

        static void EnsureLayer(int index, string name)
        {
            var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tags.FindProperty("layers");
            var layer = layers.GetArrayElementAtIndex(index);
            if (string.IsNullOrEmpty(layer.stringValue))
            {
                layer.stringValue = name;
                tags.ApplyModifiedPropertiesWithoutUndo();
            }
            else if (layer.stringValue != name)
            {
                Debug.LogWarning($"RealisticCarRacing: layer {index} is '{layer.stringValue}', expected '{name}'.");
            }
        }

        static void SetFixedTimestep(float seconds)
        {
            var timeManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TimeManager.asset")[0]);
            var fixedTimestep = timeManager.FindProperty("Fixed Timestep");
            if (fixedTimestep != null && fixedTimestep.propertyType == SerializedPropertyType.Float)
            {
                fixedTimestep.floatValue = seconds;
                timeManager.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                Time.fixedDeltaTime = seconds;
            }
        }
    }
}
