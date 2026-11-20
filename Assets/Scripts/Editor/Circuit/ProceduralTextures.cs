using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace RealisticCarRacing.EditorTools.Circuit
{
    /// <summary>
    /// Generates the circuit's tileable textures (PNG, deterministic) and imports them with mobile overrides.
    /// Existing files are reused; delete Assets/Textures/Circuit to regenerate.
    /// </summary>
    public static class ProceduralTextures
    {
        const string Folder = "Assets/Textures/Circuit";

        public sealed class Set
        {
            public Texture2D asphalt, asphaltNormal, asphaltMask, asphaltDetail, asphaltDetailNormal;
            public Texture2D grassMacro, grassDetail, grassNormal;
            public Texture2D gravel, gravelNormal;
            public Texture2D concrete, concreteNormal;
            public Texture2D barrier, barrierNormal, barrierMask, kerb, checker, fence, clouds;
            public Texture2D officeFacade, residentialFacade, garageFacade, officeMask, residentialMask, garageMask;
            public Texture2D roof, crowd, bark, leaves, lightLeaves, pineLeaves, leavesNormal;
        }

        /// <param name="barrierPaintedFraction">Fraction of the barrier profile (U) on the track-facing side that is painted.</param>
        /// <param name="roadWidth">Road width in metres; positions the painted edge lines.</param>
        public static Set LoadOrCreate(float barrierPaintedFraction, float roadWidth)
        {
            SceneBuildUtility.EnsureFolder(Folder);
            return new Set
            {
                asphalt = Get("Asphalt_v2", 1024, TextureKind.Color, (size, px) => Asphalt(size, px, roadWidth)),
                asphaltNormal = Get("Asphalt_Normal_v2", 1024, TextureKind.Normal, (size, px) => HeightToNormal(size, px, (u, v) => AsphaltHeight(u, v, roadWidth), 3.5f)),
                asphaltMask = Get("Asphalt_Mask", 512, TextureKind.Linear, (size, px) => AsphaltMask(size, px, roadWidth)),
                asphaltDetail = Get("Asphalt_Detail", 256, TextureKind.Linear, AsphaltDetail),
                asphaltDetailNormal = Get("Asphalt_DetailNormal", 256, TextureKind.Normal, (size, px) => HeightToNormal(size, px, AsphaltDetailHeight, 2f)),
                grassMacro = Get("Grass_Macro", 256, TextureKind.Color, GrassMacro),
                grassDetail = Get("Grass_Detail", 512, TextureKind.Color, GrassDetail),
                grassNormal = Get("Grass_Normal", 512, TextureKind.Normal, (size, px) => HeightToNormal(size, px, GrassHeight, 1.5f)),
                gravel = Get("Gravel", 512, TextureKind.Color, Gravel),
                gravelNormal = Get("Gravel_Normal", 512, TextureKind.Normal, (size, px) => HeightToNormal(size, px, GravelHeight, 4f)),
                concrete = Get("Concrete", 512, TextureKind.Color, Concrete),
                concreteNormal = Get("Concrete_Normal", 512, TextureKind.Normal, (size, px) => HeightToNormal(size, px, ConcreteHeight, 1f)),
                barrier = Get("Barrier_v2", 512, TextureKind.Color, (size, px) => Barrier(size, px, barrierPaintedFraction)),
                barrierNormal = Get("Barrier_Normal", 512, TextureKind.Normal, (size, px) => HeightToNormal(size, px, (u, v) => BarrierHeight(u, v, barrierPaintedFraction), 1.5f)),
                barrierMask = Get("Barrier_Mask", 256, TextureKind.Linear, (size, px) => BarrierMask(size, px, barrierPaintedFraction)),
                fence = Get("CatchFence", 512, TextureKind.ColorAlphaTest, CatchFence),
                clouds = Get("Clouds", 512, TextureKind.Linear, Clouds),
                kerb = Get("Kerb", 256, TextureKind.Color, Kerb),
                checker = Get("Checker", 64, TextureKind.ColorPoint, Checker),
                officeFacade = Get("Facade_Office", 512, TextureKind.Color, (size, px) => Facade(size, px, 4, 4, 0.78f, 0.7f, new Color(0.78f, 0.78f, 0.76f))),
                residentialFacade = Get("Facade_Residential", 512, TextureKind.Color, (size, px) => Facade(size, px, 4, 5, 0.45f, 0.5f, new Color(0.86f, 0.8f, 0.7f))),
                officeMask = Get("Facade_Office_Mask", 512, TextureKind.Linear, (size, px) => FacadeMask(size, px, 4, 4, 0.78f, 0.7f)),
                residentialMask = Get("Facade_Residential_Mask", 512, TextureKind.Linear, (size, px) => FacadeMask(size, px, 4, 5, 0.45f, 0.5f)),
                garageFacade = Get("Facade_Garage", 512, TextureKind.Color, Garage),
                garageMask = Get("Facade_Garage_Mask", 512, TextureKind.Linear, GarageMask),
                roof = Get("Roof", 256, TextureKind.Color, Roof),
                crowd = Get("Grandstand_Crowd", 256, TextureKind.Color, Crowd),
                bark = Get("Bark", 128, TextureKind.Color, Bark),
                leaves = Get("Leaves_v2", 256, TextureKind.Color, (size, px) => Leaves(size, px, new Color(0.2f, 0.33f, 0.11f), 131)),
                lightLeaves = Get("Leaves_Light", 256, TextureKind.Color, (size, px) => Leaves(size, px, new Color(0.31f, 0.41f, 0.14f), 141)),
                pineLeaves = Get("Leaves_Pine_v2", 256, TextureKind.Color, (size, px) => Leaves(size, px, new Color(0.1f, 0.21f, 0.11f), 151)),
                leavesNormal = Get("Leaves_Normal", 256, TextureKind.Normal, (size, px) => HeightToNormal(size, px, (u, v) => LeafHeight(u, v, 131), 3f))
            };
        }

        internal enum TextureKind { Color, ColorPoint, Linear, Normal, ColorAlphaTest }

        static Texture2D Get(string name, int size, TextureKind kind, Action<int, Color[]> generate) => Get(Folder, name, size, kind, generate);

        /// <summary>Loads <paramref name="folder"/>/<paramref name="name"/>.png, generating and importing it first if missing.</summary>
        internal static Texture2D Get(string folder, string name, int size, TextureKind kind, Action<int, Color[]> generate)
        {
            SceneBuildUtility.EnsureFolder(folder);
            string path = $"{folder}/{name}.png";
            if (File.Exists(path) && AssetImporter.GetAtPath(path) is TextureImporter existing && !existing.streamingMipmaps)
                ConfigureImporter(path, size, kind); // upgrade textures generated before mip streaming was enabled
            if (!File.Exists(path))
            {
                var pixels = new Color[size * size];
                generate(size, pixels);
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, kind == TextureKind.Linear || kind == TextureKind.Normal);
                texture.SetPixels(pixels);
                File.WriteAllBytes(path, texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path);
                ConfigureImporter(path, size, kind);
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static void ConfigureImporter(string path, int size, TextureKind kind)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = kind == TextureKind.Normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = kind == TextureKind.Color || kind == TextureKind.ColorPoint || kind == TextureKind.ColorAlphaTest;
            // Cut-out textures keep their coverage in smaller mips, so thin wires don't fade away with distance.
            importer.alphaIsTransparency = kind == TextureKind.ColorAlphaTest;
            importer.mipMapsPreserveCoverage = kind == TextureKind.ColorAlphaTest;
            importer.alphaTestReferenceValue = 0.5f;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.mipmapEnabled = true;
            importer.filterMode = kind == TextureKind.ColorPoint ? FilterMode.Point : FilterMode.Trilinear;
            importer.anisoLevel = size >= 512 ? 8 : 4;
            importer.maxTextureSize = size;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            // Mip streaming: only the mip levels the camera needs stay in memory (budget per graphics preset).
            importer.streamingMipmaps = true;

            // Phones: half resolution (min 128) with ASTC, which every supported Android/iOS GPU decodes in hardware.
            int mobileSize = Mathf.Max(Mathf.Min(size, 128), size / 2);
            foreach (string platform in new[] { "Android", "iPhone" })
            {
                importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
                {
                    name = platform,
                    overridden = true,
                    maxTextureSize = mobileSize,
                    format = kind == TextureKind.Normal ? TextureImporterFormat.ASTC_4x4 : TextureImporterFormat.ASTC_6x6
                });
            }
            importer.SaveAndReimport();
        }

        // ---------- Noise ----------

        /// <summary>Tileable value noise: the lattice wraps every <paramref name="period"/> cells.</summary>
        internal static float ValueNoise(float x, float y, int period, int seed)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float xf = x - xi, yf = y - yi;
            float u = xf * xf * (3f - 2f * xf), v = yf * yf * (3f - 2f * yf);
            float a = Hash(xi, yi, period, seed), b = Hash(xi + 1, yi, period, seed);
            float c = Hash(xi, yi + 1, period, seed), d = Hash(xi + 1, yi + 1, period, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }

        internal static float Hash(int x, int y, int period, int seed)
        {
            x = ((x % period) + period) % period;
            y = ((y % period) + period) % period;
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 144665);
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0xFFFFFF;
            }
        }

        /// <summary>Fractal noise over a texture in 0..1 UV space; tiles seamlessly.</summary>
        internal static float Fbm(float u, float v, int baseFrequency, int octaves, int seed, float persistence = 0.5f)
        {
            float sum = 0f, amplitude = 1f, total = 0f;
            int frequency = baseFrequency;
            for (int i = 0; i < octaves; i++)
            {
                sum += ValueNoise(u * frequency, v * frequency, frequency, seed + i * 31) * amplitude;
                total += amplitude;
                amplitude *= persistence;
                frequency *= 2;
            }
            return sum / total;
        }

        internal static void ForEach(int size, Color[] pixels, Func<float, float, Color> shade)
        {
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                pixels[y * size + x] = shade((x + 0.5f) / size, (y + 0.5f) / size);
        }

        internal static void HeightToNormal(int size, Color[] pixels, Func<float, float, float> height, float strength)
        {
            var heights = new float[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                heights[y * size + x] = height((x + 0.5f) / size, (y + 0.5f) / size);

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = heights[y * size + (x + 1) % size] - heights[y * size + (x - 1 + size) % size];
                float dy = heights[((y + 1) % size) * size + x] - heights[((y - 1 + size) % size) * size + x];
                var normal = new Vector3(-dx * strength * size / 256f, -dy * strength * size / 256f, 1f).normalized;
                pixels[y * size + x] = new Color(normal.x * 0.5f + 0.5f, normal.y * 0.5f + 0.5f, normal.z * 0.5f + 0.5f, 1f);
            }
        }

        internal static float Grain(float u, float v, int frequency, int seed) => Hash(Mathf.FloorToInt(u * frequency), Mathf.FloorToInt(v * frequency), frequency, seed);

        // ---------- Surfaces ----------

        // Road texture: U spans the full road width, V one square tile along it (so one texel ~1.2 cm).

        static bool IsEdgeLine(float u, float roadWidth)
        {
            float lineInset = 0.3f / roadWidth, lineWidth = 0.22f / roadWidth;
            float edge = Mathf.Min(u, 1f - u);
            return edge > lineInset && edge < lineInset + lineWidth;
        }

        /// <summary>Resurfaced rectangles: a coarse grid of cells, some patched with newer, darker asphalt.</summary>
        static bool IsRepairPatch(float u, float v, out float border)
        {
            int cu = Mathf.FloorToInt(u * 3f), cv = Mathf.FloorToInt(v * 4f);
            border = 1f;
            if (Hash(cu, cv, 4096, 7) > 0.22f)
                return false;
            float fu = u * 3f - cu, fv = v * 4f - cv;
            float w = 0.15f + Hash(cu, cv, 4096, 8) * 0.25f, l = 0.2f + Hash(cu, cv, 4096, 9) * 0.5f;
            float du = Mathf.Abs(fu - 0.5f), dv = Mathf.Abs(fv - 0.5f);
            border = Mathf.Min(w - du, l - dv);
            return du < w && dv < l;
        }

        /// <summary>Thin meandering cracks from ridged noise.</summary>
        static float Crack(float u, float v) => Mathf.Abs(Fbm(u, v, 8, 3, 13) - 0.5f) < 0.006f ? 1f : 0f;

        /// <summary>Darker rubbered-in band where cars drive.</summary>
        static float Rubber(float u) => Mathf.Exp(-Mathf.Pow((Mathf.Abs(u - 0.5f) - 0.17f) / 0.1f, 2f));

        static float AsphaltHeight(float u, float v, float roadWidth)
        {
            float stones = Mathf.Pow(Grain(u, v, 512, 12), 3f);
            float fine = Grain(u, v, 1024, 14);
            float h = Fbm(u, v, 64, 3, 11) * 0.3f + stones * 0.5f + fine * 0.2f;
            if (IsRepairPatch(u, v, out float border))
                h = h * 0.6f + 0.25f + (border < 0.01f ? -0.3f : 0f);
            h -= Crack(u, v) * 0.6f;
            if (IsEdgeLine(u, roadWidth))
                h = h * 0.3f + 0.45f; // paint fills the texture
            return h;
        }

        static void Asphalt(int size, Color[] pixels, float roadWidth) => ForEach(size, pixels, (u, v) =>
        {
            float patches = Fbm(u, v, 4, 3, 3);
            float stones = Grain(u, v, 512, 12);
            float tone = 0.15f + patches * 0.06f + (stones > 0.9f ? 0.1f : stones < 0.06f ? -0.04f : 0f) + (Grain(u, v, 1024, 14) - 0.5f) * 0.03f;
            if (IsRepairPatch(u, v, out float border))
                tone = border < 0.01f ? 0.05f : 0.11f + (stones > 0.92f ? 0.05f : 0f); // fresh patch with a sealed seam
            tone -= Rubber(u) * 0.035f;
            tone -= Crack(u, v) * 0.07f;
            float oil = Mathf.SmoothStep(0.72f, 0.8f, Fbm(u, v, 6, 2, 15)) * Rubber(u) * 0.04f;
            var color = new Color(tone - oil, tone - oil, tone * 1.04f - oil, 1f);

            if (IsEdgeLine(u, roadWidth))
            {
                float wear = Fbm(u, v, 32, 2, 9);
                color = Color.Lerp(new Color(0.78f, 0.78f, 0.76f), color, wear > 0.7f ? 0.65f : 0.04f);
            }
            return color;
        });

        /// <summary>Metallic (R, always 0) and smoothness (A): aggregate glints, polished racing line, glossier paint.</summary>
        static void AsphaltMask(int size, Color[] pixels, float roadWidth) => ForEach(size, pixels, (u, v) =>
        {
            float smoothness = 0.16f + Rubber(u) * 0.14f + (Grain(u, v, 256, 12) > 0.88f ? 0.25f : 0f);
            if (IsRepairPatch(u, v, out _))
                smoothness += 0.06f;
            if (IsEdgeLine(u, roadWidth))
                smoothness = 0.42f;
            return new Color(0f, 0f, 0f, smoothness);
        });

        /// <summary>Close-up detail (multiplied ×2, so 0.5 is neutral): millimetre-scale grit.</summary>
        static void AsphaltDetail(int size, Color[] pixels) => ForEach(size, pixels, (u, v) =>
        {
            float g = 0.5f + (Grain(u, v, 128, 16) - 0.5f) * 0.14f;
            return new Color(g, g, g, 1f);
        });

        static float AsphaltDetailHeight(float u, float v) => Mathf.Pow(Grain(u, v, 128, 16), 2f) * 0.7f + Fbm(u, v, 32, 2, 17) * 0.3f;

        static void GrassMacro(int size, Color[] pixels) => ForEach(size, pixels, (u, v) =>
        {
            float n = Fbm(u, v, 4, 4, 21);
            float dry = Mathf.SmoothStep(0.55f, 0.75f, Fbm(u, v, 8, 3, 22));
            var lush = Color.Lerp(new Color(0.24f, 0.38f, 0.14f), new Color(0.33f, 0.47f, 0.19f), n);
            return Color.Lerp(lush, new Color(0.47f, 0.48f, 0.26f), dry * 0.6f);
        });

        static float GrassHeight(float u, float v) => Grain(u, v, 256, 31) * 0.6f + Fbm(u, v, 32, 2, 32) * 0.4f;

        static void GrassDetail(int size, Color[] pixels) => ForEach(size, pixels, (u, v) =>
        {
            // Detail maps multiply by 2, so 0.5 grey is neutral.
            float blades = Grain(u, v * 0.5f, 256, 33);
            float clumps = Fbm(u, v, 16, 3, 34);
            float g = 0.38f + blades * 0.2f + clumps * 0.12f;
            return new Color(g * 0.95f, g * 1.05f, g * 0.9f, 1f);
        });

        static float GravelHeight(float u, float v) => Mathf.Pow(Grain(u, v, 128, 41), 2f) * 0.7f + Fbm(u, v, 64, 2, 42) * 0.3f;

        static void Gravel(int size, Color[] pixels) => ForEach(size, pixels, (u, v) =>
        {
            float stones = Grain(u, v, 128, 41);
            float tint = Fbm(u, v, 8, 2, 43);
            float g = 0.5f + stones * 0.25f + tint * 0.1f;
            return new Color(g * 0.95f, g * 0.88f, g * 0.75f, 1f);
        });

        static float ConcreteHeight(float u, float v) => Fbm(u, v, 32, 3, 51);

        static void Concrete(int size, Color[] pixels) => ForEach(size, pixels, (u, v) =>
        {
            float g = 0.62f + Fbm(u, v, 8, 4, 52) * 0.12f + (Grain(u, v, 256, 53) - 0.5f) * 0.05f;
            return new Color(g, g, g * 0.98f, 1f);
        });

        // Barrier: U across the profile (0 = track-side foot ... 1 = far foot), V along 8 m (two 4 m precast sections).
        // The track-facing side (U below the painted fraction) is painted in 4 m red/white blocks.

        static bool IsBarrierJoint(float v) => Mathf.Abs(v - 0.5f) < 0.004f || v < 0.004f || v > 0.996f;

        /// <summary>Black rubber scuffs left by cars on the lower painted face.</summary>
        static float Scuff(float u, float v, float paintedFraction)
        {
            if (u > paintedFraction * 0.6f || u < paintedFraction * 0.1f)
                return 0f;
            float streak = Fbm(u * 0.3f, v, 16, 3, 63);
            return Mathf.SmoothStep(0.62f, 0.75f, streak) * Fbm(0.5f, v, 4, 2, 64);
        }

        static float BarrierHeight(float u, float v, float paintedFraction)
        {
            float h = Fbm(u, v, 16, 3, 61) * 0.3f + Mathf.Pow(Grain(u, v, 256, 65), 4f) * -0.3f; // pores
            if (u < paintedFraction && Fbm(u, v, 64, 2, 62) > 0.75f)
                h -= 0.2f; // paint chips
            if (IsBarrierJoint(v))
                h -= 0.8f;
            return h;
        }

        static void Barrier(int size, Color[] pixels, float paintedFraction) => ForEach(size, pixels, (u, v) =>
        {
            float g = 0.6f + Fbm(u, v, 8, 3, 61) * 0.1f - Mathf.Pow(Grain(u, v, 256, 65), 4f) * 0.1f;
            var concrete = new Color(g, g, g * 0.98f, 1f);
            var color = concrete;
            if (u < paintedFraction)
            {
                var paint = v < 0.5f ? new Color(0.68f, 0.06f, 0.05f) : new Color(0.88f, 0.88f, 0.86f);
                color = Color.Lerp(paint, concrete, Fbm(u, v, 64, 2, 62) > 0.75f ? 0.75f : 0f);
                color = Color.Lerp(color, new Color(0.05f, 0.05f, 0.05f), Scuff(u, v, paintedFraction) * 0.8f);
            }
            // Road grime splashed up the foot of the wall on both sides.
            float foot = Mathf.Min(u, 1f - u);
            color = Color.Lerp(color, new Color(0.3f, 0.28f, 0.24f), Mathf.SmoothStep(0.12f, 0f, foot) * (0.4f + Fbm(u, v, 16, 2, 66) * 0.4f));
            if (IsBarrierJoint(v))
                color *= 0.35f;
            return color;
        });

        static void BarrierMask(int size, Color[] pixels, float paintedFraction) => ForEach(size, pixels, (u, v) =>
        {
            float smoothness = u < paintedFraction ? 0.42f - Scuff(u, v, paintedFraction) * 0.15f : 0.14f;
            return new Color(0f, 0f, 0f, smoothness);
        });

        /// <summary>
        /// Debris fence: U up the fence (2.4 m), V along it (4 m): a post at each end, top and bottom rails, and
        /// 10 cm chain-link diamonds. Alpha is the cut-out mask (1 = wire).
        /// </summary>
        static void CatchFence(int size, Color[] pixels) => ForEach(size, pixels, (u, v) =>
        {
            bool post = v < 0.015f || v > 0.985f;
            bool rail = u < 0.025f || u > 0.975f || Mathf.Abs(u - 0.5f) < 0.01f;
            if (post || rail)
            {
                float g = post ? 0.32f : 0.45f;
                return new Color(g, g, g * 1.03f, 1f);
            }
            float x = v * 40f, y = u * 24f; // 4 m / 0.1 m and 2.4 m / 0.1 m
            float a = Mathf.Abs((x + y) % 1f - 0.5f), b = Mathf.Abs((x - y + 100f) % 1f - 0.5f);
            bool wire = a > 0.42f || b > 0.42f;
            float shade = 0.5f + Grain(u, v, 64, 67) * 0.1f;
            return wire ? new Color(shade, shade, shade * 1.03f, 1f) : new Color(shade, shade, shade, 0f);
        });

        /// <summary>Tiling cloud density (R) for the sky shader: billowy fractal noise.</summary>
        static void Clouds(int size, Color[] pixels) => ForEach(size, pixels, (u, v) =>
        {
            float n = Fbm(u, v, 4, 6, 171, 0.55f);
            float billow = 1f - Mathf.Abs(Fbm(u, v, 8, 4, 172) * 2f - 1f);
            float d = Mathf.Clamp01(n * 0.75f + billow * 0.25f);
            return new Color(d, d, d, 1f);
        });

        /// <summary>V along 2 m: 1 m red, 1 m white, with a dirty inner edge.</summary>
        static void Kerb(int size, Color[] pixels) => ForEach(size, pixels, (u, v) =>
        {
            var color = v < 0.5f ? new Color(0.75f, 0.08f, 0.06f) : new Color(0.92f, 0.92f, 0.9f);
            float dirt = Fbm(u, v, 16, 3, 71) * (1f - u) * 0.5f;
            return Color.Lerp(color, new Color(0.15f, 0.15f, 0.15f), dirt);
        });

        /// <summary>2×2 checker; one tile = 1 m, so squares are 0.5 m.</summary>
        static void Checker(int size, Color[] pixels) => ForEach(size, pixels, (u, v) =>
            (u < 0.5f) == (v < 0.5f) ? new Color(0.95f, 0.95f, 0.95f) : new Color(0.04f, 0.04f, 0.04f));

        // ---------- Buildings ----------

        static bool IsWindow(float u, float v, int columns, int floors, float windowWidth, float windowHeight)
        {
            float cu = u * columns % 1f, cv = v * floors % 1f;
            return Mathf.Abs(cu - 0.5f) < windowWidth * 0.5f && cv > 0.2f && cv < 0.2f + windowHeight;
        }

        static void Facade(int size, Color[] pixels, int columns, int floors, float windowWidth, float windowHeight, Color wall) =>
            ForEach(size, pixels, (u, v) =>
            {
                float grime = Fbm(u, v, 8, 3, 81) * 0.12f;
                if (!IsWindow(u, v, columns, floors, windowWidth, windowHeight))
                    return wall * (1f - grime);
                // Glass: dark, bluish, brighter towards the top of each pane, some panes lit or with blinds.
                float cv = v * floors % 1f;
                float pane = Hash(Mathf.FloorToInt(u * columns), Mathf.FloorToInt(v * floors), 4096, 82);
                var glass = Color.Lerp(new Color(0.06f, 0.09f, 0.13f), new Color(0.2f, 0.26f, 0.33f), (cv - 0.2f) / windowHeight);
                return pane > 0.85f ? new Color(0.55f, 0.5f, 0.42f) : glass;
            });

        static void FacadeMask(int size, Color[] pixels, int columns, int floors, float windowWidth, float windowHeight) =>
            ForEach(size, pixels, (u, v) => IsWindow(u, v, columns, floors, windowWidth, windowHeight)
                ? new Color(0.1f, 0f, 0f, 0.93f)
                : new Color(0f, 0f, 0f, 0.18f));

        /// <summary>Pit garage façade: one tile is 10 m wide × 9 m tall — two roller doors below, a window band above.</summary>
        static void Garage(int size, Color[] pixels) => ForEach(size, pixels, (u, v) =>
        {
            float cu = u * 2f % 1f;
            bool door = v < 0.5f && cu > 0.1f && cu < 0.9f;
            bool windowBand = v > 0.62f && v < 0.88f && cu > 0.04f && cu < 0.96f;
            float grime = Fbm(u, v, 8, 2, 91) * 0.1f;
            if (door)
            {
                float slats = (v * 90f % 1f) < 0.15f ? 0.08f : 0f;
                return new Color(0.42f - slats, 0.44f - slats, 0.47f - slats);
            }
            if (windowBand)
                return Color.Lerp(new Color(0.07f, 0.1f, 0.14f), new Color(0.22f, 0.28f, 0.35f), (v - 0.62f) / 0.26f);
            return v > 0.5f && v < 0.56f ? new Color(0.12f, 0.2f, 0.45f) : new Color(0.88f, 0.88f, 0.86f) * (1f - grime);
        });

        static void GarageMask(int size, Color[] pixels) => ForEach(size, pixels, (u, v) =>
        {
            float cu = u * 2f % 1f;
            bool windowBand = v > 0.62f && v < 0.88f && cu > 0.04f && cu < 0.96f;
            bool door = v < 0.5f && cu > 0.1f && cu < 0.9f;
            return new Color(door ? 0.6f : 0f, 0f, 0f, windowBand ? 0.93f : door ? 0.45f : 0.2f);
        });

        static void Roof(int size, Color[] pixels) => ForEach(size, pixels, (u, v) =>
        {
            float g = 0.3f + Fbm(u, v, 16, 3, 101) * 0.1f + (Grain(u, v, 128, 102) - 0.5f) * 0.06f;
            return new Color(g, g, g * 1.02f, 1f);
        });

        /// <summary>Grandstand seating: U along 4 m (8 seats), V one row; ~65% of seats occupied.</summary>
        static void Crowd(int size, Color[] pixels) => ForEach(size, pixels, (u, v) =>
        {
            int seat = Mathf.FloorToInt(u * 8f);
            float su = u * 8f % 1f;
            float occupied = Hash(seat, 0, 4096, 111);
            var seatColor = new Color(0.1f, 0.25f, 0.6f);
            if (v < 0.25f)
                return new Color(0.55f, 0.55f, 0.55f); // concrete step edge
            if (occupied < 0.65f && su > 0.15f && su < 0.85f && v > 0.3f)
            {
                float shirt = Hash(seat, 1, 4096, 112);
                var clothes = Color.HSVToRGB(shirt, 0.55f + shirt * 0.3f, 0.45f + Hash(seat, 2, 4096, 113) * 0.45f);
                var skin = Color.Lerp(new Color(0.36f, 0.24f, 0.17f), new Color(0.93f, 0.76f, 0.62f), Hash(seat, 3, 4096, 114));
                return v > 0.78f ? skin : clothes;
            }
            return seatColor;
        });

        // ---------- Vegetation ----------

        static void Bark(int size, Color[] pixels) => ForEach(size, pixels, (u, v) =>
        {
            float ridges = Fbm(u * 4f % 1f, v, 4, 3, 121);
            float g = 0.22f + ridges * 0.16f;
            return new Color(g * 1.1f, g * 0.9f, g * 0.7f, 1f);
        });

        /// <summary>Leaf clusters: bright tops of clumps, dark gaps between them (also drives the normal map).</summary>
        static float LeafHeight(float u, float v, int seed)
        {
            float clumps = Fbm(u, v, 8, 3, seed);
            float leaves = Grain(u, v, 64, seed + 1) * 0.5f + Grain(u, v, 128, seed + 2) * 0.5f;
            return clumps * 0.6f + leaves * 0.4f;
        }

        static void Leaves(int size, Color[] pixels, Color baseColor, int seed) => ForEach(size, pixels, (u, v) =>
        {
            float h = LeafHeight(u, v, seed);
            float light = 0.45f + h * 0.95f;
            float hue = (Fbm(u, v, 4, 2, seed + 3) - 0.5f) * 0.25f; // some clumps yellower, some bluer
            return new Color(baseColor.r * light * (1f + hue), baseColor.g * light, baseColor.b * light * (1f - hue), 1f);
        });
    }
}
