using System.Collections;
using NUnit.Framework;
using RealisticCarRacing.Cameras;
using RealisticCarRacing.Managers;
using RealisticCarRacing.Racing;
using RealisticCarRacing.Tracks;
using RealisticCarRacing.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;

namespace RealisticCarRacing.Tests
{
    /// <summary>Graphics settings service, both settings menus, and the presets scaling a live race scene.</summary>
    public class GraphicsSettingsTests
    {
        int originalLevel;

        [SetUp]
        public void SetUp()
        {
            originalLevel = QualitySettings.GetQualityLevel();
            GameSession.UseStorage(new MemoryStorage());
            GraphicsSettingsService.Preferences.preset = 2;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            GraphicsSettingsService.SetShowPerformance(false);
            GraphicsSettingsService.SetPreset(originalLevel);
            yield return TestScenes.ResetToEmptyScene();
        }

        static void Click(string name)
        {
            foreach (var button in Object.FindObjectsByType<Button>())
            {
                if (button.name == name && button.isActiveAndEnabled)
                {
                    button.onClick.Invoke();
                    return;
                }
            }
            Assert.Fail($"No active button '{name}'");
        }

        [UnityTest]
        public IEnumerator Service_AppliesPresetScaleFrameRateAndOverlay()
        {
            GraphicsSettingsService.SetPreset(0);
            Assert.That(QualitySettings.GetQualityLevel(), Is.EqualTo(0));
            float lowAssetScale = ((UniversalRenderPipelineAsset)GraphicsSettingsService.PresetAsset(0)).renderScale;
            Assert.That(GraphicsSettingsService.EffectiveRenderScale, Is.EqualTo(lowAssetScale));

            GraphicsSettingsService.SetRenderScale(0.6f);
            yield return null;
            Assert.That(GraphicsSettingsService.ActivePipeline.renderScale, Is.EqualTo(0.6f).Within(0.001f), "override applies");
            Assert.That(((UniversalRenderPipelineAsset)GraphicsSettingsService.PresetAsset(0)).renderScale, Is.EqualTo(lowAssetScale),
                "the project's asset is never modified");
            GraphicsSettingsService.ResetRenderScale();
            yield return null;
            Assert.That(GraphicsSettingsService.ActivePipeline, Is.SameAs(GraphicsSettingsService.PresetAsset(0)), "back on the preset's own asset");

            // Every preset must render with URP (a null pipeline would silently fall back to the built-in renderer).
            for (int preset = 0; preset < 3; preset++)
            {
                GraphicsSettingsService.SetPreset(preset);
                yield return null;
                Assert.That(GraphicsSettingsService.ActivePipeline, Is.Not.Null, $"preset {preset} has a URP asset");
                Assert.That(GraphicsSettingsService.ActivePipeline, Is.SameAs(GraphicsSettingsService.PresetAsset(preset)));
            }
            GraphicsSettingsService.SetPreset(0);

            GraphicsSettingsService.SetFrameRate(30);
            Assert.That(Application.targetFrameRate, Is.EqualTo(30));
            GraphicsSettingsService.SetFrameRate(-1);
            Assert.That(Application.targetFrameRate, Is.EqualTo(-1));

            GraphicsSettingsService.SetShowPerformance(true);
            yield return new WaitForSecondsRealtime(0.7f);
            var overlay = Object.FindAnyObjectByType<PerformanceOverlay>();
            Assert.That(overlay, Is.Not.Null);
            Assert.That(overlay.LastText, Does.Contain("FPS").And.Contain("Low"));
            GraphicsSettingsService.SetShowPerformance(false);
            yield return null;
            Assert.That(Object.FindAnyObjectByType<PerformanceOverlay>(), Is.Null);

            GraphicsSettingsService.SetPreset(2);
            Assert.That(QualitySettings.GetQualityLevel(), Is.EqualTo(2));
            Assert.That(GameSession.Profile.graphics.preset, Is.EqualTo(2), "saved");
        }

        [UnityTest]
        public IEnumerator MainMenu_SettingsScreen_ChangesAndSavesGraphics()
        {
            SceneManager.LoadScene(SceneFlow.MainMenuScene);
            yield return null;
            yield return null;
            var menu = Object.FindAnyObjectByType<MainMenuController>();
            Click("Button SETTINGS");
            yield return null;
            Assert.That(menu.Current, Is.EqualTo(MainMenuController.Screen.Settings));

            Click("Preset LOW");
            Assert.That(QualitySettings.GetQualityLevel(), Is.EqualTo(0));
            float before = GraphicsSettingsService.EffectiveRenderScale;
            Click("ScaleUp");
            Assert.That(GraphicsSettingsService.EffectiveRenderScale, Is.EqualTo(before + 0.05f).Within(0.001f));
            Click("ScaleDown");
            Click("ScaleDown");
            Assert.That(GraphicsSettingsService.EffectiveRenderScale, Is.EqualTo(before - 0.05f).Within(0.001f));
            Click("ScaleReset");
            Assert.That(GraphicsSettingsService.Preferences.renderScale, Is.EqualTo(0f));
            Click("Fps 30");
            Assert.That(Application.targetFrameRate, Is.EqualTo(30));
            Click("Fps MAX");
            Assert.That(Application.targetFrameRate, Is.EqualTo(-1));
            Click("ShowPerformance");
            Assert.That(GraphicsSettingsService.Preferences.showPerformance, Is.True);
            Click("ShowPerformance");
            Click("Preset MEDIUM");
            Assert.That(QualitySettings.GetQualityLevel(), Is.EqualTo(1));
            Click("Preset HIGH");
            Assert.That(QualitySettings.GetQualityLevel(), Is.EqualTo(2));

            Click("Button BACK");
            yield return null;
            Assert.That(menu.Current, Is.EqualTo(MainMenuController.Screen.Main));
            var saved = ProfileStore.Load(GameSession.Storage).graphics;
            Assert.That(saved.preset, Is.EqualTo(2));
            Assert.That(saved.frameRateLimit, Is.EqualTo(-1));
        }

