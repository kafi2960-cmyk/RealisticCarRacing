using RealisticCarRacing.EditorTools.Circuit;
using RealisticCarRacing.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using TextureKind = RealisticCarRacing.EditorTools.Circuit.ProceduralTextures.TextureKind;

namespace RealisticCarRacing.EditorTools
{
    /// <summary>
    /// Generates the MainMenu scene: a car showroom with the garage turntable, plus the menu controller (which builds
    /// its UI at runtime). It becomes the first scene in the build.
    /// <para>
    /// The room is a seamless cyclorama (floor curving up into the walls) with a polished floor. Its look comes mostly
    /// from things that cost nothing per frame on a phone: overhead light panels, a ring light and wall light strips
    /// are unlit emissive surfaces, and a baked, box-projected reflection probe puts their reflections in the paint,
    /// glass and floor. Real-time lighting is one shadowed key light plus two unshadowed accents, and a soft contact
    /// shadow under the car keeps it grounded when shadows are short or off.
    /// </para>
    /// </summary>
    public static class MenuSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/MainMenu.unity";
        const string MeshFolder = "Assets/Models/Generated/Showroom";
        const string TextureFolder = "Assets/Textures/Showroom";
        const string UnlitShader = "Universal Render Pipeline/Unlit";

        // Room dimensions (m).
        const float FloorRadius = 13f, CoveRadius = 2.5f, WallHeight = 7.5f, CeilingCove = 2f;
        const float TurntableRadius = 3.15f, TurntableHeight = 0.06f;

        [MenuItem("RealisticCarRacing/Create Main Menu Scene")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            GameContentBuilder.Build();
            SceneBuildUtility.EnsureFolder(MeshFolder);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.3f, 0.31f, 0.34f);
            RenderSettings.ambientEquatorColor = new Color(0.15f, 0.15f, 0.16f);
            RenderSettings.ambientGroundColor = new Color(0.05f, 0.05f, 0.055f);
            RenderSettings.fog = false;

            var camera = Camera.main;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.fieldOfView = 30f; // a longer lens, as car photographers use: less distortion of the body
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 60f;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;

            BuildRoom(out var turntable);
            SetUpLights();
            CreateReflectionProbe();
            CreatePostProcessing();

            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystem.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            var showroom = new GameObject("Showroom").AddComponent<ShowroomController>();
            showroom.Configure(turntable);
            var orbit = showroom.gameObject.AddComponent<ShowroomOrbit>();
            orbit.Configure(turntable, camera);
            new GameObject("MainMenu").AddComponent<MainMenuController>().Configure(showroom, orbit);

