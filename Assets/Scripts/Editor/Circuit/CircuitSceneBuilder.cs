using System.Collections.Generic;
using RealisticCarRacing.Managers;
using RealisticCarRacing.Racing;
using RealisticCarRacing.Tracks;
using RealisticCarRacing.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RealisticCarRacing.EditorTools.Circuit
{
    /// <summary>
    /// Generates the Grand Prix circuit scene from <see cref="TrackLayout"/>: road with painted edge lines, kerbs,
    /// gravel traps, concrete barriers, start gantry and grid, pit building, grandstands, control tower, a city
    /// skyline, distant hills and instanced trees, plus sun, sky, fog, reflection probe and post-processing,
    /// and the race systems (camera rig, touch controls, RaceManager, HUD and race menus).
    /// Menu: RealisticCarRacing &gt; Create Circuit Scene. Deterministic: re-running gives the same result.
    /// </summary>
    public static class CircuitSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Circuit_GrandPrix.unity";
        public const string LayoutPath = "Assets/Settings/Tracks/GrandPrixCircuit.asset";
        const string MeshFolder = "Assets/Models/Generated/Circuit";
        const string MaterialFolder = "Circuit";
        const int GridSlots = 12;
        const float GroundSize = 4400f;
        const float SceneryRadius = 900f;
        const int TrackChunkSamples = 100; // ~200 m of track per render chunk

        sealed class CircuitMaterials
        {
            public Material asphalt, kerb, gravel, barrier, paint, checker, metal, grass, hills, concrete;
            public Material crowd, roof, office, officeBlue, residential, residentialGrey, garage, glass;
            public Material bark, leaves, pineLeaves;
        }

        /// <summary>Areas (XZ) occupied by buildings, kept clear of trees.</summary>
        sealed class Footprints
        {
            readonly List<Rect> rects = new List<Rect>();

            public void Add(Vector3 center, Quaternion rotation, Vector3 size, float margin)
            {
                var min = new Vector2(float.MaxValue, float.MaxValue);
                var max = new Vector2(float.MinValue, float.MinValue);
                foreach (var corner in new[] { new Vector3(-1, 0, -1), new Vector3(1, 0, -1), new Vector3(1, 0, 1), new Vector3(-1, 0, 1) })
                {
                    Vector3 p = center + rotation * Vector3.Scale(corner, size * 0.5f);
                    min = Vector2.Min(min, new Vector2(p.x, p.z));
                    max = Vector2.Max(max, new Vector2(p.x, p.z));
                }
                rects.Add(Rect.MinMaxRect(min.x - margin, min.y - margin, max.x + margin, max.y + margin));
            }

            public bool Overlaps(Vector2 point)
            {
                foreach (var rect in rects)
                {
                    if (rect.Contains(point))
                        return true;
                }
                return false;
            }

            public bool Overlaps(Rect other)
            {
                foreach (var rect in rects)
                {
                    if (rect.Overlaps(other))
                        return true;
                }
                return false;
            }
        }

        [MenuItem("RealisticCarRacing/Create Circuit Scene")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var layout = LoadOrCreateLayout();
            var samples = layout.Sample();
            float length = samples[^1].distance + samples[1].distance;
            var barrierSection = TrackMeshBuilder.BarrierSection(layout.barrierHeight, out float paintedFraction);
            var materials = CreateMaterials(ProceduralTextures.LoadOrCreate(paintedFraction, layout.roadWidth));
            SceneBuildUtility.EnsureFolder(MeshFolder);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            SetUpSunSkyAndFog(materials);

            // --- Track ---
            var trackRoot = new GameObject("Track");
            var track = trackRoot.AddComponent<RaceTrack>();
            track.Bake(layout, samples);
            // Collision uses seamless whole-loop meshes; rendering uses ~200 m chunks so off-screen track is culled.
            float roadTile = TileLength(length, layout.roadWidth), barrierTile = TileLength(length, 8f);
            AddColliderObject("Road", trackRoot.transform, TrackMeshBuilder.Road(samples, length, layout, roadTile, name: "Road_Collider"), null);
            AddColliderObject("Kerbs", trackRoot.transform, TrackMeshBuilder.Kerbs(samples, length, layout, out int cornerCount), null);
            AddColliderObject("Barriers", trackRoot.transform,
                TrackMeshBuilder.Barriers(samples, length, layout, barrierSection, barrierTile, name: "Barriers_Collider"), LoadOrCreateBarrierPhysics());

            var trackVisuals = new GameObject("TrackVisuals").transform;
            trackVisuals.SetParent(trackRoot.transform, false);
            int chunk = 0;
            for (int first = 0; first < samples.Length; first += TrackChunkSamples, chunk++)
            {
                int count = Mathf.Min(TrackChunkSamples, samples.Length - first) + 1; // +1 ring joins the next chunk
                AddMeshObject($"Road_{chunk}", trackVisuals, TrackMeshBuilder.Road(samples, length, layout, roadTile, first, count, $"Road_{chunk}"),
                    new[] { materials.asphalt }, collider: false, castShadows: false);
                AddMeshObject($"Barriers_{chunk}", trackVisuals, TrackMeshBuilder.Barriers(samples, length, layout, barrierSection, barrierTile, first, count, $"Barriers_{chunk}"),
                    new[] { materials.barrier }, collider: false, castShadows: true);
            }
            foreach (var kerb in TrackMeshBuilder.KerbPieces(samples, length, layout))
                AddMeshObject(kerb.name, trackVisuals, kerb, new[] { materials.kerb }, collider: false, castShadows: false);
            foreach (var trap in TrackMeshBuilder.GravelTraps(samples, length, layout))
                AddMeshObject(trap.name, trackVisuals, trap, new[] { materials.gravel }, collider: false, castShadows: false);
            AddMeshObject("Markings", trackRoot.transform, TrackMeshBuilder.Markings(track, layout, GridSlots),
                new[] { materials.paint, materials.checker }, collider: false, castShadows: false);
            AddMeshObject("StartGantry", trackRoot.transform, TrackMeshBuilder.StartGantry(track, layout),
                new[] { materials.metal, materials.checker }, collider: false, castShadows: true, cullScreenHeight: 0.004f);

            // --- Ground, buildings, scenery ---
            Vector3 center = TrackCenter(samples);
            var environment = new GameObject("Environment").transform;
            var ground = AddMeshObject("Ground", environment, SceneryMeshBuilder.GroundGrid(center, GroundSize, 88),
                new[] { materials.grass }, collider: false, castShadows: false);
            var groundCollider = ground.AddComponent<BoxCollider>();
            groundCollider.center = new Vector3(center.x, -1f, center.z);
            groundCollider.size = new Vector3(GroundSize, 2f, GroundSize);

            var footprints = new Footprints();
            int buildings = BuildPaddock(track, layout, materials, footprints, environment);

            // The hills are part of the circuit's look, so every preset keeps them; only the skyline is optional.
            var hills = AddMeshObject("DistantHills", environment, SceneryMeshBuilder.HillRing(center, 1750f, 2700f),
                new[] { materials.hills }, collider: false, castShadows: false);
            hills.layer = LayerMask.NameToLayer(EnvironmentQualityScaler.BackgroundLayer);
            var highDetail = new GameObject("HighDetailScenery");
            highDetail.transform.SetParent(environment, false);
            buildings += BuildCity(samples, layout, center, materials, footprints, highDetail.transform);

            var scatters = BuildTrees(samples, layout, center, footprints, materials, environment, out int treeCount);

            // --- Race: camera, controls, race manager and UI. Cars are spawned at runtime from the GameSession. ---
            var camera = Camera.main;
            var rig = SceneBuildUtility.SetUpRaceCamera(camera, track.GetGridPose(0));
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            SceneBuildUtility.AddTouchControls();
            var raceObject = new GameObject("Race");
            var raceManager = raceObject.AddComponent<RaceManager>();
            raceManager.Configure(track, rig);
            raceObject.AddComponent<RaceHud>().Configure(raceManager);
            raceObject.AddComponent<RaceMenus>().Configure(raceManager);

            var volume = CreatePostProcessing();
            CreateReflectionProbe(center);
            new GameObject("EnvironmentQuality").AddComponent<EnvironmentQualityScaler>()
                .Configure(camera, volume, scatters, highDetail);

            EditorSceneManager.SaveScene(scene, ScenePath);
            BakeEnvironmentLighting();
            EditorSceneManager.SaveScene(scene, ScenePath);
            SceneBuildUtility.AddSceneToBuild(ScenePath);
            AssetDatabase.SaveAssets();

            Debug.Log($"RealisticCarRacing: built {ScenePath} — lap {length:0} m, {samples.Length} samples, {cornerCount} kerbed corners, " +
                      $"{buildings} buildings, {treeCount} trees.");
        }

        /// <summary>Command-line entry: applies project settings, then builds the circuit.</summary>
        public static void BuildFromCommandLine()
        {
            ProjectSetup.Apply();
            Build();
        }

        static TrackLayout LoadOrCreateLayout()
        {
            var layout = AssetDatabase.LoadAssetAtPath<TrackLayout>(LayoutPath);
            if (layout != null)
                return layout;
            SceneBuildUtility.EnsureFolder(System.IO.Path.GetDirectoryName(LayoutPath));
            layout = ScriptableObject.CreateInstance<TrackLayout>();
            AssetDatabase.CreateAsset(layout, LayoutPath);
            return layout;
        }

        /// <summary>Texture repeat length adjusted so a whole number of tiles fits the lap (no seam at the start line).</summary>
        static float TileLength(float lapLength, float desired) => lapLength / Mathf.Max(1f, Mathf.Round(lapLength / desired));

        static Vector3 TrackCenter(TrackSample[] samples)
        {
            var bounds = new Bounds(samples[0].position, Vector3.zero);
            foreach (var sample in samples)
                bounds.Encapsulate(sample.position);
            return new Vector3(bounds.center.x, 0f, bounds.center.z);
        }

        static float DistanceToTrack(TrackSample[] samples, Vector2 point)
        {
            float best = float.MaxValue;
            for (int i = 0; i < samples.Length; i += 2)
            {
                float dx = samples[i].position.x - point.x, dz = samples[i].position.z - point.y;
                best = Mathf.Min(best, dx * dx + dz * dz);
            }
            return Mathf.Sqrt(best);
        }

        static Material[] Mats(params Material[] materials) => materials;

        static CircuitMaterials CreateMaterials(ProceduralTextures.Set t)
        {
            SceneBuildUtility.LitMaps Maps(Texture2D baseMap, Texture2D normal = null, float normalScale = 1f, Texture2D mask = null, float tiling = 1f) =>
                new SceneBuildUtility.LitMaps { baseMap = baseMap, normalMap = normal, normalScale = normalScale, metallicGlossMap = mask, tiling = Vector2.one * tiling };
            Material Lit(string name, Color color, float smoothness, SceneBuildUtility.LitMaps maps = default, float metallic = 0f, bool instancing = false) =>
                SceneBuildUtility.LoadOrCreateLit(name, color, smoothness, metallic, maps, MaterialFolder, instancing);

            var m = new CircuitMaterials
            {
                asphalt = Lit("Asphalt", Color.white, 0.3f, Maps(t.asphalt, t.asphaltNormal, 0.35f)),
                kerb = Lit("Kerb", Color.white, 0.45f, Maps(t.kerb)),
                gravel = Lit("Gravel", Color.white, 0.08f, Maps(t.gravel, t.gravelNormal, 1f)),
                barrier = Lit("Barrier", Color.white, 0.25f, Maps(t.barrier, t.concreteNormal, 0.4f)),
                paint = Lit("LinePaint", new Color(0.85f, 0.85f, 0.83f), 0.4f),
                checker = Lit("Checker", Color.white, 0.4f, Maps(t.checker)),
                metal = Lit("Metal", new Color(0.58f, 0.6f, 0.63f), 0.6f, metallic: 0.85f),
                grass = SceneBuildUtility.LoadOrCreateLit("Grass", Color.white, 0.12f, 0f, new SceneBuildUtility.LitMaps
                {
                    baseMap = t.grassMacro, tiling = Vector2.one / 300f,
                    detailAlbedoMap = t.grassDetail, detailNormalMap = t.grassNormal, detailTiling = Vector2.one * 75f
                }, MaterialFolder),
                hills = Lit("DistantHills", new Color(0.62f, 0.7f, 0.6f), 0.05f, Maps(t.grassMacro, tiling: 0.3f)),
                concrete = Lit("Concrete", Color.white, 0.2f, Maps(t.concrete, t.concreteNormal, 0.5f)),
                crowd = Lit("GrandstandCrowd", Color.white, 0.15f, Maps(t.crowd)),
                roof = Lit("Roof", Color.white, 0.3f, Maps(t.roof), metallic: 0.3f),
                office = Lit("Facade_Office", Color.white, 1f, Maps(t.officeFacade, mask: t.officeMask)),
                officeBlue = Lit("Facade_OfficeBlue", new Color(0.78f, 0.87f, 1f), 1f, Maps(t.officeFacade, mask: t.officeMask)),
                residential = Lit("Facade_Residential", Color.white, 1f, Maps(t.residentialFacade, mask: t.residentialMask)),
                residentialGrey = Lit("Facade_ResidentialGrey", new Color(0.86f, 0.86f, 0.9f), 1f, Maps(t.residentialFacade, mask: t.residentialMask)),
                garage = Lit("Facade_Garage", Color.white, 1f, Maps(t.garageFacade, mask: t.garageMask)),
                glass = Lit("Glass", new Color(0.3f, 0.38f, 0.46f), 0.95f, metallic: 0.5f),
                bark = Lit("Bark", Color.white, 0.1f, Maps(t.bark), instancing: true),
                leaves = Lit("Leaves", Color.white, 0.2f, Maps(t.leaves), instancing: true),
                pineLeaves = Lit("LeavesPine", Color.white, 0.15f, Maps(t.pineLeaves), instancing: true)
            };

            // Open cone undersides are visible from the car, so foliage renders both faces.
            foreach (var foliage in new[] { m.leaves, m.pineLeaves })
            {
                foliage.SetFloat("_Cull", (float)CullMode.Off);
                foliage.doubleSidedGI = true;
            }
            return m;
        }

        static void SetUpSunSkyAndFog(CircuitMaterials materials)
        {
            var sun = Object.FindAnyObjectByType<Light>();
            sun.name = "Sun";
            sun.transform.rotation = Quaternion.Euler(42f, 50f, 0f); // low afternoon sun from the south-west
            sun.color = new Color(1f, 0.956f, 0.886f);
            sun.intensity = 1.6f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.85f;

            string skyPath = $"Assets/Materials/{MaterialFolder}/Sky.mat";
            var sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            if (sky == null)
            {
                sky = new Material(Shader.Find("Skybox/Procedural"));
                AssetDatabase.CreateAsset(sky, skyPath);
            }
            sky.SetFloat("_SunSize", 0.035f);
            sky.SetFloat("_SunSizeConvergence", 6f);
            sky.SetFloat("_AtmosphereThickness", 0.95f);
            sky.SetColor("_SkyTint", new Color(0.48f, 0.55f, 0.62f));
            sky.SetColor("_GroundColor", new Color(0.42f, 0.44f, 0.4f));
            sky.SetFloat("_Exposure", 1.2f);
            EditorUtility.SetDirty(sky);

            RenderSettings.skybox = sky;
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.7f, 0.77f, 0.84f);
            RenderSettings.fogStartDistance = 350f;
            RenderSettings.fogEndDistance = 2800f;
        }

        /// <param name="cullScreenHeight">If above 0, a LODGroup hides the object when it covers less of the screen height than this.</param>
        static GameObject AddMeshObject(string name, Transform parent, Mesh mesh, Material[] materials, bool collider, bool castShadows,
            float cullScreenHeight = 0f)
        {
            // Render-only meshes don't need a CPU-side copy at runtime; collision meshes keep theirs.
            mesh = SaveMesh(mesh, readable: collider);

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            if (collider)
                go.AddComponent<MeshCollider>().sharedMesh = mesh;
            GameObjectUtility.SetStaticEditorFlags(go,
                StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic);
            if (cullScreenHeight > 0f)
                AddCulling(go, cullScreenHeight);
            return go;
        }

        static void AddCulling(GameObject go, float screenHeight)
        {
            var lodGroup = go.AddComponent<LODGroup>();
            lodGroup.SetLODs(new[] { new LOD(screenHeight, new Renderer[] { go.GetComponent<MeshRenderer>() }) });
            lodGroup.RecalculateBounds();
        }

        /// <summary>Invisible collision-only object (the visible track is drawn by separate render chunks).</summary>
        static void AddColliderObject(string name, Transform parent, Mesh mesh, PhysicsMaterial material)
        {
            mesh = SaveMesh(mesh, readable: true);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var meshCollider = go.AddComponent<MeshCollider>();
            meshCollider.sharedMesh = mesh;
            meshCollider.sharedMaterial = material;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        }

        static GameObject AddInstance(string name, Transform parent, GameObject source, Vector3 position, Quaternion rotation, float cullScreenHeight = 0f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, rotation);
            go.AddComponent<MeshFilter>().sharedMesh = source.GetComponent<MeshFilter>().sharedMesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = source.GetComponent<MeshRenderer>().sharedMaterials;
            GameObjectUtility.SetStaticEditorFlags(go, GameObjectUtility.GetStaticEditorFlags(source));
            if (cullScreenHeight > 0f)
                AddCulling(go, cullScreenHeight);
            return go;
        }

        static PhysicsMaterial LoadOrCreateBarrierPhysics()
        {
            const string path = "Assets/Settings/Physics/Barrier.asset";
            var material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if (material == null)
            {
                SceneBuildUtility.EnsureFolder("Assets/Settings/Physics");
                material = new PhysicsMaterial("Barrier");
                AssetDatabase.CreateAsset(material, path);
            }
            // Low friction so a car glancing off the wall slides along it instead of being caught and spun.
            material.dynamicFriction = 0.1f;
            material.staticFriction = 0.1f;
            material.bounciness = 0.05f;
            material.frictionCombine = PhysicsMaterialCombine.Minimum;
            material.bounceCombine = PhysicsMaterialCombine.Average;
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>Pit building and control tower inside the main straight, grandstands outside it and at the slowest corners.</summary>
        static int BuildPaddock(RaceTrack track, TrackLayout layout, CircuitMaterials m, Footprints footprints, Transform parent)
        {
            var paddock = new GameObject("Paddock").transform;
            paddock.SetParent(parent, false);
            var start = track.GetSample(0);
            var alongTrack = Quaternion.LookRotation(start.forward, Vector3.up);
            float spacing = track.Length / track.SampleCount;
            TrackSample At(float distance) => track.GetSample(Mathf.RoundToInt(distance / spacing));

            // Pit building (left = infield) with a canopy towards the pit wall.
            var pitBuilder = new MeshBuilder(3);
            var pitCenter = At(-30f).position - start.right * (layout.BarrierOffset + 14f);
            var pitSize = new Vector3(14f, 9f, 170f);
            pitBuilder.AddBox(pitCenter, alongTrack, pitSize, 0, 1, new Vector2(10f, 9f));
            pitBuilder.AddBox(pitCenter + start.right * 9.5f + Vector3.up * 8.6f, alongTrack, new Vector3(5f, 0.3f, 170f), 2, 1, Vector2.one * 4f, includeBottom: true);
            AddMeshObject("PitBuilding", paddock, pitBuilder.ToMesh("PitBuilding"), Mats(m.garage, m.roof, m.concrete), collider: false, castShadows: true, cullScreenHeight: 0.01f);
            footprints.Add(pitCenter, alongTrack, pitSize + new Vector3(12f, 0f, 0f), 8f);

            // Race control tower beside the start line: glass cab on a concrete shaft.
            var towerBuilder = new MeshBuilder(3);
            var towerCenter = At(55f).position - start.right * (layout.BarrierOffset + 12f);
            towerBuilder.AddBox(towerCenter, alongTrack, new Vector3(7f, 18f, 7f), 0, 1, new Vector2(12f, 14f));
            towerBuilder.AddBox(towerCenter + Vector3.up * 18f, alongTrack, new Vector3(11f, 5f, 11f), 2, 1, new Vector2(4f, 5f), includeBottom: true);
            AddMeshObject("ControlTower", paddock, towerBuilder.ToMesh("ControlTower"), Mats(m.office, m.roof, m.glass), collider: false, castShadows: true, cullScreenHeight: 0.01f);
            footprints.Add(towerCenter, alongTrack, new Vector3(11f, 0f, 11f), 8f);
            int count = 2;

            // Main grandstands opposite the pits, facing the track.
            var mainStand = AddMeshObject("Grandstand_Main_0", paddock, SceneryMeshBuilder.Grandstand(70f, 14),
                Mats(m.concrete, m.crowd, m.roof), collider: false, castShadows: true, cullScreenHeight: 0.015f);
            mainStand.SetActive(false);
            var standRotation = Quaternion.LookRotation(start.right, Vector3.up);
            float[] standDistances = { -150f, -75f, 0f, 75f, 150f };
            for (int i = 0; i < standDistances.Length; i++)
            {
                var sample = At(standDistances[i]);
                Vector3 position = sample.position + sample.right * (layout.BarrierOffset + 4f);
                var stand = i == 0 ? mainStand : AddInstance($"Grandstand_Main_{i}", paddock, mainStand, position, standRotation, 0.015f);
                stand.transform.SetPositionAndRotation(position, standRotation);
                stand.SetActive(true);
                footprints.Add(position + sample.right * 9f, standRotation, new Vector3(72f, 0f, 20f), 6f);
                count++;
            }

            // Smaller stands on the outside of the slowest corners.
            var cornerStand = AddMeshObject("Grandstand_Corner_0", paddock, SceneryMeshBuilder.Grandstand(45f, 9),
                Mats(m.concrete, m.crowd, m.roof), collider: false, castShadows: true, cullScreenHeight: 0.015f);
            cornerStand.SetActive(false);
            int placed = 0;
            var samples = new TrackSample[track.SampleCount];
            for (int i = 0; i < samples.Length; i++)
                samples[i] = track.GetSample(i);
            foreach (var (first, rangeCount, turnSign) in TrackMeshBuilder.CornerRanges(samples, 1f / 55f, padding: 0))
            {
                var apex = track.GetSample(first + rangeCount / 2);
                Vector3 outward = apex.right * (turnSign > 0f ? 1f : -1f);
                Vector3 position = apex.position + outward * (layout.BarrierOffset + 5f);
                var rotation = Quaternion.LookRotation(outward, Vector3.up);
                if (!IsClearOfTrack(samples, position, rotation, new Vector3(47f, 0f, 16f), layout.BarrierOffset + 3f))
                    continue;
                var stand = placed == 0 ? cornerStand : AddInstance($"Grandstand_Corner_{placed}", paddock, cornerStand, position, rotation, 0.015f);
                stand.transform.SetPositionAndRotation(position, rotation);
                stand.SetActive(true);
                footprints.Add(position + outward * 7f, rotation, new Vector3(47f, 0f, 16f), 6f);
                placed++;
                count++;
            }
            if (placed == 0)
                Object.DestroyImmediate(cornerStand);
            return count;
        }

        /// <summary>True when every corner of the box (centre at its front edge, extending along +Z) is outside the clearance.</summary>
        static bool IsClearOfTrack(TrackSample[] samples, Vector3 frontCenter, Quaternion rotation, Vector3 size, float clearance)
        {
            foreach (var corner in new[] { new Vector3(-0.5f, 0, 0), new Vector3(0.5f, 0, 0), new Vector3(0.5f, 0, 1), new Vector3(-0.5f, 0, 1) })
            {
                Vector3 p = frontCenter + rotation * Vector3.Scale(corner, size);
                if (DistanceToTrack(samples, new Vector2(p.x, p.z)) < clearance)
                    return false;
            }
            return true;
        }

        /// <summary>Skyline of office and apartment blocks around the circuit, combined into one mesh.</summary>
        static int BuildCity(TrackSample[] samples, TrackLayout layout, Vector3 center, CircuitMaterials m, Footprints footprints, Transform parent)
        {
            var random = new System.Random(4242);
            var builder = new MeshBuilder(5);
            var tileSizes = new[] { new Vector2(12f, 14f), new Vector2(12f, 14f), new Vector2(12f, 15f), new Vector2(12f, 15f) };
            int placed = 0;
            for (int attempt = 0; attempt < 2000 && placed < 70; attempt++)
            {
                float angle = (float)random.NextDouble() * Mathf.PI * 2f;
                float radius = Mathf.Lerp(620f, 1150f, Mathf.Sqrt((float)random.NextDouble()));
                Vector3 position = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                var size = new Vector3(Mathf.Lerp(14f, 36f, (float)random.NextDouble()), 0f, Mathf.Lerp(14f, 36f, (float)random.NextDouble()));
                // Taller towers further out keep the skyline readable without crowding the circuit.
                size.y = Mathf.Lerp(12f, 30f, (float)random.NextDouble()) + Mathf.InverseLerp(620f, 1150f, radius) * (float)random.NextDouble() * 60f;
                var rotation = Quaternion.Euler(0f, angle * Mathf.Rad2Deg + (float)random.NextDouble() * 20f, 0f);

                var area = Rect.MinMaxRect(position.x - size.x, position.z - size.z, position.x + size.x, position.z + size.z);
                if (footprints.Overlaps(area) || DistanceToTrack(samples, new Vector2(position.x, position.z)) < layout.BarrierOffset + 80f)
                    continue;

                int style = random.Next(4);
                builder.AddBox(position, rotation, size, style, 4, tileSizes[style]);
                footprints.Add(position, rotation, size, 6f);
                placed++;
            }
            AddMeshObject("City", parent, builder.ToMesh("City"),
                Mats(m.office, m.officeBlue, m.residential, m.residentialGrey, m.roof), collider: false, castShadows: true);
            return placed;
        }

        static InstancedScatter[] BuildTrees(TrackSample[] samples, TrackLayout layout, Vector3 center, Footprints footprints,
            CircuitMaterials m, Transform parent, out int treeCount)
        {
            // Large chunks: fewer instanced draw calls; culling stays coarse but trees are cheap.
            const float spacing = 12f, chunkSize = 200f;
            var random = new System.Random(99);
            var chunks = new[] { new Dictionary<Vector2Int, List<Matrix4x4>>(), new Dictionary<Vector2Int, List<Matrix4x4>>() };
            treeCount = 0;

            for (float x = -SceneryRadius; x <= SceneryRadius; x += spacing)
            for (float z = -SceneryRadius; z <= SceneryRadius; z += spacing)
            {
                var point = new Vector2(center.x + x + ((float)random.NextDouble() - 0.5f) * spacing * 0.9f,
                                        center.z + z + ((float)random.NextDouble() - 0.5f) * spacing * 0.9f);
                float chance = (float)random.NextDouble();
                if (x * x + z * z > SceneryRadius * SceneryRadius)
                    continue;
                // Woods in clumps, scattered trees elsewhere.
                float woods = Mathf.PerlinNoise(point.x * 0.006f + 50f, point.y * 0.006f + 50f);
                if (chance > Mathf.Lerp(0.04f, 0.75f, Mathf.SmoothStep(0.4f, 0.65f, woods)))
                    continue;
                if (footprints.Overlaps(point) || DistanceToTrack(samples, point) < layout.BarrierOffset + 8f)
                    continue;

                int variant = Mathf.PerlinNoise(point.x * 0.01f + 7f, point.y * 0.01f + 7f) > 0.55f ? 1 : 0;
                float scale = Mathf.Lerp(0.8f, 1.35f, (float)random.NextDouble());
                var matrix = Matrix4x4.TRS(new Vector3(point.x, 0f, point.y),
                    Quaternion.Euler(((float)random.NextDouble() - 0.5f) * 6f, (float)random.NextDouble() * 360f, 0f), Vector3.one * scale);
                var key = new Vector2Int(Mathf.FloorToInt(point.x / chunkSize), Mathf.FloorToInt(point.y / chunkSize));
                if (!chunks[variant].TryGetValue(key, out var list))
                    chunks[variant][key] = list = new List<Matrix4x4>();
                list.Add(matrix);
                treeCount++;
            }

            var meshes = new[] { SceneryMeshBuilder.DeciduousTree(), SceneryMeshBuilder.PineTree() };
            var materials = new[] { Mats(m.bark, m.leaves), Mats(m.bark, m.pineLeaves) };
            var scatters = new InstancedScatter[2];
            var root = new GameObject("Trees").transform;
            root.SetParent(parent, false);
            for (int variant = 0; variant < 2; variant++)
            {
                var mesh = SaveMesh(meshes[variant]); // instanced: GPU data only
                var bakedChunks = new List<InstancedScatter.Chunk>();
                foreach (var list in chunks[variant].Values)
                {
                    // Shuffle so a lower density setting thins the chunk evenly.
                    for (int i = list.Count - 1; i > 0; i--)
                    {
                        int j = random.Next(i + 1);
                        (list[i], list[j]) = (list[j], list[i]);
                    }
                    var bounds = new Bounds(list[0].GetPosition(), Vector3.zero);
                    foreach (var matrix in list)
                    {
                        float s = matrix.lossyScale.x;
                        bounds.Encapsulate(new Bounds(matrix.GetPosition() + Vector3.up * 4.5f * s, new Vector3(7f, 9.5f, 7f) * s));
                    }
                    bakedChunks.Add(new InstancedScatter.Chunk { bounds = bounds, matrices = list.ToArray() });
                }
                var scatter = new GameObject(mesh.name).AddComponent<InstancedScatter>();
                scatter.transform.SetParent(root, false);
                scatter.Configure(mesh, materials[variant], bakedChunks.ToArray());
                scatters[variant] = scatter;
            }
            return scatters;
        }

        static Mesh SaveMesh(Mesh mesh, bool readable = false)
        {
            string path = $"{MeshFolder}/{mesh.name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
            }
            else
            {
                EditorUtility.CopySerialized(mesh, existing);
                Object.DestroyImmediate(mesh);
                mesh = existing;
            }
            var serialized = new SerializedObject(mesh);
            serialized.FindProperty("m_IsReadable").boolValue = readable;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return mesh;
        }

        static Volume CreatePostProcessing()
        {
            const string path = "Assets/Settings/Circuit_PostProcess.asset";
            AssetDatabase.DeleteAsset(path);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);

            var tonemapping = profile.Add<Tonemapping>(true);
            tonemapping.mode.value = TonemappingMode.ACES;
            var colorAdjustments = profile.Add<ColorAdjustments>(true);
            colorAdjustments.postExposure.value = 0.35f;
            colorAdjustments.contrast.value = 10f;
            colorAdjustments.saturation.value = 8f;
            var whiteBalance = profile.Add<WhiteBalance>(true);
            whiteBalance.temperature.value = 4f;
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.value = 1.1f;
            bloom.intensity.value = 0.3f;
            bloom.scatter.value = 0.65f;
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.value = 0.18f;
            foreach (var component in profile.components)
                AssetDatabase.AddObjectToAsset(component, profile);
            EditorUtility.SetDirty(profile);

            var volume = new GameObject("PostProcessing").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
            return volume;
        }

        static void CreateReflectionProbe(Vector3 center)
        {
            var probe = new GameObject("ReflectionProbe").AddComponent<ReflectionProbe>();
            probe.transform.position = center + Vector3.up * 20f;
            probe.mode = ReflectionProbeMode.Baked;
            probe.size = new Vector3(1800f, 400f, 1400f);
            probe.resolution = 256;
            probe.hdr = true;
            probe.boxProjection = false;
        }

        /// <summary>Bakes only the environment (sky ambient + reflection probe); all lighting stays real-time.</summary>
        static void BakeEnvironmentLighting()
        {
            const string path = "Assets/Settings/Circuit_Lighting.lighting";
            var settings = AssetDatabase.LoadAssetAtPath<LightingSettings>(path);
            if (settings == null)
            {
                settings = new LightingSettings { name = "Circuit_Lighting" };
                AssetDatabase.CreateAsset(settings, path);
            }
            settings.bakedGI = false;
            settings.realtimeGI = false;
            Lightmapping.lightingSettings = settings;
            if (!Lightmapping.Bake())
                Debug.LogWarning("RealisticCarRacing: environment lighting bake did not complete; open the scene and use Window > Rendering > Lighting > Generate Lighting.");
        }
    }
}
