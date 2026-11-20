using System.Collections;
using NUnit.Framework;
using RealisticCarRacing.Managers;
using RealisticCarRacing.Racing;
using RealisticCarRacing.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace RealisticCarRacing.Tests
{
    /// <summary>
    /// Menus and the pause menu driven by each input method separately, with simulated devices:
    /// keyboard (arrows / Enter / Esc), gamepad (D-pad / A / B / Start) and touch (taps).
    /// </summary>
    public class MenuInputTests : InputTestFixture
    {
        public override void Setup()
        {
            base.Setup();
            GameSession.UseStorage(new MemoryStorage());
        }

        [UnityTearDown]
        public IEnumerator UnloadScenes()
        {
            GameSession.PendingRace = null;
            yield return TestScenes.ResetToEmptyScene();
        }

        public override void TearDown()
        {
            Time.timeScale = 1f;
            base.TearDown();
        }

        static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++)
                yield return null;
        }

        static IEnumerator LoadMenu()
        {
            SceneManager.LoadScene(SceneFlow.MainMenuScene);
            yield return Frames(3);
        }

        static IEnumerator WaitForRace()
        {
            float end = Time.realtimeSinceStartup + 10f;
            while (Object.FindAnyObjectByType<RaceManager>() == null && Time.realtimeSinceStartup < end)
                yield return null;
            yield return Frames(3);
        }

        /// <summary>Holds a key/button for two frames, as a real press does (the UI module samples it while held).</summary>
        IEnumerator Hold(UnityEngine.InputSystem.Controls.ButtonControl control)
        {
            Press(control);
            yield return Frames(2);
            Release(control);
            yield return Frames(2);
        }

        static string Selected => EventSystem.current.currentSelectedGameObject != null ? EventSystem.current.currentSelectedGameObject.name : "(none)";

        static Vector2 ScreenCenter(GameObject target)
        {
            var rect = (RectTransform)target.transform;
            return RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
        }

        static GameObject Find(string name)
        {
            foreach (var button in Object.FindObjectsByType<Button>())
            {
                if (button.name == name && button.isActiveAndEnabled)
                    return button.gameObject;
            }
            Assert.Fail($"No active button '{name}'");
            return null;
        }

        [UnityTest]
        public IEnumerator Keyboard_NavigatesMenus_StartsRace_PausesAndResumes()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            yield return LoadMenu();
            var menu = Object.FindAnyObjectByType<MainMenuController>();
            Assert.That(Selected, Is.EqualTo("Button RACE"));

            yield return Hold(keyboard.downArrowKey);
            Assert.That(Selected, Is.EqualTo("Button GARAGE"), "arrow keys move the selection");
            yield return Hold(keyboard.enterKey);
            Assert.That(menu.Current, Is.EqualTo(MainMenuController.Screen.Garage), "Enter presses the selected button");
            yield return Hold(keyboard.escapeKey);
            Assert.That(menu.Current, Is.EqualTo(MainMenuController.Screen.Main), "Esc goes back");

            // RACE → first race → START RACE, all with Enter.
            yield return Hold(keyboard.enterKey);
            Assert.That(menu.Current, Is.EqualTo(MainMenuController.Screen.RaceSelect));
            yield return Hold(keyboard.enterKey);
            Assert.That(menu.Current, Is.EqualTo(MainMenuController.Screen.CarSelect));
            Assert.That(Selected, Is.EqualTo("Button START RACE"));
            yield return Hold(keyboard.enterKey);
            yield return WaitForRace();

            var race = Object.FindAnyObjectByType<RaceManager>();
            yield return Hold(keyboard.escapeKey);
            Assert.That(race.IsPaused, Is.True, "Esc pauses");
            Assert.That(Selected, Is.EqualTo("Button RESUME"));
            yield return Hold(keyboard.escapeKey);
            Assert.That(race.IsPaused, Is.False, "Esc resumes");
            yield return TestScenes.ResetToEmptyScene();
        }

        [UnityTest]
        public IEnumerator Gamepad_NavigatesMenus_StartsRace_PausesAndResumes()
        {
            var gamepad = InputSystem.AddDevice<Gamepad>();
            yield return LoadMenu();
            var menu = Object.FindAnyObjectByType<MainMenuController>();

            yield return Hold(gamepad.dpad.down);
            Assert.That(Selected, Is.EqualTo("Button GARAGE"), "D-pad moves the selection");
            yield return Hold(gamepad.buttonSouth);
            Assert.That(menu.Current, Is.EqualTo(MainMenuController.Screen.Garage), "A presses");
            yield return Hold(gamepad.buttonEast);
            Assert.That(menu.Current, Is.EqualTo(MainMenuController.Screen.Main), "B goes back");

            yield return Hold(gamepad.buttonSouth);   // RACE
            yield return Hold(gamepad.buttonSouth);   // first race
            yield return Hold(gamepad.buttonSouth);   // START RACE
            yield return WaitForRace();

            var race = Object.FindAnyObjectByType<RaceManager>();
            yield return Hold(gamepad.startButton);
            Assert.That(race.IsPaused, Is.True, "Start pauses");
            yield return Hold(gamepad.buttonEast);
            Assert.That(race.IsPaused, Is.False, "B resumes");
            yield return Hold(gamepad.startButton);
            yield return Hold(gamepad.buttonSouth);   // RESUME is selected
            Assert.That(race.IsPaused, Is.False, "A on RESUME resumes");
            yield return TestScenes.ResetToEmptyScene();
        }

        [UnityTest]
        public IEnumerator Touch_TapsThroughMenus_StartsRace_PausesAndResumes()
        {
            InputSystem.AddDevice<Touchscreen>();
            yield return LoadMenu();
            var menu = Object.FindAnyObjectByType<MainMenuController>();

            int touchId = 1;
            IEnumerator Tap(string buttonName)
            {
                Vector2 position = ScreenCenter(Find(buttonName));
                BeginTouch(touchId, position);
                yield return Frames(2);
                EndTouch(touchId, position);
                touchId++;
                yield return Frames(3);
            }

            yield return Tap("Button RACE");
            Assert.That(menu.Current, Is.EqualTo(MainMenuController.Screen.RaceSelect));
            yield return Tap("Race sprint");
            Assert.That(menu.Current, Is.EqualTo(MainMenuController.Screen.CarSelect));
            yield return Tap("NextCar");
            Assert.That(GameSession.SelectedCar, Is.EqualTo(GameSession.Catalog.cars[1]));
            yield return Tap("Button START RACE");
            yield return WaitForRace();

            var race = Object.FindAnyObjectByType<RaceManager>();
            Assert.That(race.Race.id, Is.EqualTo("sprint"));
            yield return Tap("PauseButton");
            Assert.That(race.IsPaused, Is.True, "on-screen pause button");
            yield return Tap("Button RESUME");
            Assert.That(race.IsPaused, Is.False, "tap RESUME");
            // Unload while the simulated devices still exist: the on-screen controls remove their virtual gamepad on disable.
            yield return TestScenes.ResetToEmptyScene();
        }
    }
}