        [UnityTest]
        public IEnumerator Race_PresetsScaleTheSceneAndPauseMenuHasSettings()
        {
            var race = ScriptableObject.CreateInstance<RaceDefinition>();
            race.id = "graphics_test";
            race.laps = 3;
            race.opponents = 5;
            GameSession.PendingRace = race;
            SceneManager.LoadScene("Circuit_GrandPrix");
            yield return null;
            yield return null;
            var manager = Object.FindAnyObjectByType<RaceManager>();
            var camera = Camera.main;
            var scaler = Object.FindAnyObjectByType<EnvironmentQualityScaler>();
            var trees = Object.FindObjectsByType<InstancedScatter>();
            var skyline = GameObject.Find("HighDetailScenery");

            int background = LayerMask.NameToLayer(EnvironmentQualityScaler.BackgroundLayer);
            var hills = GameObject.Find("DistantHills");
            GraphicsSettingsService.SetPreset(0);
            yield return null;
            Assert.That(camera.layerCullDistances[0], Is.EqualTo(900f), "ordinary objects culled at 900 m on Low");
            Assert.That(hills.layer, Is.EqualTo(background));
            Assert.That(camera.layerCullDistances[background], Is.EqualTo(0f), "horizon layer uses the full far plane");
            Assert.That(camera.farClipPlane, Is.GreaterThan(2700f), "the hills (out to 2.7 km) stay visible on Low");
            Assert.That(RenderSettings.fogEndDistance, Is.EqualTo(2800f), "same fog (look) on every preset");
            Assert.That(trees[0].Density, Is.EqualTo(0.35f).Within(0.001f));
            Assert.That(trees[0].ShadowDistance, Is.EqualTo(0f));
            Assert.That(skyline.activeSelf, Is.False, "skyline off on Low");
            Assert.That(GameObject.Find("DistantHills"), Is.Not.Null, "hills stay on every preset");

            GraphicsSettingsService.SetPreset(2);
            yield return null;
            Assert.That(camera.layerCullDistances[0], Is.EqualTo(3200f));
            Assert.That(trees[0].Density, Is.EqualTo(1f));
            Assert.That(skyline.activeSelf, Is.True);
            Assert.That(scaler.ActiveTier.qualityLevel, Is.EqualTo("High"));

            // Optimised cars: interior hidden from the chase camera, distant cars managed by LOD.
            foreach (var racer in manager.Racers)
            {
                var mounts = racer.Vehicle.GetComponent<Vehicles.VehicleCameraMounts>();
                Assert.That(mounts.InteriorVisible, Is.False, $"{racer.Name} interior hidden");
                Assert.That(racer.Vehicle.GetComponent<LODGroup>(), Is.Not.Null);
                int interiorRenderers = racer.Vehicle.transform.Find("Interior").GetComponentsInChildren<MeshRenderer>().Length;
                Assert.That(interiorRenderers, Is.LessThanOrEqualTo(4), "interior merged into a few renderers");
            }
            Object.FindAnyObjectByType<VehicleCameraRig>().SetView(CameraView.Cockpit);
            Assert.That(manager.Player.Vehicle.GetComponent<Vehicles.VehicleCameraMounts>().InteriorVisible, Is.True);

            // Pause → SETTINGS → change preset → BACK returns to the pause menu.
            var menus = Object.FindAnyObjectByType<RaceMenus>();
            manager.SetPaused(true);
            yield return null;
            Click("Button SETTINGS");
            Assert.That(menus.SettingsPanel.activeSelf, Is.True);
            Click("Preset MEDIUM");
            Assert.That(QualitySettings.GetQualityLevel(), Is.EqualTo(1));
            Assert.That(camera.layerCullDistances[0], Is.EqualTo(1700f));
            Click("Button BACK");
            Assert.That(menus.SettingsPanel.activeSelf, Is.False);
            Assert.That(menus.PausePanel.activeSelf, Is.True);
            manager.SetPaused(false);
            Object.Destroy(race);
        }
    }
}
