using System.Collections;
using System.IO;
using NUnit.Framework;
using RealisticCarRacing.Managers;
using RealisticCarRacing.Racing;
using RealisticCarRacing.Vehicles;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RealisticCarRacing.Tests
{
    /// <summary>
    /// Renders a fixed set of Game-camera views of the circuit (cars held on the grid) for before/after comparisons.
    /// Explicit: only runs when asked for, e.g.
    /// <c>CAPTURE_DIR=/path CAPTURE_TAG=after Unity -runTests -testPlatform PlayMode -testFilter VisualCaptureTests</c>
    /// </summary>
    [Explicit("Writes screenshots; run on demand")]
    public class VisualCaptureTests
    {
        const int Width = 1920, Height = 1080;

        [UnityTest]
        public IEnumerator CaptureCircuitViews([Values("High", "Low")] string preset)
        {
            string directory = System.Environment.GetEnvironmentVariable("CAPTURE_DIR");
            string tag = System.Environment.GetEnvironmentVariable("CAPTURE_TAG") ?? "capture";
            Assume.That(!string.IsNullOrEmpty(directory), "set CAPTURE_DIR");
            Directory.CreateDirectory(directory);

            GameSession.UseStorage(new MemoryStorage());
            var race = ScriptableObject.CreateInstance<RaceDefinition>();
            race.id = "capture";
            race.laps = 3;
            race.opponents = 5;
            GameSession.PendingRace = race;
            SceneManager.LoadScene("Circuit_GrandPrix");
            yield return null;
            yield return null;
            var manager = Object.FindAnyObjectByType<RaceManager>();
            manager.SetPaused(true); // everyone stays on the grid: identical frames every run
            GraphicsSettingsService.SetPreset(System.Array.IndexOf(QualitySettings.names, preset));
            // Still frames: camera-motion blur would smear the jump between viewpoints.
            var volume = Object.FindAnyObjectByType<UnityEngine.Rendering.Volume>();
            if (volume != null && volume.profile.TryGet(out UnityEngine.Rendering.Universal.MotionBlur motionBlur))
                motionBlur.intensity.Override(0f);
            for (int i = 0; i < 5; i++)
                yield return null;

            var camera = Camera.main;
            var rig = camera.GetComponent<Cameras.VehicleCameraRig>();
            rig.enabled = false; // views are placed by hand
            var car = manager.Player.Vehicle.transform;
            var track = manager.Track;
            Vector3 forward = car.forward, right = car.right, up = Vector3.up;

            var apexIndex = 0;
            for (int i = 0; i < track.SampleCount; i++)
                if (Mathf.Abs(track.GetSample(i).curvature) > Mathf.Abs(track.GetSample(apexIndex).curvature)) apexIndex = i;
            var apex = track.GetSample(apexIndex);
            var entry = track.GetSample(apexIndex - 35);
            float outside = apex.curvature > 0 ? 1f : -1f;
            var straight = track.GetSample(60);

            var views = new (string name, Vector3 position, Vector3 target, float fov, bool cockpit)[]
            {
                ("1_chase", car.position - forward * 6.5f + up * 2.2f, car.position + forward * 10f + up * 0.9f, 60f, false),
                ("2_car_closeup", car.position + forward * 3.2f - right * 3.4f + up * 0.9f, car.position + up * 0.6f, 45f, false),
                ("3_corner", entry.position + entry.right * (outside * 7f) + up * 1.6f, apex.position + up * 0.5f, 60f, false),
                ("4_straight_wide", straight.position - straight.right * 11f + up * 5f, straight.position + straight.forward * 120f, 60f, false),
                ("5_cockpit", Vector3.zero, Vector3.zero, 65f, true)
            };

            var target = new RenderTexture(Width, Height, 24);
            var texture = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            foreach (var view in views)
            {
                var mounts = car.GetComponent<VehicleCameraMounts>();
                mounts.SetInteriorVisible(view.cockpit);
                if (view.cockpit)
                    camera.transform.SetPositionAndRotation(mounts.Cockpit.position, mounts.Cockpit.rotation);
                else
                    camera.transform.SetPositionAndRotation(view.position, Quaternion.LookRotation(view.target - view.position, up));
                camera.fieldOfView = view.fov;
                camera.nearClipPlane = view.cockpit ? 0.03f : 0.3f;
                for (int i = 0; i < 3; i++)
                    yield return null; // let instanced trees, LODs and temporal effects settle

                camera.targetTexture = target;
                camera.Render();
                camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                camera.targetTexture = null;
                RenderTexture.active = null;
                File.WriteAllBytes(Path.Combine(directory, $"{tag}_{preset}_{view.name}.png"), texture.EncodeToPNG());
            }
            Object.Destroy(texture);
            target.Release();
            manager.SetPaused(false);
            Object.Destroy(race);
            yield return TestScenes.ResetToEmptyScene();
        }

        [UnityTest]
        public IEnumerator CaptureGarage([Values("High", "Low")] string preset)
        {
            string directory = System.Environment.GetEnvironmentVariable("CAPTURE_DIR");
            string tag = System.Environment.GetEnvironmentVariable("CAPTURE_TAG") ?? "capture";
            Assume.That(!string.IsNullOrEmpty(directory), "set CAPTURE_DIR");
            Directory.CreateDirectory(directory);

            GameSession.UseStorage(new MemoryStorage());
            SceneManager.LoadScene(SceneFlow.MainMenuScene);
            yield return null;
            yield return null;
            GraphicsSettingsService.SetPreset(System.Array.IndexOf(QualitySettings.names, preset));
            var menu = Object.FindAnyObjectByType<UI.MainMenuController>();
            var orbit = Object.FindAnyObjectByType<UI.ShowroomOrbit>();
            var capture = new UiCapture(Camera.main, Width, Height);
            var shots = new (string name, UI.MainMenuController.Screen screen, UI.MainMenuController.GarageTab tab, UI.ShowroomView view, float zoom, int car, int paint, int wheels)[]
            {
                ("1_main", UI.MainMenuController.Screen.Main, default, UI.ShowroomView.ThreeQuarter, 1f, 0, 0, 0),
                ("2_garage_paint", UI.MainMenuController.Screen.Garage, UI.MainMenuController.GarageTab.Paint, UI.ShowroomView.ThreeQuarter, 1f, 0, 0, 0),
                ("3_front_wheels", UI.MainMenuController.Screen.Garage, UI.MainMenuController.GarageTab.Wheels, UI.ShowroomView.Front, 1f, 1, 1, 1),
                ("4_side_specs", UI.MainMenuController.Screen.Garage, UI.MainMenuController.GarageTab.Specs, UI.ShowroomView.Side, 1f, 2, 4, 3),
                ("5_rear_setup", UI.MainMenuController.Screen.Garage, UI.MainMenuController.GarageTab.Setup, UI.ShowroomView.Rear, 1f, 0, 2, 2),
                ("6_wheel_closeup", UI.MainMenuController.Screen.Garage, UI.MainMenuController.GarageTab.Wheels, UI.ShowroomView.Side, 0.5f, 0, 0, 2)
            };
            foreach (var shot in shots)
            {
                var car = GameSession.Catalog.cars[shot.car];
                GameSession.SelectCar(car);
                GameSession.SelectedSetup.paint = shot.paint;
                GameSession.SelectedSetup.wheels = shot.wheels;
                menu.Show(shot.screen);
                menu.ShowTab(shot.tab);
                if (orbit != null)
                {
                    orbit.SetView(shot.view);
                    orbit.ZoomBy(shot.zoom);
                    orbit.Settle();
                }
                for (int i = 0; i < 4; i++)
                    yield return null;
                capture.Save(Path.Combine(directory, $"{tag}_{preset}_garage_{shot.name}.png"));
            }
            capture.Dispose();
            yield return TestScenes.ResetToEmptyScene();
        }

        /// <summary>
        /// Renders a camera together with the screen-space UI (moved onto an overlay camera without post-processing,
        /// so the UI keeps its colours) into a PNG.
        /// </summary>
        sealed class UiCapture : System.IDisposable
        {
            readonly Camera camera;
            readonly Camera uiCamera;
            readonly RenderTexture target, uiTarget;
            readonly Texture2D texture, uiTexture;

            public UiCapture(Camera camera, int width, int height)
            {
                this.camera = camera;
                target = new RenderTexture(width, height, 24);
                texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                uiTarget = new RenderTexture(width, height, 24);
                uiTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                int uiLayer = LayerMask.NameToLayer("UI");
                // UI drawn separately onto transparent black (no post-processing), then laid over the scene image.
                uiCamera = new GameObject("CaptureUiCamera").AddComponent<Camera>();
                uiCamera.clearFlags = CameraClearFlags.SolidColor;
                uiCamera.backgroundColor = Color.clear;
                uiCamera.cullingMask = 1 << uiLayer;
                uiCamera.targetTexture = uiTarget;
                uiCamera.enabled = false;
                UnityEngine.Rendering.Universal.CameraExtensions.GetUniversalAdditionalCameraData(uiCamera).renderPostProcessing = false;
                camera.cullingMask &= ~(1 << uiLayer);
                camera.targetTexture = target;
                foreach (var canvas in Object.FindObjectsByType<Canvas>())
                {
                    if (!canvas.isRootCanvas || canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                        continue;
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = uiCamera;
                    canvas.planeDistance = 1f;
                    foreach (var t in canvas.GetComponentsInChildren<Transform>(true))
                        t.gameObject.layer = uiLayer;
                }
            }

            public void Save(string path)
            {
                camera.Render();
                camera.Render();
                Canvas.ForceUpdateCanvases();
                uiCamera.Render();
                Read(target, texture);
                Read(uiTarget, uiTexture);
                var scene = texture.GetPixels32();
                var ui = uiTexture.GetPixels32();
                for (int i = 0; i < scene.Length; i++)
                {
                    // UI output is premultiplied by alpha.
                    int keep = 255 - ui[i].a;
                    scene[i] = new Color32((byte)Mathf.Min(255, ui[i].r + scene[i].r * keep / 255), (byte)Mathf.Min(255, ui[i].g + scene[i].g * keep / 255),
                        (byte)Mathf.Min(255, ui[i].b + scene[i].b * keep / 255), 255);
                }
                texture.SetPixels32(scene);
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }

            static void Read(RenderTexture source, Texture2D destination)
            {
                RenderTexture.active = source;
                destination.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
                RenderTexture.active = null;
            }

            public void Dispose()
            {
                camera.targetTexture = null;
                Object.Destroy(uiCamera.gameObject);
                Object.Destroy(texture);
                Object.Destroy(uiTexture);
                target.Release();
                uiTarget.Release();
            }
        }
    }
}