            EditorSceneManager.SaveScene(scene, ScenePath);
            const string lightingPath = "Assets/Settings/Menu_Lighting.lighting";
            var settings = AssetDatabase.LoadAssetAtPath<LightingSettings>(lightingPath);
            if (settings == null)
            {
                settings = new LightingSettings { name = "Menu_Lighting" };
                AssetDatabase.CreateAsset(settings, lightingPath);
            }
            settings.bakedGI = false;
            settings.realtimeGI = false;
            Lightmapping.lightingSettings = settings;
            Lightmapping.Bake(); // bakes the reflection probe (no lightmaps)
            EditorSceneManager.SaveScene(scene, ScenePath);
            SceneBuildUtility.AddSceneToBuild(ScenePath, first: true);
            AssetDatabase.SaveAssets();
            Debug.Log($"RealisticCarRacing: built {ScenePath}.");
        }

        // ---------- Room ----------

        static void BuildRoom(out Transform turntable)
        {
            var floorAlbedo = ProceduralTextures.Get(TextureFolder, "Floor_Albedo", 512, TextureKind.Color, FloorAlbedo);
            var floorNormal = ProceduralTextures.Get(TextureFolder, "Floor_Normal", 512, TextureKind.Normal,
                (size, px) => ProceduralTextures.HeightToNormal(size, px, FloorHeight, 1.5f));
            var floorMask = ProceduralTextures.Get(TextureFolder, "Floor_Mask", 512, TextureKind.Linear, FloorMask);
            var floor = SceneBuildUtility.LoadOrCreateLit("Showroom_Floor", Color.white, 1f, 0f,
                new SceneBuildUtility.LitMaps { baseMap = floorAlbedo, normalMap = floorNormal, normalScale = 0.35f, metallicGlossMap = floorMask }, "Menu");
            var walls = SceneBuildUtility.LoadOrCreateLit("Showroom_Walls", new Color(0.16f, 0.165f, 0.175f), 0.18f, 0f, default, "Menu");
            var deck = SceneBuildUtility.LoadOrCreateLit("Showroom_Turntable", new Color(0.09f, 0.092f, 0.1f), 0.72f, 0.85f, default, "Menu");

            var room = new GameObject("Room").transform;
            MeshObject("Floor", room, Save(Floor(), "Showroom_Floor"), floor, castShadows: false);
            MeshObject("Cyclorama", room, Save(Cyclorama(), "Showroom_Cyclorama"), walls, castShadows: false);

            // Light fixtures: unlit HDR surfaces. Bloom gives them a glow; the baked probe puts them in every reflection.
            var panelLight = Unlit("Showroom_LightPanel", new Color(1f, 0.98f, 0.95f) * 3.2f);
            var stripLight = Unlit("Showroom_LightStrip", new Color(0.85f, 0.9f, 1f) * 2.2f);
            var accentLight = Unlit("Showroom_AccentLine", new Color(0.95f, 0.22f, 0.12f) * 2.5f);
            var fixtures = new GameObject("LightFixtures").transform;
            MeshObject("RingLight", fixtures, Save(Annulus(2.55f, 2.95f, 5.4f, 72, faceUp: false), "Showroom_RingLight"), panelLight, castShadows: false);
            MeshObject("Softboxes", fixtures, Save(Softboxes(), "Showroom_Softboxes"), panelLight, castShadows: false);
            MeshObject("WallStrips", fixtures, Save(WallStrips(), "Showroom_WallStrips"), stripLight, castShadows: false);
            MeshObject("SkirtingLight", fixtures, Save(SkirtingBand(), "Showroom_SkirtingLight"), accentLight, castShadows: false);

            turntable = new GameObject("Turntable").transform;
            MeshObject("Deck", turntable, Save(Turntable(), "Showroom_Turntable"), deck, castShadows: false);
            MeshObject("DeckLight", turntable, Save(Annulus(TurntableRadius - 0.11f, TurntableRadius - 0.07f, TurntableHeight + 0.0015f, 96, faceUp: true),
                "Showroom_DeckLight"), stripLight, castShadows: false);
            MeshObject("ContactShadow", turntable, Save(ContactShadowQuad(), "Showroom_ContactShadow"), ContactShadowMaterial(), castShadows: false);
        }

        static void MeshObject(string name, Transform parent, Mesh mesh, Material material, bool castShadows)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            GameObjectUtility.SetStaticEditorFlags(go, parent.name == "Turntable"
                ? 0 // turns with the car; also kept out of the baked reflections
                : StaticEditorFlags.ReflectionProbeStatic | StaticEditorFlags.BatchingStatic);
        }

        static Material Unlit(string name, Color hdrColor)
        {
            var material = LoadOrCreate(name, UnlitShader);
            material.SetColor("_BaseColor", hdrColor);
            EditorUtility.SetDirty(material);
            return material;
        }

        static Material LoadOrCreate(string name, string shader)
        {
            string path = $"Assets/Materials/Menu/{name}.mat";
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

        /// <summary>Soft dark patch under the car (ambient occlusion where the body meets the deck).</summary>
        static Material ContactShadowMaterial()
        {
            var texture = ProceduralTextures.Get(TextureFolder, "ContactShadow", 128, TextureKind.Linear, (size, pixels) =>
                ProceduralTextures.ForEach(size, pixels, (u, v) =>
                {
                    // Rounded rectangle falloff: dark under the body, fading out past the wheels.
                    float dx = Mathf.Max(Mathf.Abs(u - 0.5f) * 2f - 0.55f, 0f) / 0.45f;
                    float dy = Mathf.Max(Mathf.Abs(v - 0.5f) * 2f - 0.62f, 0f) / 0.38f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Pow(Mathf.Clamp01(1f - d), 1.8f) * 0.82f;
                    return new Color(1f, 1f, 1f, alpha);
                }));
            var material = LoadOrCreate("Showroom_ContactShadow", UnlitShader);
            material.SetColor("_BaseColor", Color.black);
            material.SetTexture("_BaseMap", texture);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_ZWrite", 0f);
            BaseShaderGUI.SetupMaterialBlendMode(material);
            material.renderQueue = (int)RenderQueue.Transparent - 50; // before the car's glass
            EditorUtility.SetDirty(material);
            return material;
        }

        // ---------- Lights, reflections, post-processing ----------

        static void SetUpLights()
        {
            // Key: high and in front, like the big overhead softbox it stands for. The only shadow-casting light.
            var key = Object.FindAnyObjectByType<Light>();
            key.name = "KeyLight";
            key.type = LightType.Directional;
            key.intensity = 1.5f;
            key.color = new Color(1f, 0.97f, 0.93f);
            key.transform.rotation = Quaternion.Euler(64f, 20f, 0f);
            key.shadows = LightShadows.Soft;
            key.shadowStrength = 0.85f;

            // Accents for the silhouette (no shadows; per-vertex on Low).
            Spot("RimLight", new Vector3(-5.5f, 4.2f, 5f), new Color(0.75f, 0.85f, 1f), 26f, 48f);
            Spot("KickerLight", new Vector3(6f, 3f, -2.5f), new Color(1f, 0.93f, 0.85f), 14f, 55f);
        }

        static void Spot(string name, Vector3 position, Color color, float intensity, float angle)
        {
            var light = new GameObject(name).AddComponent<Light>();
            light.type = LightType.Spot;
            light.transform.position = position;
            light.transform.LookAt(new Vector3(0f, 0.6f, 0f));
            light.color = color;
            light.intensity = intensity;
            light.range = 16f;
            light.spotAngle = angle;
            light.shadows = LightShadows.None;
        }

        static void CreateReflectionProbe()
        {
            var probe = new GameObject("ReflectionProbe").AddComponent<ReflectionProbe>();
            probe.transform.position = new Vector3(0f, 1.2f, 0f);
            probe.mode = ReflectionProbeMode.Baked;
            probe.boxProjection = true; // reflections line up with the room (High; plain cube map elsewhere)
            float extent = (FloorRadius + CoveRadius) * 2f;
            probe.size = new Vector3(extent, WallHeight + CeilingCove + 1f, extent);
            probe.center = new Vector3(0f, (WallHeight + CeilingCove) * 0.5f - 1.2f, 0f);
            probe.resolution = 256; // the car fills the screen here: sharper reflections than on the track
            probe.hdr = true;
            probe.importance = 1;
        }

        static void CreatePostProcessing()
        {
            const string path = "Assets/Settings/Menu_PostProcess.asset";
            AssetDatabase.DeleteAsset(path);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
            profile.Add<Tonemapping>(true).mode.value = TonemappingMode.ACES;
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.value = 1f;
            bloom.intensity.value = 0.45f;
            bloom.scatter.value = 0.6f;
            profile.Add<Vignette>(true).intensity.value = 0.3f;
            var color = profile.Add<ColorAdjustments>(true);
            color.postExposure.value = 0.35f;
            color.contrast.value = 8f;
            foreach (var component in profile.components)
                AssetDatabase.AddObjectToAsset(component, profile);
            EditorUtility.SetDirty(profile);

            var volume = new GameObject("PostProcessing").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
        }

        // ---------- Meshes ----------

        /// <summary>
        /// Lathes a profile of (radius, height) points around the Y axis. The profile runs outwards/upwards, so the
        /// left-hand normal of each step faces into the room. <paramref name="uv"/> maps a world position to UVs.
        /// </summary>
        static void LatheY(MeshBuilder builder, Vector2[] profile, int segments, System.Func<Vector3, float, Vector2> uv)
        {
            var grid = new int[segments + 1, profile.Length];
            float run = 0f;
            var runs = new float[profile.Length];
            for (int k = 1; k < profile.Length; k++)
                runs[k] = run += Vector2.Distance(profile[k - 1], profile[k]);
            for (int s = 0; s <= segments; s++)
            {
                float angle = s * 2f * Mathf.PI / segments;
                var radial = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                for (int k = 0; k < profile.Length; k++)
                {
                    Vector3 position = radial * profile[k].x + Vector3.up * profile[k].y;
                    grid[s, k] = builder.AddVertex(position, uv(position, runs[k]));
                }
            }
            for (int s = 0; s < segments; s++)
            {
                float angle = (s + 0.5f) * 2f * Mathf.PI / segments;
                var radial = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                for (int k = 0; k < profile.Length - 1; k++)
                {
                    Vector2 step = profile[k + 1] - profile[k];
                    Vector3 facing = radial * -step.y + Vector3.up * step.x;
                    builder.AddQuad(0, grid[s, k], grid[s + 1, k], grid[s + 1, k + 1], grid[s, k + 1], facing);
                }
            }
        }

        static Vector2[] Arc(Vector2 centre, float radius, float fromDegrees, float toDegrees, int steps)
        {
            var points = new Vector2[steps + 1];
            for (int i = 0; i <= steps; i++)
            {
                float a = Mathf.Lerp(fromDegrees, toDegrees, i / (float)steps) * Mathf.Deg2Rad;
                points[i] = centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
            }
            return points;
        }

        /// <summary>Polished floor; 2 m tiles (planar UVs).</summary>
        static Mesh Floor()
        {
            var builder = new MeshBuilder();
            var profile = new Vector2[14];
            for (int i = 0; i < profile.Length; i++)
                profile[i] = new Vector2(FloorRadius * i / (profile.Length - 1f), 0f);
            LatheY(builder, profile, 96, (p, _) => new Vector2(p.x, p.z) / 2f);
            return builder.ToMesh("Showroom_Floor");
        }

        /// <summary>Seamless cove from the floor's edge up into the wall, the wall, and a cove into the ceiling.</summary>
        static Mesh Cyclorama()
        {
            var points = new System.Collections.Generic.List<Vector2>();
            points.AddRange(Arc(new Vector2(FloorRadius, CoveRadius), CoveRadius, -90f, 0f, 10));
            points.Add(new Vector2(FloorRadius + CoveRadius, WallHeight));
            points.AddRange(Arc(new Vector2(FloorRadius + CoveRadius - CeilingCove, WallHeight), CeilingCove, 0f, 90f, 8));
            points.Add(new Vector2(0f, WallHeight + CeilingCove)); // the ceiling step runs inwards, so it faces down
            var builder = new MeshBuilder();
            LatheY(builder, points.ToArray(), 128, (p, run) => new Vector2(Mathf.Atan2(p.x, p.z) * 4f, run / 4f));
            return builder.ToMesh("Showroom_Cyclorama");
        }

        /// <summary>Low deck with a bevelled edge; the car's tyres stand on its top face.</summary>
        static Mesh Turntable()
        {
            var builder = new MeshBuilder();
            var profile = new[]
            {
                new Vector2(0f, TurntableHeight), new Vector2(TurntableRadius - 0.04f, TurntableHeight),
                new Vector2(TurntableRadius, TurntableHeight - 0.02f), new Vector2(TurntableRadius, 0f)
            };
            LatheY(builder, profile, 96, (p, _) => new Vector2(p.x, p.z) / 6f);
            return builder.ToMesh("Showroom_Turntable");
        }

        /// <summary>Flat ring at <paramref name="height"/>, facing up (floor lines) or down (ceiling lights).</summary>
        static Mesh Annulus(float inner, float outer, float height, int segments, bool faceUp)
        {
            var builder = new MeshBuilder();
            var profile = faceUp
                ? new[] { new Vector2(inner, height), new Vector2(outer, height) }
                : new[] { new Vector2(outer, height), new Vector2(inner, height) };
            LatheY(builder, profile, segments, (p, run) => new Vector2(0.5f, 0.5f));
            return builder.ToMesh("Showroom_Ring");
        }

        /// <summary>
        /// Low glowing band where the floor meets the cove. Upright, so it stays a solid line from the camera (a line
        /// painted on the floor would be seen edge-on and break up).
        /// </summary>
        static Mesh SkirtingBand()
        {
            var builder = new MeshBuilder();
            LatheY(builder, new[] { new Vector2(FloorRadius + 0.03f, 0.03f), new Vector2(FloorRadius + 0.03f, 0.11f) }, 128, (p, run) => new Vector2(0.5f, 0.5f));
            return builder.ToMesh("Showroom_SkirtingLight");
        }

        /// <summary>Two long ceiling softboxes either side of the turntable, angled in towards the car.</summary>
        static Mesh Softboxes()
        {
            var builder = new MeshBuilder();
            foreach (float side in new[] { -1f, 1f })
            {
                var rotation = Quaternion.Euler(0f, 0f, side * 18f);
                Vector3 centre = new Vector3(side * 3.6f, 5f, 0f);
                Vector3 across = rotation * Vector3.right * 0.45f, along = Vector3.forward * 3.8f;
                builder.AddQuad(0, centre - across - along, centre + across - along, centre + across + along, centre - across + along,
                    Vector2.zero, Vector2.right, Vector2.one, Vector2.up, rotation * Vector3.down);
            }
            return builder.ToMesh("Showroom_Softboxes");
        }

        /// <summary>Vertical light strips around the wall: crisp highlight lines along the car's flanks.</summary>
        static Mesh WallStrips()
        {
            var builder = new MeshBuilder();
            const int count = 18;
            float radius = FloorRadius + CoveRadius - 0.02f;
            for (int i = 0; i < count; i++)
            {
                float angle = (i + 0.5f) * 2f * Mathf.PI / count;
                var radial = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                var tangent = new Vector3(radial.z, 0f, -radial.x) * 0.09f;
                Vector3 bottom = radial * radius + Vector3.up * (CoveRadius + 0.3f), top = radial * radius + Vector3.up * 5.2f;
                builder.AddQuad(0, bottom - tangent, bottom + tangent, top + tangent, top - tangent, Vector2.zero, Vector2.right, Vector2.one, Vector2.up, -radial);
            }
            return builder.ToMesh("Showroom_WallStrips");
        }

        static Mesh ContactShadowQuad()
        {
            var builder = new MeshBuilder();
            const float halfWidth = 1.35f, halfLength = 2.75f, y = TurntableHeight + 0.003f;
            builder.AddQuad(0, new Vector3(-halfWidth, y, -halfLength), new Vector3(halfWidth, y, -halfLength), new Vector3(halfWidth, y, halfLength),
                new Vector3(-halfWidth, y, halfLength), Vector2.zero, Vector2.right, Vector2.one, Vector2.up, Vector3.up);
            return builder.ToMesh("Showroom_ContactShadow");
        }

        static Mesh Save(Mesh mesh, string name)
        {
            mesh.name = name;
            string path = $"{MeshFolder}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }
            EditorUtility.CopySerialized(mesh, existing);
            Object.DestroyImmediate(mesh);
            return existing;
        }

        // ---------- Textures ----------
        // The floor texture covers one 2 m tile: polished dark stone with fine grain and a thin grout joint.

        static bool IsJoint(float u, float v) => u < 0.004f || u > 0.996f || v < 0.004f || v > 0.996f;

        static void FloorAlbedo(int size, Color[] pixels) => ProceduralTextures.ForEach(size, pixels, (u, v) =>
        {
            if (IsJoint(u, v))
                return new Color(0.025f, 0.025f, 0.028f);
            float cloud = ProceduralTextures.Fbm(u, v, 4, 4, 511) * 0.35f + ProceduralTextures.Fbm(u, v, 32, 2, 512) * 0.15f;
            float speck = ProceduralTextures.Grain(u, v, 256, 513) > 0.985f ? 0.05f : 0f;
            float g = 0.06f + cloud * 0.05f + speck;
            return new Color(g, g * 1.01f, g * 1.06f);
        });

        static float FloorHeight(float u, float v) => IsJoint(u, v) ? 0f : 0.5f + ProceduralTextures.Fbm(u, v, 64, 2, 514) * 0.05f;

        /// <summary>Mask map: metallic (R) 0, smoothness (A) high on the polished tiles, low in the joints and faint scuffs.</summary>
        static void FloorMask(int size, Color[] pixels) => ProceduralTextures.ForEach(size, pixels, (u, v) =>
        {
            if (IsJoint(u, v))
                return new Color(0f, 0f, 0f, 0.3f);
            float scuff = Mathf.SmoothStep(0.55f, 0.75f, ProceduralTextures.Fbm(u, v, 6, 3, 515)) * 0.18f;
            return new Color(0f, 0f, 0f, 0.86f - scuff);
        });
    }
}
