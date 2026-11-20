using System;
using RealisticCarRacing.Tracks;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RealisticCarRacing.Managers
{
    /// <summary>
    /// Scales the environment to the active graphics preset (Low / Medium / High quality levels):
    /// view distance, fog, tree density/distance/shadows, the city skyline and bloom. URP-level settings
    /// (render scale, MSAA, shadows, SSAO) live in each preset's URP asset; this covers the scene-side costs.
    /// The look (sky, colour grading, hills, track) is the same on every preset.
    /// </summary>
    public sealed class EnvironmentQualityScaler : MonoBehaviour
    {
        [Serializable]
        public struct Tier
        {
            [Tooltip("Name of the quality level this tier applies to.")]
            public string qualityLevel;
            [Tooltip("Cull distance for ordinary objects; the Background layer is always drawn to the horizon.")]
            public float cameraFarClip;
            [Tooltip("Keep the same on every tier: fog is part of the look and costs nothing.")]
            public float fogEnd;
            public float scatterDrawDistance;
            [Range(0f, 1f)] public float scatterDensity;
            [Tooltip("Trees within this distance cast shadows (0 = none). Keep at or below the preset's shadow distance.")]
            public float scatterShadowDistance;
            [Tooltip("Show the objects under High Detail Root (the city skyline).")]
            public bool highDetailScenery;
            public bool bloom;
            public AntialiasingMode antialiasing;
        }

        [SerializeField] Camera targetCamera;
        [SerializeField] Volume postProcessVolume;
        [SerializeField] InstancedScatter[] scatters = Array.Empty<InstancedScatter>();
        [SerializeField] GameObject highDetailRoot;
        [SerializeField] Tier[] tiers =
        {
            new Tier
            {
                qualityLevel = "Low", cameraFarClip = 900f, fogEnd = 2800f, scatterDrawDistance = 250f, scatterDensity = 0.35f,
                scatterShadowDistance = 0f, highDetailScenery = false, bloom = false, antialiasing = AntialiasingMode.None
            },
            new Tier
            {
                qualityLevel = "Medium", cameraFarClip = 1700f, fogEnd = 2800f, scatterDrawDistance = 550f, scatterDensity = 0.65f,
                scatterShadowDistance = 60f, highDetailScenery = true, bloom = true, antialiasing = AntialiasingMode.None
            },
            new Tier
            {
                qualityLevel = "High", cameraFarClip = 3200f, fogEnd = 2800f, scatterDrawDistance = 1100f, scatterDensity = 1f,
                scatterShadowDistance = 150f, highDetailScenery = true, bloom = true, antialiasing = AntialiasingMode.None
            }
        };

        /// <summary>Layer for horizon scenery that every preset keeps (see <see cref="BackgroundDistance"/>).</summary>
        public const string BackgroundLayer = "Background";
        public const float BackgroundDistance = 3200f;

        public Tier ActiveTier { get; private set; }

        public void Configure(Camera cameraToScale, Volume volume, InstancedScatter[] scatterRenderers, GameObject highDetail)
        {
            targetCamera = cameraToScale;
            postProcessVolume = volume;
            scatters = scatterRenderers;
            highDetailRoot = highDetail;
        }

        void OnEnable()
        {
            QualitySettings.activeQualityLevelChanged += OnQualityLevelChanged;
            Apply();
        }

        void OnDisable() => QualitySettings.activeQualityLevelChanged -= OnQualityLevelChanged;

        void OnQualityLevelChanged(int previous, int current) => Apply();

        public void Apply()
        {
            ActiveTier = FindTier(QualitySettings.names[QualitySettings.GetQualityLevel()]);
            var tier = ActiveTier;

            if (targetCamera != null)
            {
                // Objects are culled at the tier's distance, but the Background layer (the hills on the horizon) is
                // always drawn: it is part of the circuit's look and costs a single mesh.
                targetCamera.farClipPlane = Mathf.Max(tier.cameraFarClip, BackgroundDistance);
                var distances = new float[32];
                for (int layer = 0; layer < distances.Length; layer++)
                    distances[layer] = tier.cameraFarClip;
                int background = LayerMask.NameToLayer(BackgroundLayer);
                if (background >= 0)
                    distances[background] = 0f; // 0 = use the far clip plane
                targetCamera.layerCullDistances = distances;
                if (targetCamera.TryGetComponent(out UniversalAdditionalCameraData cameraData))
                    cameraData.antialiasing = tier.antialiasing;
            }

            RenderSettings.fogEndDistance = tier.fogEnd;
            RenderSettings.fogStartDistance = Mathf.Min(RenderSettings.fogStartDistance, tier.fogEnd * 0.5f);

            foreach (var scatter in scatters)
            {
                if (scatter == null)
                    continue;
                scatter.DrawDistance = tier.scatterDrawDistance;
                scatter.Density = tier.scatterDensity;
                scatter.ShadowDistance = tier.scatterShadowDistance;
            }

            if (highDetailRoot != null)
                highDetailRoot.SetActive(tier.highDetailScenery);

            if (postProcessVolume != null && postProcessVolume.profile != null &&
                postProcessVolume.profile.TryGet(out Bloom bloom))
                bloom.active = tier.bloom;
        }

        Tier FindTier(string qualityLevel)
        {
            foreach (var tier in tiers)
            {
                if (string.Equals(tier.qualityLevel, qualityLevel, StringComparison.OrdinalIgnoreCase))
                    return tier;
            }
            // Unknown level: use the cheapest tier, which is always safe to run.
            return tiers[0];
        }
    }
}
