using RealisticCarRacing.Cameras;
using RealisticCarRacing.Controls;
using RealisticCarRacing.UI;
using RealisticCarRacing.Vehicles;
using UnityEditor;
using UnityEngine;

namespace RealisticCarRacing.EditorTools
{
    /// <summary>Building blocks shared by the scene generators: URP materials and the driving camera/HUD/touch UI.</summary>
    public static class SceneBuildUtility
    {
        public const string LitShader = "Universal Render Pipeline/Lit";

        /// <summary>Optional texture inputs for <see cref="LoadOrCreateLit"/>.</summary>
        public struct LitMaps
        {
            public Texture2D baseMap;
            public Texture2D normalMap;
            public float normalScale;
            /// <summary>Metallic (R) and smoothness (A).</summary>
            public Texture2D metallicGlossMap;
            /// <summary>Fine detail colour (multiplied ×2) for breaking up tiling on large surfaces. Detail tiling is relative to <see cref="tiling"/>.</summary>
            public Texture2D detailAlbedoMap;
            public Texture2D detailNormalMap;
            public Vector2 tiling;
            public Vector2 detailTiling;
        }

        /// <summary>Creates or updates a URP Lit material at Assets/Materials/{folder}/{name}.mat.</summary>
        public static Material LoadOrCreateLit(string name, Color color, float smoothness, float metallic = 0f,
            LitMaps maps = default, string folder = "", bool instancing = false)
        {
            string directory = string.IsNullOrEmpty(folder) ? "Assets/Materials" : $"Assets/Materials/{folder}";
            EnsureFolder(directory);
            string path = $"{directory}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find(LitShader));
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", metallic);
            material.enableInstancing = instancing;

            Vector2 tiling = maps.tiling == Vector2.zero ? Vector2.one : maps.tiling;
            material.SetTexture("_BaseMap", maps.baseMap);
            material.SetTextureScale("_BaseMap", tiling);

            material.SetTexture("_BumpMap", maps.normalMap);
            material.SetFloat("_BumpScale", maps.normalMap != null && maps.normalScale > 0f ? maps.normalScale : 1f);
            SetKeyword(material, "_NORMALMAP", maps.normalMap != null);

            material.SetTexture("_MetallicGlossMap", maps.metallicGlossMap);
            SetKeyword(material, "_METALLICSPECGLOSSMAP", maps.metallicGlossMap != null);

            material.SetTexture("_DetailAlbedoMap", maps.detailAlbedoMap);
            material.SetFloat("_DetailAlbedoMapScale", 1f);
            material.SetTexture("_DetailNormalMap", maps.detailNormalMap);
            material.SetFloat("_DetailNormalMapScale", 1f);
            material.SetTextureScale("_DetailAlbedoMap", maps.detailTiling == Vector2.zero ? Vector2.one : maps.detailTiling);
            SetKeyword(material, "_DETAIL_MULX2", maps.detailAlbedoMap != null);

            EditorUtility.SetDirty(material);
            return material;
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }

        /// <summary>Adds a scene to the build list if missing; <paramref name="first"/> makes it the startup scene.</summary>
        /// <param name="includeInPlayer">False keeps the scene listed (for the editor) but out of player builds.</param>
        public static void AddSceneToBuild(string scenePath, bool first = false, bool includeInPlayer = true)
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(scene => scene.path == scenePath || !System.IO.File.Exists(scene.path));
            var entry = new EditorBuildSettingsScene(scenePath, includeInPlayer);
            if (first)
                scenes.Insert(0, entry);
            else
                scenes.Add(entry);
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        static void SetKeyword(Material material, string keyword, bool enabled)
        {
            if (enabled)
                material.EnableKeyword(keyword);
            else
                material.DisableKeyword(keyword);
        }

        /// <summary>Camera rig with player camera controls, placed behind <paramref name="start"/>; the race sets its target at runtime.</summary>
        public static VehicleCameraRig SetUpRaceCamera(Camera camera, Pose start)
        {
            camera.transform.SetPositionAndRotation(start.position - start.forward * 6.5f + Vector3.up * 2.2f,
                start.rotation * Quaternion.Euler(10f, 0f, 0f));
            var rig = camera.gameObject.AddComponent<VehicleCameraRig>();
            camera.gameObject.AddComponent<PlayerCameraInput>();
            return rig;
        }

        public static void AddTouchControls() => TouchControlsFactory.Create(
            AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
            AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"));

        /// <summary>Puts the camera rig on the scene's main camera, targets the car, and adds the debug HUD and touch controls.</summary>
        public static Camera SetUpDrivingView(VehicleController car)
        {
            var camera = Camera.main;
            camera.transform.SetPositionAndRotation(
                car.transform.position - car.transform.forward * 6.5f + Vector3.up * 2.2f,
                Quaternion.LookRotation(car.transform.forward, Vector3.up) * Quaternion.Euler(10f, 0f, 0f));
            var rig = camera.gameObject.AddComponent<VehicleCameraRig>();
            rig.SetTarget(car.transform);
            camera.gameObject.AddComponent<PlayerCameraInput>();

            var hud = new GameObject("VehicleDebugHUD").AddComponent<VehicleDebugHud>();
            hud.SetVehicle(car);
            hud.SetCameraRig(rig);
            AddTouchControls();
            return camera;
        }
    }
}
