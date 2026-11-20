using RealisticCarRacing.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RealisticCarRacing.EditorTools
{
    /// <summary>
    /// Generates the MainMenu scene: a dark studio with a turntable, spotlights, reflections and post-processing,
    /// plus the menu controller (which builds its UI at runtime). It becomes the first scene in the build.
    /// </summary>
    public static class MenuSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/MainMenu.unity";
        static readonly Color Backdrop = new Color(0.03f, 0.035f, 0.045f);

        [MenuItem("RealisticCarRacing/Create Main Menu Scene")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            GameContentBuilder.Build();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.22f, 0.24f, 0.28f);
            RenderSettings.ambientEquatorColor = new Color(0.1f, 0.1f, 0.12f);
            RenderSettings.ambientGroundColor = new Color(0.04f, 0.04f, 0.05f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Backdrop;
            RenderSettings.fogStartDistance = 12f;
            RenderSettings.fogEndDistance = 40f;

            var camera = Camera.main;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Backdrop;
            camera.fieldOfView = 38f;
            camera.transform.position = new Vector3(-3.4f, 1.7f, -7.2f);
            camera.transform.LookAt(new Vector3(-1.3f, 0.75f, 0f));
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            camera.GetUniversalAdditionalCameraData().antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;

            var floorMaterial = SceneBuildUtility.LoadOrCreateLit("Showroom_Floor", new Color(0.06f, 0.065f, 0.075f), 0.6f, 0.1f, default, "Menu");
            var turntableMaterial = SceneBuildUtility.LoadOrCreateLit("Showroom_Turntable", new Color(0.13f, 0.13f, 0.15f), 0.75f, 0.4f, default, "Menu");
            Disc("Floor", null, Vector3.down * 0.02f, 40f, 0.02f, floorMaterial);
            var turntable = new GameObject("Turntable").transform;
            Disc("Platform", turntable, new Vector3(0f, 0.03f, 0f), 6.4f, 0.03f, turntableMaterial);

            SetUpLights();
            var probe = new GameObject("ReflectionProbe").AddComponent<ReflectionProbe>();
            probe.transform.position = new Vector3(0f, 2f, 0f);
            probe.mode = ReflectionProbeMode.Baked;
            probe.size = new Vector3(30f, 10f, 30f);
            probe.resolution = 128;
            CreatePostProcessing();

            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystem.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            var showroom = new GameObject("Showroom").AddComponent<ShowroomController>();
            showroom.Configure(turntable);
            new GameObject("MainMenu").AddComponent<MainMenuController>().Configure(showroom);

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
            Lightmapping.Bake();
            EditorSceneManager.SaveScene(scene, ScenePath);
            SceneBuildUtility.AddSceneToBuild(ScenePath, first: true);
            AssetDatabase.SaveAssets();
            Debug.Log($"RealisticCarRacing: built {ScenePath}.");
        }

        static void Disc(string name, Transform parent, Vector3 position, float diameter, float halfHeight, Material material)
        {
            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = name;
            Object.DestroyImmediate(disc.GetComponent<Collider>());
            disc.transform.SetParent(parent, false);
            disc.transform.localPosition = position;
            disc.transform.localScale = new Vector3(diameter, halfHeight, diameter);
            disc.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        static void SetUpLights()
        {
            var ambientLight = Object.FindAnyObjectByType<Light>();
            ambientLight.name = "Moonlight";
            ambientLight.intensity = 0.6f;
            ambientLight.color = new Color(0.75f, 0.82f, 1f);
            ambientLight.transform.rotation = Quaternion.Euler(55f, -30f, 0f);
            ambientLight.shadows = LightShadows.None;

            Spot("KeyLight", new Vector3(-3.5f, 6f, -4f), new Color(1f, 0.94f, 0.86f), 22f, 60f, LightShadows.Soft);
            Spot("RimLight", new Vector3(3.5f, 4.5f, 5f), new Color(0.6f, 0.72f, 1f), 12f, 55f, LightShadows.None);
            Spot("FillLight", new Vector3(5f, 3f, -4f), new Color(1f, 0.8f, 0.7f), 8f, 65f, LightShadows.None);
        }

        static void Spot(string name, Vector3 position, Color color, float intensity, float angle, LightShadows shadows)
        {
            var light = new GameObject(name).AddComponent<Light>();
            light.type = LightType.Spot;
            light.transform.position = position;
            light.transform.LookAt(new Vector3(0f, 0.5f, 0f));
            light.color = color;
            light.intensity = intensity;
            light.range = 18f;
            light.spotAngle = angle;
            light.shadows = shadows;
        }

        static void CreatePostProcessing()
        {
            const string path = "Assets/Settings/Menu_PostProcess.asset";
            AssetDatabase.DeleteAsset(path);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
            profile.Add<Tonemapping>(true).mode.value = TonemappingMode.ACES;
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.value = 0.9f;
            bloom.intensity.value = 0.5f;
            profile.Add<Vignette>(true).intensity.value = 0.38f;
            profile.Add<ColorAdjustments>(true).postExposure.value = 0.4f;
            foreach (var component in profile.components)
                AssetDatabase.AddObjectToAsset(component, profile);
            EditorUtility.SetDirty(profile);

            var volume = new GameObject("PostProcessing").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
        }
    }
}
