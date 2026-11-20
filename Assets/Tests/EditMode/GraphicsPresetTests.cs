using NUnit.Framework;
using RealisticCarRacing.Managers;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RealisticCarRacing.Tests
{
    public class GraphicsPresetTests
    {
        static UniversalRenderPipelineAsset Asset(int level) => (UniversalRenderPipelineAsset)QualitySettings.GetRenderPipelineAssetAt(level);

        [Test]
        public void Presets_AreLowMediumHigh_WithTheirOwnPipelineAssets()
        {
            Assert.That(QualitySettings.names, Is.EqualTo(new[] { "Low", "Medium", "High" }));
            Assert.That(Asset(0), Is.Not.SameAs(Asset(1)));
            Assert.That(Asset(1), Is.Not.SameAs(Asset(2)));
        }

        [Test]
        public void Presets_ScaleUpMonotonically()
        {
            for (int i = 1; i < 3; i++)
            {
                Assert.That(Asset(i).renderScale, Is.GreaterThan(Asset(i - 1).renderScale), "render scale");
                Assert.That(Asset(i).shadowDistance, Is.GreaterThan(Asset(i - 1).shadowDistance), "shadow distance");
                Assert.That(Asset(i).shadowCascadeCount, Is.GreaterThan(Asset(i - 1).shadowCascadeCount), "cascades");
                Assert.That(Asset(i).msaaSampleCount, Is.GreaterThan(Asset(i - 1).msaaSampleCount), "MSAA");
            }
            // Same look everywhere: HDR (and with it the tonemapped colour) is on for every preset.
            for (int i = 0; i < 3; i++)
                Assert.That(Asset(i).supportsHDR, Is.True);
        }

        [Test]
        public void Presets_AreSelectableOnEveryPlatform()
        {
            var qualitySettings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);
            var levels = qualitySettings.FindProperty("m_QualitySettings");
            for (int i = 0; i < levels.arraySize; i++)
                Assert.That(levels.GetArrayElementAtIndex(i).FindPropertyRelative("excludedTargetPlatforms").arraySize, Is.EqualTo(0));
        }

        [Test]
        public void PerformanceSettings_AreApplied()
        {
            Assert.That(PlayerSettings.bakeCollisionMeshes, Is.True, "collision meshes cooked at build time");
            Assert.That(Physics.queriesHitTriggers, Is.False);
            Assert.That(LayerMask.NameToLayer("Vehicles"), Is.GreaterThanOrEqualTo(0));
            Assert.That(PlayerSettings.enableFrameTimingStats, Is.True);
        }

        [Test]
        public void DeviceDetector_RecommendsSensiblePresets()
        {
            DeviceTierDetector.DeviceFacts Facts(bool mobile, int ram, int vram, int cores, GraphicsDeviceType api = GraphicsDeviceType.Metal, int shader = 50) =>
                new DeviceTierDetector.DeviceFacts(mobile, ram, vram, cores, shader, api);

            Assert.That(DeviceTierDetector.Recommend(Facts(false, 16384, 10000, 10)), Is.EqualTo(DeviceTierDetector.High), "M-series laptop");
            Assert.That(DeviceTierDetector.Recommend(Facts(false, 8192, 1536, 4, GraphicsDeviceType.Direct3D11)), Is.EqualTo(DeviceTierDetector.Medium), "older laptop, integrated GPU");
            Assert.That(DeviceTierDetector.Recommend(Facts(true, 8192, 2048, 8, GraphicsDeviceType.Vulkan)), Is.EqualTo(DeviceTierDetector.Medium), "recent phone");
            Assert.That(DeviceTierDetector.Recommend(Facts(true, 4096, 1024, 8, GraphicsDeviceType.Vulkan)), Is.EqualTo(DeviceTierDetector.Low), "4 GB phone");
            Assert.That(DeviceTierDetector.Recommend(Facts(true, 6144, 2048, 8, GraphicsDeviceType.OpenGLES3)), Is.EqualTo(DeviceTierDetector.Low), "GLES-only device");
            Assert.That(DeviceTierDetector.Recommend(Facts(false, 3072, 512, 2, GraphicsDeviceType.Direct3D11, 40)), Is.EqualTo(DeviceTierDetector.Low));
        }

        [Test]
        public void GraphicsPreferences_AreSavedWithTheProfile()
        {
            var storage = new MemoryStorage();
            var profile = new PlayerProfile();
            profile.graphics.preset = 0;
            profile.graphics.renderScale = 0.65f;
            profile.graphics.frameRateLimit = 30;
            profile.graphics.showPerformance = true;
            ProfileStore.Save(storage, profile);
            var loaded = ProfileStore.Load(storage).graphics;
            Assert.That(loaded.preset, Is.EqualTo(0));
            Assert.That(loaded.renderScale, Is.EqualTo(0.65f));
            Assert.That(loaded.frameRateLimit, Is.EqualTo(30));
            Assert.That(loaded.showPerformance, Is.True);
        }
    }
}
