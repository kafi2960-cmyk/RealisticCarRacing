using RealisticCarRacing.Vehicles;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RealisticCarRacing.EditorTools
{
    /// <summary>
    /// Generates the vehicle test track: a gridded ground plane, ramp, slalom cones, speed bumps,
    /// the primitive test car (saved as a prefab), chase camera, debug HUD and touch controls.
    /// Menu: RealisticCarRacing &gt; Create Vehicle Test Scene. Re-running rebuilds the scene and prefab;
    /// an existing settings asset is kept so tuning isn't lost.
    /// </summary>
    public static class VehicleTestSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/VehicleTestTrack.unity";
        public const string PrefabPath = "Assets/Prefabs/TestVehicle.prefab";
        public const string SettingsPath = "Assets/Settings/Vehicles/TestCarSettings.asset";
        const string GridTexturePath = "Assets/Textures/TestGrid.png";
        const float GroundSize = 600f;
        const float GridCellSize = 10f;

        [MenuItem("RealisticCarRacing/Create Vehicle Test Scene")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var settings = LoadOrCreateSettings();
            var materials = LoadOrCreateCarMaterials();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            ConfigureSun();
            BuildTrack();

            var car = TestVehicleFactory.Create(settings, new Vector3(0f, 0.5f, 0f), Quaternion.identity, materials);
            PrefabUtility.SaveAsPrefabAssetAndConnect(car.gameObject, PrefabPath, InteractionMode.AutomatedAction);

            SceneBuildUtility.SetUpDrivingView(car).farClipPlane = 1000f;

            EditorSceneManager.SaveScene(scene, ScenePath);
            // A development sandbox: kept out of player builds (smaller download, less to load).
            SceneBuildUtility.AddSceneToBuild(ScenePath, includeInPlayer: false);
            AssetDatabase.SaveAssets();
            Debug.Log($"RealisticCarRacing: built {ScenePath} and {PrefabPath}. Press Play to drive.");
        }

        /// <summary>The primitive test car prefab, created (with its settings and materials) if it doesn't exist yet.</summary>
        public static GameObject LoadOrCreatePrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab != null)
                return prefab;
            var car = TestVehicleFactory.Create(LoadOrCreateSettings(), Vector3.zero, Quaternion.identity, LoadOrCreateCarMaterials());
            prefab = PrefabUtility.SaveAsPrefabAsset(car.gameObject, PrefabPath);
            Object.DestroyImmediate(car.gameObject);
            return prefab;
        }

        public static TestVehicleFactory.Materials LoadOrCreateCarMaterials() => new TestVehicleFactory.Materials
        {
            body = LoadOrCreateMaterial("TestVehicle_Body", new Color(0.75f, 0.1f, 0.08f), smoothness: 0.75f, metallic: 0.3f),
            cabin = LoadOrCreateMaterial("TestVehicle_Cabin", new Color(0.12f, 0.14f, 0.18f), smoothness: 0.9f),
            tyre = LoadOrCreateMaterial("TestVehicle_Tyre", new Color(0.08f, 0.08f, 0.08f), smoothness: 0.2f),
            marker = LoadOrCreateMaterial("TestVehicle_SpinMarker", new Color(1f, 0.85f, 0.1f), smoothness: 0.3f)
        };

        public static VehicleSettings LoadOrCreateSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<VehicleSettings>(SettingsPath);
            if (settings != null)
                return settings;
            settings = ScriptableObject.CreateInstance<VehicleSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
            return settings;
        }

        static Material LoadOrCreateMaterial(string name, Color color, float smoothness, float metallic = 0f) =>
            SceneBuildUtility.LoadOrCreateLit(name, color, smoothness, metallic);

        static void ConfigureSun()
        {
            var sun = Object.FindAnyObjectByType<Light>();
            sun.name = "Sun";
            sun.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
            sun.shadows = LightShadows.Soft;
        }

        static void BuildTrack()
        {
            var track = new GameObject("TestTrack").transform;

            var groundMaterial = LoadOrCreateMaterial("TestTrack_Ground", Color.white, smoothness: 0.1f);
            groundMaterial.SetTexture("_BaseMap", LoadOrCreateGridTexture());
            groundMaterial.SetTextureScale("_BaseMap", Vector2.one * (GroundSize / GridCellSize));
            var ground = CreatePrimitive(PrimitiveType.Plane, "Ground", track, Vector3.zero, groundMaterial);
            ground.transform.localScale = new Vector3(GroundSize / 10f, 1f, GroundSize / 10f); // a Plane is 10 m across

            var wallMaterial = LoadOrCreateMaterial("TestTrack_Wall", new Color(0.55f, 0.55f, 0.6f), smoothness: 0.2f);
            float half = GroundSize * 0.5f;
            CreateBox("Wall_North", track, new Vector3(0f, 1f, half), new Vector3(GroundSize, 2f, 1f), wallMaterial);
            CreateBox("Wall_South", track, new Vector3(0f, 1f, -half), new Vector3(GroundSize, 2f, 1f), wallMaterial);
            CreateBox("Wall_East", track, new Vector3(half, 1f, 0f), new Vector3(1f, 2f, GroundSize), wallMaterial);
            CreateBox("Wall_West", track, new Vector3(-half, 1f, 0f), new Vector3(1f, 2f, GroundSize), wallMaterial);

            // Ramp ahead and to the right of the start: tests suspension landing and anti-roll.
            var rampMaterial = LoadOrCreateMaterial("TestTrack_Ramp", new Color(0.25f, 0.45f, 0.8f), smoothness: 0.2f);
            var ramp = CreateBox("Ramp", track, new Vector3(20f, 0.8f, 80f), new Vector3(6f, 0.5f, 10f), rampMaterial);
            ramp.transform.rotation = Quaternion.Euler(-10f, 0f, 0f);

            // Slalom cones to the left: steering response and handbrake turns. Cones are light dynamic bodies.
            var coneMaterial = LoadOrCreateMaterial("TestTrack_Cone", new Color(1f, 0.45f, 0.05f), smoothness: 0.3f);
            for (int i = 0; i < 10; i++)
            {
                var cone = CreatePrimitive(PrimitiveType.Cylinder, $"Cone_{i:00}", track, new Vector3(-25f, 0.4f, 30f + i * 18f), coneMaterial);
                cone.transform.localScale = new Vector3(0.45f, 0.4f, 0.45f);
                cone.AddComponent<Rigidbody>().mass = 4f;
            }

            // Speed bumps behind the start: suspension over repeated impacts.
            var bumpMaterial = LoadOrCreateMaterial("TestTrack_Bump", new Color(0.9f, 0.9f, 0.2f), smoothness: 0.2f);
            for (int i = 0; i < 5; i++)
            {
                var bump = CreatePrimitive(PrimitiveType.Cylinder, $"SpeedBump_{i}", track, new Vector3(0f, 0f, -40f - i * 12f), bumpMaterial);
                bump.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
                bump.transform.localScale = new Vector3(0.3f, 5f, 0.3f);
            }
        }

        static Texture2D LoadOrCreateGridTexture()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(GridTexturePath);
            if (existing != null)
                return existing;

            const int size = 256;
            var texture = new Texture2D(size, size, TextureFormat.RGB24, false);
            var fill = new Color(0.36f, 0.37f, 0.38f);
            var line = new Color(0.85f, 0.85f, 0.85f);
            var subLine = new Color(0.45f, 0.46f, 0.47f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool major = x < 4 || y < 4;
                    bool minor = x % 64 < 2 || y % 64 < 2;
                    texture.SetPixel(x, y, major ? line : minor ? subLine : fill);
                }
            }
            System.IO.File.WriteAllBytes(GridTexturePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(GridTexturePath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(GridTexturePath);
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.anisoLevel = 8;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(GridTexturePath);
        }

        static GameObject CreatePrimitive(PrimitiveType type, string name, Transform parent, Vector3 position, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        static GameObject CreateBox(string name, Transform parent, Vector3 position, Vector3 size, Material material)
        {
            var box = CreatePrimitive(PrimitiveType.Cube, name, parent, position, material);
            box.transform.localScale = size;
            return box;
        }

        /// <summary>Command-line entry: applies project settings, then builds the scene.</summary>
        public static void SetupFromCommandLine()
        {
            ProjectSetup.Apply();
            Build();
        }
    }
}
