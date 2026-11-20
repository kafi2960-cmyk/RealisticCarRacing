using System.Collections.Generic;
using RealisticCarRacing.EditorTools.Circuit;
using RealisticCarRacing.Vehicles;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using TextureKind = RealisticCarRacing.EditorTools.Circuit.ProceduralTextures.TextureKind;

namespace RealisticCarRacing.EditorTools
{
    /// <summary>
    /// Generates the <see cref="CarVisualKit"/>: modelled wheel parts (lathed tyre with tread and sidewall, ten-spoke
    /// rim, drilled brake disc, caliper), the placeholder glasshouse, their textures, and the car materials
    /// (clear-coat metallic paint, glass, rubber, metals, leather, plastics, gauges and lamps). Deterministic;
    /// re-running updates the assets in place.
    /// </summary>
    public static class CarKitBuilder
    {
        public const string KitPath = "Assets/Settings/Cars/CarVisualKit.asset";
        public const string PaintPath = MaterialFolder + "/Car_Paint.mat";
        const string MeshFolder = "Assets/Models/Generated/Car";
        const string TextureFolder = "Assets/Textures/Car";
        const string MaterialFolder = "Assets/Materials/Car";
        const string LitShader = "Universal Render Pipeline/Lit";
        const string ComplexLitShader = "Universal Render Pipeline/Complex Lit";

        public static CarVisualKit LoadOrCreate()
        {
            SceneBuildUtility.EnsureFolder(MeshFolder);
            SceneBuildUtility.EnsureFolder(MaterialFolder);
            SceneBuildUtility.EnsureFolder(System.IO.Path.GetDirectoryName(KitPath));
            var kit = AssetDatabase.LoadAssetAtPath<CarVisualKit>(KitPath);
            if (kit == null)
            {
                kit = ScriptableObject.CreateInstance<CarVisualKit>();
                AssetDatabase.CreateAsset(kit, KitPath);
            }

            kit.tyre = Save(Tyre(48, detailed: true), "Car_Tyre");
            kit.tyreLow = Save(Tyre(14, detailed: false), "Car_TyreLow");
            kit.rim = Save(Rim(SplitTenSpoke), "Car_Rim");
            kit.brakeDisc = Save(BrakeDisc(), "Car_BrakeDisc");
            kit.caliper = Save(Caliper(), "Car_Caliper");
            var (glass, frame) = Greenhouse();
            kit.greenhouseGlass = Save(glass, "Car_GreenhouseGlass");
            kit.greenhouseFrame = Save(frame, "Car_GreenhouseFrame");
            CreateMaterials(kit);
            EditorUtility.SetDirty(kit);
            AssetDatabase.SaveAssets();
            return kit;
        }

        /// <summary>Clear-coated metallic paint; each car instance copies it and sets its own colour.</summary>
        public static Material LoadOrCreatePaint()
        {
            var paint = Mat("Car_Paint", ComplexLitShader);
            paint.SetColor("_BaseColor", new Color(0.75f, 0.1f, 0.08f));
            paint.SetFloat("_Metallic", 0.45f);    // metallic flake base: tinted reflections
            paint.SetFloat("_Smoothness", 0.55f); // the base layer is satin...
            paint.SetFloat("_ClearCoat", 1f);     // ...under a glossy lacquer layer
            paint.SetFloat("_ClearCoatMask", 1f);
            paint.SetFloat("_ClearCoatSmoothness", 0.94f);
            paint.EnableKeyword("_CLEARCOAT");
            EditorUtility.SetDirty(paint);
            return paint;
        }

        // ---------- Materials ----------

        static void CreateMaterials(CarVisualKit kit)
        {
            LoadOrCreatePaint();
            var tyreAlbedo = ProceduralTextures.Get(TextureFolder, "Tyre_Albedo", 256, TextureKind.Color, TyreAlbedo);
            var tyreNormal = ProceduralTextures.Get(TextureFolder, "Tyre_Normal", 256, TextureKind.Normal, (size, px) => ProceduralTextures.HeightToNormal(size, px, TyreHeight, 3f));
            var tyreMask = ProceduralTextures.Get(TextureFolder, "Tyre_Mask", 256, TextureKind.Linear, TyreMask);
            var discAlbedo = ProceduralTextures.Get(TextureFolder, "BrakeDisc_Albedo", 256, TextureKind.Color, DiscAlbedo);
            var discMask = ProceduralTextures.Get(TextureFolder, "BrakeDisc_Mask", 256, TextureKind.Linear, DiscMask);
            var grainNormal = ProceduralTextures.Get(TextureFolder, "Interior_Grain_Normal", 256, TextureKind.Normal,
                (size, px) => ProceduralTextures.HeightToNormal(size, px, (u, v) => ProceduralTextures.Fbm(u, v, 32, 3, 301), 2f));
            var gauge = ProceduralTextures.Get(TextureFolder, "Gauge_Face", 256, TextureKind.Color, GaugeFace);

            kit.tyreRubber = Lit("Car_Tyre", Color.white, 1f, 0f, tyreAlbedo, tyreNormal, 1f, tyreMask);
            kit.rimMetal = Lit("Car_Rim", new Color(0.62f, 0.63f, 0.65f), 0.8f, 1f);
            kit.wheelDesigns = new[]
            {
                new WheelDesign { id = "split10", displayName = "Split 10-Spoke", finishName = "Machined silver", rim = kit.rim, finish = kit.rimMetal },
                new WheelDesign { id = "yspoke", displayName = "Forged Y-Spoke", finishName = "Gloss black",
                    rim = Save(Rim(YSpoke), "Car_Rim_YSpoke"), finish = Lit("Car_Rim_GlossBlack", new Color(0.035f, 0.036f, 0.04f), 0.88f, 0.6f) },
                new WheelDesign { id = "crossspoke", displayName = "Cross-Spoke Mesh", finishName = "Gunmetal",
                    rim = Save(Rim(CrossSpoke), "Car_Rim_CrossSpoke"), finish = Lit("Car_Rim_Gunmetal", new Color(0.2f, 0.21f, 0.22f), 0.72f, 1f) },
                new WheelDesign { id = "turbine", displayName = "Turbine 7-Spoke", finishName = "Satin bronze",
                    rim = Save(Rim(Turbine), "Car_Rim_Turbine"), finish = Lit("Car_Rim_Bronze", new Color(0.47f, 0.34f, 0.18f), 0.62f, 1f) }
            };
            kit.brakeDiscMetal = Lit("Car_BrakeDisc", Color.white, 1f, 1f, discAlbedo, mask: discMask);
            kit.caliperPaint = Lit("Car_Caliper", new Color(0.72f, 0.05f, 0.04f), 0.75f, 0f);
            kit.leather = Lit("Car_Leather", new Color(0.12f, 0.1f, 0.085f), 0.38f, 0f, normal: grainNormal, normalScale: 0.6f, tiling: 4f);
            kit.plastic = Lit("Car_Plastic", new Color(0.07f, 0.072f, 0.078f), 0.22f, 0f, normal: grainNormal, normalScale: 0.25f, tiling: 8f);
            kit.headliner = Lit("Car_Headliner", new Color(0.32f, 0.31f, 0.3f), 0.05f, 0f, normal: grainNormal, normalScale: 0.3f, tiling: 6f);
            kit.screen = Emissive(Lit("Car_Screen", new Color(0.015f, 0.02f, 0.03f), 0.92f, 0f), new Color(0.07f, 0.17f, 0.32f) * 1.5f);
            kit.gaugeFace = Emissive(Lit("Car_GaugeFace", Color.black, 0.85f, 0f), Color.white * 1.4f, gauge);
            kit.gaugeNeedle = Emissive(Lit("Car_GaugeNeedle", new Color(0.8f, 0.2f, 0.05f), 0.5f, 0f), new Color(1f, 0.25f, 0.05f) * 3f);

            // Lamps: lenses glow in HDR so bloom (Medium/High) gives them a halo; brake/reverse switch materials at runtime.
            kit.headlight = Emissive(Lit("Car_Headlight", new Color(0.9f, 0.9f, 0.92f), 0.95f, 0f), new Color(1f, 0.97f, 0.9f) * 4f);
            kit.tailLight = Emissive(Lit("Car_TailLight", new Color(0.25f, 0.02f, 0.02f), 0.92f, 0f), new Color(1f, 0.05f, 0.03f) * 0.9f);
            kit.brakeLight = Emissive(Lit("Car_BrakeLight", new Color(0.4f, 0.03f, 0.02f), 0.92f, 0f), new Color(1f, 0.05f, 0.03f) * 8f);
            kit.reverseLightOff = Lit("Car_ReverseLightOff", new Color(0.62f, 0.62f, 0.64f), 0.95f, 0f);
            kit.reverseLightOn = Emissive(Lit("Car_ReverseLightOn", new Color(0.9f, 0.9f, 0.92f), 0.95f, 0f), Color.white * 4f);

            // Tinted, reflective glass: alpha blends the cabin, while "preserve specular" keeps reflections at full strength.
            var glass = Lit("Car_Glass", new Color(0.015f, 0.02f, 0.025f, 0.72f), 0.96f, 0f);
            glass.SetFloat("_Surface", 1f);
            glass.SetFloat("_Blend", 0f);
            glass.SetFloat("_BlendModePreserveSpecular", 1f);
            glass.SetFloat("_ZWrite", 0f);
            BaseShaderGUI.SetupMaterialBlendMode(glass);
            kit.glass = glass;
        }

        static Material Mat(string name, string shader)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find(shader));
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader.name != shader)
            {
                material.shader = Shader.Find(shader);
            }
            return material;
        }

        static Material Lit(string name, Color color, float smoothness, float metallic, Texture2D albedo = null, Texture2D normal = null,
            float normalScale = 1f, Texture2D mask = null, float tiling = 1f)
        {
            var material = Mat(name, LitShader);
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness); // with a mask texture this multiplies its alpha
            material.SetFloat("_Metallic", metallic);
            material.SetTexture("_BaseMap", albedo);
            material.SetTextureScale("_BaseMap", Vector2.one * tiling);
            material.SetTexture("_BumpMap", normal);
            material.SetFloat("_BumpScale", normalScale);
            SetKeyword(material, "_NORMALMAP", normal != null);
            material.SetTexture("_MetallicGlossMap", mask);
            SetKeyword(material, "_METALLICSPECGLOSSMAP", mask != null);
            EditorUtility.SetDirty(material);
            return material;
        }

        static Material Emissive(Material material, Color emission, Texture2D emissionMap = null)
        {
            material.SetColor("_EmissionColor", emission);
            material.SetTexture("_EmissionMap", emissionMap);
            if (emissionMap != null)
                material.SetTexture("_BaseMap", emissionMap); // the dial markings also show when unlit
            material.EnableKeyword("_EMISSION");
            // URP's material validator keeps _EMISSION only for materials flagged emissive. The scene has no
            // real-time GI, so the flag costs nothing; it just keeps the lamps glowing.
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(material);
            return material;
        }

        static void SetKeyword(Material material, string keyword, bool enabled)
        {
            if (enabled)
                material.EnableKeyword(keyword);
            else
                material.DisableKeyword(keyword);
        }

        // ---------- Textures ----------
        // Tyre UVs: U around the tyre (24 tiles per turn), V across the profile (0 outer bead ... 1 inner bead).

        static bool IsTread(float v) => v > 0.31f && v < 0.69f;

        static float TyreHeight(float u, float v)
        {
            float h = 0.5f;
            if (IsTread(v))
            {
                // Two circumferential grooves and angled shoulder slots, as on a road-legal performance tyre.
                if (Mathf.Abs(v - 0.43f) < 0.018f || Mathf.Abs(v - 0.57f) < 0.018f)
                    h = 0f;
                float slot = (u * 3f + Mathf.Abs(v - 0.5f) * 2f) % 1f;
                if ((v < 0.39f || v > 0.61f) && slot < 0.12f)
                    h = 0.1f;
            }
            else
            {
                float side = v < 0.5f ? v : 1f - v;
                if (side > 0.1f && side < 0.125f)
                    h = 0.8f; // rim protector rib
                if (side > 0.16f && side < 0.23f && ProceduralTextures.Hash(Mathf.FloorToInt(u * 12f), Mathf.FloorToInt(side * 60f), 4096, 211) > 0.45f)
                    h = 0.62f; // raised lettering band
            }
            return h + ProceduralTextures.Fbm(u, v, 32, 2, 212) * 0.04f;
        }

        static void TyreAlbedo(int size, Color[] pixels) => ProceduralTextures.ForEach(size, pixels, (u, v) =>
        {
            float h = TyreHeight(u, v);
            float g = IsTread(v) ? 0.032f : 0.045f;
            if (h > 0.6f && !IsTread(v))
                g = 0.07f; // worn raised lettering catches the light
            g *= 0.85f + ProceduralTextures.Fbm(u, v, 16, 3, 213) * 0.3f;
            if (h < 0.2f)
                g *= 0.6f;
            return new Color(g, g, g * 1.05f, 1f);
        });

        static void TyreMask(int size, Color[] pixels) => ProceduralTextures.ForEach(size, pixels, (u, v) =>
            new Color(0f, 0f, 0f, IsTread(v) ? 0.22f : 0.38f));

        // Disc UVs: U around (12 tiles per turn), V across the friction ring (0 inner ... 1 outer).
        static bool IsDrillHole(float u, float v)
        {
            for (int i = 0; i < 3; i++)
            {
                float cu = (i + 0.5f) / 3f, cv = 0.3f + i * 0.2f;
                float du = (u - cu) * 2.2f, dv = v - cv;
                if (du * du + dv * dv < 0.05f * 0.05f)
                    return true;
            }
            return false;
        }

        static void DiscAlbedo(int size, Color[] pixels) => ProceduralTextures.ForEach(size, pixels, (u, v) =>
        {
            if (IsDrillHole(u, v))
                return new Color(0.04f, 0.04f, 0.04f);
            float scratches = ProceduralTextures.Fbm(0.5f, v, 64, 2, 221) * 0.12f; // concentric pad marks
            float g = 0.44f + scratches;
            float heat = Mathf.SmoothStep(0.8f, 1f, v) * 0.15f; // bluish heat tint at the edge
            return new Color(g - heat * 0.2f, g - heat * 0.1f, g + heat * 0.1f);
        });

        static void DiscMask(int size, Color[] pixels) => ProceduralTextures.ForEach(size, pixels, (u, v) =>
            IsDrillHole(u, v) ? new Color(1f, 0f, 0f, 0.2f) : new Color(1f, 0f, 0f, 0.5f + ProceduralTextures.Fbm(0.5f, v, 64, 2, 222) * 0.15f));

        /// <summary>Dial: 270° scale with major/minor ticks and a red zone, white on black (also the emission map).</summary>
        static void GaugeFace(int size, Color[] pixels) => ProceduralTextures.ForEach(size, pixels, (u, v) =>
        {
            float x = u - 0.5f, y = v - 0.5f, r = Mathf.Sqrt(x * x + y * y);
            float angle = Mathf.Atan2(x, y) * Mathf.Rad2Deg; // 0 at 12 o'clock, clockwise positive
            if (r > 0.47f && r < 0.48f)
                return new Color(0.35f, 0.35f, 0.35f);
            if (Mathf.Abs(angle) > 135f)
                return Color.black;
            float step = (angle + 135f) / 270f * 45f;
            float nearest = Mathf.Abs(step - Mathf.Round(step));
            bool major = Mathf.RoundToInt(step) % 5 == 0;
            bool tick = major ? r > 0.36f && r < 0.45f && nearest < 0.12f : r > 0.41f && r < 0.45f && nearest < 0.07f;
            if (!tick)
                return Color.black;
            return angle > 90f ? new Color(1f, 0.1f, 0.05f) : Color.white;
        });

        // ---------- Meshes ----------
        // Wheel parts are unit size: radius 1 around the X axle, width 1 (X -0.5..0.5), outer face towards +X.

        static Vector3 Radial(float angle) => new Vector3(0f, Mathf.Cos(angle), Mathf.Sin(angle));

        /// <summary>
        /// Lathes a profile of (x, radius) points about the X axis. Faces point away from <paramref name="center"/> in
        /// the profile plane. V runs along the profile from <paramref name="v0"/> to <paramref name="v1"/>.
        /// </summary>
        static void Lathe(MeshBuilder builder, int subMesh, Vector2[] profile, Vector2 center, int segments, float uTiles,
            float v0 = 0f, float v1 = 1f, float startDegrees = 0f, float endDegrees = 360f)
        {
            var vs = new float[profile.Length];
            float total = 0f;
            for (int i = 1; i < profile.Length; i++)
                total += Vector2.Distance(profile[i - 1], profile[i]);
            float run = 0f;
            for (int i = 0; i < profile.Length; i++)
            {
                if (i > 0)
                    run += Vector2.Distance(profile[i - 1], profile[i]);
                vs[i] = Mathf.Lerp(v0, v1, total > 0f ? run / total : 0f);
            }

            float start = startDegrees * Mathf.Deg2Rad, end = endDegrees * Mathf.Deg2Rad;
            var grid = new int[segments + 1, profile.Length];
            for (int s = 0; s <= segments; s++)
            {
                float angle = Mathf.Lerp(start, end, s / (float)segments);
                for (int k = 0; k < profile.Length; k++)
                {
                    var p = profile[k];
                    grid[s, k] = builder.AddVertex(new Vector3(p.x, 0f, 0f) + Radial(angle) * p.y, new Vector2(s / (float)segments * uTiles, vs[k]));
                }
            }
            for (int s = 0; s < segments; s++)
            {
                float mid = Mathf.Lerp(start, end, (s + 0.5f) / segments);
                for (int k = 0; k < profile.Length - 1; k++)
                {
                    Vector2 away = (profile[k] + profile[k + 1]) * 0.5f - center;
                    Vector3 facing = new Vector3(away.x, 0f, 0f) + Radial(mid) * away.y;
                    builder.AddQuad(subMesh, grid[s, k], grid[s + 1, k], grid[s + 1, k + 1], grid[s, k + 1], facing);
                }
            }
        }

        static Mesh Tyre(int segments, bool detailed)
        {
            var builder = new MeshBuilder();
            var profile = detailed
                ? new[]
                {
                    new Vector2(0.44f, 0.66f), new Vector2(0.48f, 0.72f), new Vector2(0.5f, 0.8f), new Vector2(0.49f, 0.88f),
                    new Vector2(0.46f, 0.95f), new Vector2(0.4f, 0.99f), new Vector2(0.3f, 1f), new Vector2(-0.3f, 1f),
                    new Vector2(-0.4f, 0.99f), new Vector2(-0.46f, 0.95f), new Vector2(-0.49f, 0.88f), new Vector2(-0.5f, 0.8f),
                    new Vector2(-0.48f, 0.72f), new Vector2(-0.44f, 0.66f)
                }
                : new[] { new Vector2(0.44f, 0.66f), new Vector2(0.5f, 0.82f), new Vector2(0.42f, 0.99f), new Vector2(-0.42f, 0.99f), new Vector2(-0.5f, 0.82f), new Vector2(-0.44f, 0.66f) };
            Lathe(builder, 0, profile, new Vector2(0f, 0.84f), segments, 24f);
            if (!detailed)
            {
                // Distant wheels: dark discs instead of rim and brake, so the wheel never looks hollow.
                Lathe(builder, 0, new[] { new Vector2(0.42f, 0f), new Vector2(0.44f, 0.66f) }, new Vector2(0f, 0.3f), segments, 1f, 0.5f, 0.5f);
                Lathe(builder, 0, new[] { new Vector2(-0.42f, 0f), new Vector2(-0.44f, 0.66f) }, new Vector2(0f, 0.3f), segments, 1f, 0.5f, 0.5f);
            }
            return builder.ToMesh(detailed ? "Car_Tyre" : "Car_TyreLow");
        }

        /// <summary>Rim: lip, barrel and hub shared by every design, plus the design's spokes.</summary>
        static Mesh Rim(System.Action<MeshBuilder> spokes)
        {
            const int segments = 40;
            var builder = new MeshBuilder();
            Lathe(builder, 0, new[] { new Vector2(0.455f, 0.675f), new Vector2(0.46f, 0.64f), new Vector2(0.44f, 0.6f) }, new Vector2(0.3f, 0.63f), segments, 1f);
            Lathe(builder, 0, new[] { new Vector2(0.44f, 0.6f), new Vector2(-0.44f, 0.6f) }, new Vector2(0f, 1f), segments, 1f);
            Lathe(builder, 0, new[] { new Vector2(0.4f, 0f), new Vector2(0.415f, 0.1f), new Vector2(0.4f, 0.16f) }, new Vector2(0f, 0.08f), 20, 1f);
            Lathe(builder, 0, new[] { new Vector2(0.4f, 0.16f), new Vector2(0.33f, 0.16f) }, new Vector2(0.36f, 0f), 20, 1f);
            spokes(builder);
            return builder.ToMesh("Car_Rim");
        }

        /// <summary>Ten spokes in five close pairs.</summary>
        static void SplitTenSpoke(MeshBuilder builder)
        {
            for (int spoke = 0; spoke < 5; spoke++)
            {
                foreach (float split in new[] { -7f, 7f })
                {
                    float angle = (spoke * 72f + split) * Mathf.Deg2Rad;
                    AddSpoke(builder, angle, angle);
                }
            }
        }

        /// <summary>Five stems that fork into two arms each halfway out.</summary>
        static void YSpoke(MeshBuilder builder)
        {
            for (int spoke = 0; spoke < 5; spoke++)
            {
                float angle = spoke * 72f * Mathf.Deg2Rad;
                AddSpoke(builder, angle, angle, outer: 0.4f, innerHalf: 0.04f, outerHalf: 0.034f, outerX: 0.43f);
                foreach (float fork in new[] { -13f, 13f })
                    AddSpoke(builder, angle, angle + fork * Mathf.Deg2Rad, inner: 0.37f, innerHalf: 0.022f, outerHalf: 0.02f, innerX: 0.43f);
            }
        }

        /// <summary>Twenty thin spokes crossing each other in a lattice.</summary>
        static void CrossSpoke(MeshBuilder builder)
        {
            for (int spoke = 0; spoke < 10; spoke++)
            {
                float angle = spoke * 36f * Mathf.Deg2Rad;
                foreach (float lean in new[] { -22f, 22f })
                    AddSpoke(builder, angle, angle + lean * Mathf.Deg2Rad, innerHalf: 0.016f, outerHalf: 0.013f, innerX: lean < 0f ? 0.41f : 0.4f,
                        outerX: lean < 0f ? 0.45f : 0.44f, depth: 0.05f);
            }
        }

        /// <summary>Seven broad, swept blades.</summary>
        static void Turbine(MeshBuilder builder)
        {
            for (int spoke = 0; spoke < 7; spoke++)
            {
                float angle = spoke * (360f / 7f) * Mathf.Deg2Rad;
                AddSpoke(builder, angle, angle + 30f * Mathf.Deg2Rad, innerHalf: 0.04f, outerHalf: 0.075f, depth: 0.06f);
            }
        }

        /// <summary>Tapered, slightly dished spoke from the hub (at <paramref name="innerAngle"/>) out to the barrel.</summary>
        static void AddSpoke(MeshBuilder builder, float innerAngle, float outerAngle, float inner = 0.15f, float outer = 0.61f,
            float innerHalf = 0.03f, float outerHalf = 0.025f, float innerX = 0.41f, float outerX = 0.45f, float depth = 0.08f)
        {
            Vector3 Point(float angle, float radius, float halfWidth, float x) =>
                new Vector3(x, 0f, 0f) + Radial(angle) * radius + Radial(angle + Mathf.PI * 0.5f) * halfWidth;
            var corners = new[]
            {
                Point(innerAngle, inner, -innerHalf, innerX), Point(innerAngle, inner, innerHalf, innerX),
                Point(outerAngle, outer, outerHalf, outerX), Point(outerAngle, outer, -outerHalf, outerX), // front
                Point(innerAngle, inner, -innerHalf, innerX - depth), Point(innerAngle, inner, innerHalf, innerX - depth),
                Point(outerAngle, outer, outerHalf, outerX - depth * 0.9f), Point(outerAngle, outer, -outerHalf, outerX - depth * 0.9f) // back
            };
            Vector3 center = (corners[0] + corners[6]) * 0.5f;
            void Face(int a, int b, int c, int d)
            {
                Vector3 facing = (corners[a] + corners[b] + corners[c] + corners[d]) * 0.25f - center;
                builder.AddQuad(0, corners[a], corners[b], corners[c], corners[d], Vector2.zero, Vector2.right, Vector2.one, Vector2.up, facing);
            }
            Face(0, 1, 2, 3); // outer face
            Face(4, 5, 6, 7);
            Face(1, 5, 6, 2); // sides
            Face(0, 4, 7, 3);
        }

        /// <summary>Drilled disc with a hat: friction ring faces, outer edge, hat wall and hat face.</summary>
        static Mesh BrakeDisc()
        {
            const int segments = 36;
            var builder = new MeshBuilder();
            Lathe(builder, 0, new[] { new Vector2(0.22f, 0.25f), new Vector2(0.22f, 0.53f) }, new Vector2(0.17f, 0.4f), segments, 12f);  // outer face
            Lathe(builder, 0, new[] { new Vector2(0.12f, 0.25f), new Vector2(0.12f, 0.53f) }, new Vector2(0.17f, 0.4f), segments, 12f);  // inner face
            Lathe(builder, 0, new[] { new Vector2(0.22f, 0.53f), new Vector2(0.12f, 0.53f) }, new Vector2(0.17f, 0.4f), segments, 12f, 1f, 1f);   // edge
            Lathe(builder, 0, new[] { new Vector2(0.22f, 0.25f), new Vector2(0.3f, 0.25f), new Vector2(0.3f, 0f) }, new Vector2(0.2f, 0f), 24, 12f, 0.05f, 0.05f); // hat
            return builder.ToMesh("Car_BrakeDisc");
        }

        /// <summary>Caliper straddling the disc at the top rear of the wheel.</summary>
        static Mesh Caliper()
        {
            var builder = new MeshBuilder();
            const float start = -50f, end = -10f; // from +Y (top) towards -Z (rear): the top rear of the disc
            var section = new[] { new Vector2(0.27f, 0.43f), new Vector2(0.27f, 0.575f), new Vector2(0.075f, 0.575f), new Vector2(0.075f, 0.43f), new Vector2(0.27f, 0.43f) };
            Lathe(builder, 0, section, new Vector2(0.17f, 0.5f), 8, 1f, startDegrees: start, endDegrees: end);
            // End caps face away from the arc.
            foreach (float degrees in new[] { start, end })
            {
                float angle = degrees * Mathf.Deg2Rad;
                Vector3 P(Vector2 p) => new Vector3(p.x, 0f, 0f) + Radial(angle) * p.y;
                Vector3 facing = Radial(angle + Mathf.PI * 0.5f) * (degrees == start ? -1f : 1f);
                builder.AddQuad(0, P(section[0]), P(section[1]), P(section[2]), P(section[3]), Vector2.zero, Vector2.right, Vector2.one, Vector2.up, facing);
            }
            return builder.ToMesh("Car_Caliper");
        }

        /// <summary>
        /// Placeholder cabin in car space, fitted over the cabin collider: raked windscreen and rear window, side
        /// windows with a B-pillar, all glass framed by body-coloured pillars, roof and belt line.
        /// </summary>
        static (Mesh glass, Mesh frame) Greenhouse()
        {
            var glass = new MeshBuilder();
            var frame = new MeshBuilder();
            const float baseY = 0.75f, roofY = 1.36f, baseHalf = 0.7f, roofHalf = 0.56f;
            const float baseFront = 0.85f, baseRear = -1.3f, roofFront = 0.15f, roofRear = -0.75f;

            Vector3 BL(float side) => new Vector3(side * baseHalf, baseY, baseFront);
            Vector3 BR(float side) => new Vector3(side * baseHalf, baseY, baseRear);
            Vector3 TL(float side) => new Vector3(side * roofHalf, roofY, roofFront);
            Vector3 TR(float side) => new Vector3(side * roofHalf, roofY, roofRear);

            // Windscreen (s across, t up), rear window, and the two sides.
            Panel(glass, frame, BL(-1f), BL(1f), TL(1f), TL(-1f), new[] { 0f, 0.06f, 0.94f, 1f }, new[] { 0f, 0.05f, 0.95f, 1f },
                (s, t) => s == 1 && t == 1, new Vector3(0f, 0.7f, 0.7f));
            Panel(glass, frame, BR(1f), BR(-1f), TR(-1f), TR(1f), new[] { 0f, 0.08f, 0.92f, 1f }, new[] { 0f, 0.08f, 0.93f, 1f },
                (s, t) => s == 1 && t == 1, new Vector3(0f, 0.7f, -0.7f));
            foreach (float side in new[] { -1f, 1f })
            {
                Panel(glass, frame, BR(side), BL(side), TL(side), TR(side), new[] { 0f, 0.05f, 0.4f, 0.46f, 0.95f, 1f }, new[] { 0f, 0.1f, 0.92f, 1f },
                    (s, t) => t == 1 && (s == 1 || s == 3), new Vector3(side, 0.2f, 0f));
            }
            frame.AddQuad(0, TR(-1f), TR(1f), TL(1f), TL(-1f), Vector2.zero, Vector2.right, Vector2.one, Vector2.up, Vector3.up);
            return (glass.ToMesh("Car_GreenhouseGlass"), frame.ToMesh("Car_GreenhouseFrame"));
        }

        /// <summary>Splits the planar quad a-b-c-d into a grid; cells chosen by <paramref name="isGlass"/> are glass, the rest frame.</summary>
        static void Panel(MeshBuilder glass, MeshBuilder frame, Vector3 a, Vector3 b, Vector3 c, Vector3 d, float[] sBreaks, float[] tBreaks,
            System.Func<int, int, bool> isGlass, Vector3 facing)
        {
            Vector3 P(float s, float t) => Vector3.Lerp(Vector3.Lerp(a, b, s), Vector3.Lerp(d, c, s), t);
            for (int i = 0; i < sBreaks.Length - 1; i++)
            for (int j = 0; j < tBreaks.Length - 1; j++)
            {
                float s0 = sBreaks[i], s1 = sBreaks[i + 1], t0 = tBreaks[j], t1 = tBreaks[j + 1];
                var builder = isGlass(i, j) ? glass : frame;
                builder.AddQuad(0, P(s0, t0), P(s1, t0), P(s1, t1), P(s0, t1),
                    new Vector2(s0, t0), new Vector2(s1, t0), new Vector2(s1, t1), new Vector2(s0, t1), facing);
            }
        }

        static Mesh Save(Mesh mesh, string name)
        {
            mesh.name = name;
            string path = $"{MeshFolder}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                existing = mesh;
            }
            else
            {
                EditorUtility.CopySerialized(mesh, existing);
                Object.DestroyImmediate(mesh);
            }
            var serialized = new SerializedObject(existing);
            serialized.FindProperty("m_IsReadable").boolValue = false; // GPU-only: no CPU copy at runtime
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return existing;
        }
    }
}
