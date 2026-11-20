using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using RealisticCarRacing.Managers;
using RealisticCarRacing.Racing;
using RealisticCarRacing.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace RealisticCarRacing.Tests
{
    /// <summary>Main menu buttons, and a complete race from countdown to results, restart and back to the menu.</summary>
    public class GameFlowTests
    {
        bool quitRequested;
        System.Action originalQuitHandler;

        [SetUp]
        public void SetUp()
        {
            GameSession.UseStorage(new MemoryStorage());
            quitRequested = false;
            originalQuitHandler = SceneFlow.QuitHandler;
            SceneFlow.QuitHandler = () => quitRequested = true; // QUIT would otherwise stop play mode
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            SceneFlow.QuitHandler = originalQuitHandler;
            GameSession.PendingRace = null;
            yield return TestScenes.ResetToEmptyScene();
        }

        /// <summary>Clicks an active button by GameObject name, like a mouse/touch click.</summary>
        static void Click(string buttonName)
        {
            foreach (var button in Object.FindObjectsByType<Button>())
            {
                if (button.name == buttonName && button.isActiveAndEnabled && button.interactable)
                {
                    button.onClick.Invoke();
                    return;
                }
            }
            Assert.Fail($"No active button '{buttonName}'");
        }

        static IEnumerator LoadScene(string name)
        {
            SceneManager.LoadScene(name);
            yield return null;
            yield return null;
        }

        static IEnumerator WaitUntilSceneLoaded(string name, float timeout = 10f)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while (SceneManager.GetActiveScene().name != name && Time.realtimeSinceStartup < end)
                yield return null;
            yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(name));
        }

        [UnityTest]
        public IEnumerator MainMenu_EveryButtonWorks_AndStartsTheChosenRace()
        {
            yield return LoadScene(SceneFlow.MainMenuScene);
            var menu = Object.FindAnyObjectByType<MainMenuController>();
            var showroom = Object.FindAnyObjectByType<ShowroomController>();
            var catalog = GameSession.Catalog;
            Assert.That(menu.Current, Is.EqualTo(MainMenuController.Screen.Main));
            Assert.That(showroom.CurrentCar, Is.Not.Null, "showroom shows the selected car");

            // Garage from the main menu: paint and setup are saved and shown on the turntable.
            Click("Button GARAGE");
            yield return null;
            Assert.That(menu.Current, Is.EqualTo(MainMenuController.Screen.Garage));
            Click("Paint 3");
            Click("Tuning Grip");
            yield return null;
            var setup = GameSession.SelectedSetup;
            Assert.That(setup.paint, Is.EqualTo(3));
            Assert.That(setup.tuning, Is.EqualTo(Vehicles.TuningPreset.Grip));
            Color shown = showroom.ShownPaint, chosen = GameSession.SelectedCar.Paint(3);
            Assert.That(Vector4.Distance(shown, chosen), Is.LessThan(0.001f), "showroom car wears the chosen paint");
            Click("Button BACK");
            yield return null;
            Assert.That(menu.Current, Is.EqualTo(MainMenuController.Screen.Main));

            // Race select (and back).
            Click("Button RACE");
            yield return null;
            Assert.That(menu.Current, Is.EqualTo(MainMenuController.Screen.RaceSelect));
            Click("Button BACK");
            yield return null;
            Assert.That(menu.Current, Is.EqualTo(MainMenuController.Screen.Main));
            Click("Button RACE");
            yield return null;
            Click("Race grand_prix");
            yield return null;
            Assert.That(menu.Current, Is.EqualTo(MainMenuController.Screen.CarSelect));
            Assert.That(GameSession.SelectedRace.id, Is.EqualTo("grand_prix"));

            // Car select: next/previous, and the garage round trip.
            Click("NextCar");
            yield return null;
            Assert.That(GameSession.SelectedCar, Is.EqualTo(catalog.cars[1]));
            Click("PrevCar");
            Click("PrevCar");
            yield return null;
            Assert.That(GameSession.SelectedCar, Is.EqualTo(catalog.cars[catalog.cars.Length - 1]), "wraps around");
            Click("NextCar");
            Click("NextCar");
            yield return null;
            Assert.That(GameSession.SelectedCar, Is.EqualTo(catalog.cars[1]));
            Click("Button GARAGE");
            yield return null;
            Assert.That(menu.Current, Is.EqualTo(MainMenuController.Screen.Garage));
            Click("Button BACK");
            yield return null;
            Assert.That(menu.Current, Is.EqualTo(MainMenuController.Screen.CarSelect));
            Click("Button BACK");
            yield return null;
            Assert.That(menu.Current, Is.EqualTo(MainMenuController.Screen.RaceSelect));
            Click("Button BACK");
            yield return null;
            Click("Button QUIT");
            Assert.That(quitRequested, Is.True, "QUIT asks the application to close");

            // Start the race: the chosen event and car must arrive in the race scene.
            Click("Button RACE");
            yield return null;
            Click("Race grand_prix");
            yield return null;
            Click("Button START RACE");
            yield return WaitUntilSceneLoaded("Circuit_GrandPrix");
            var race = Object.FindAnyObjectByType<RaceManager>();
            Assert.That(race.Race.id, Is.EqualTo("grand_prix"));
            Assert.That(race.Racers.Count, Is.EqualTo(race.Race.opponents + 1));
            Assert.That(race.Player.Car, Is.EqualTo(catalog.cars[1]));
            Assert.That(race.State, Is.EqualTo(RaceState.Countdown));
        }

        [UnityTest]
        public IEnumerator CompleteRace_FromCountdownToResults_ThenRestartAndQuitToMenu()
        {
            var sprint = GameSession.Catalog.FindRace("sprint");
            GameSession.PendingRace = sprint;
            yield return LoadScene(sprint.sceneName);
            var race = Object.FindAnyObjectByType<RaceManager>();
            var hud = Object.FindAnyObjectByType<RaceHud>();
            var menus = Object.FindAnyObjectByType<RaceMenus>();
            int fieldSize = sprint.opponents + 1;
            Assert.That(race.Racers.Count, Is.EqualTo(fieldSize));

            // Countdown: everyone held on the grid, HUD counting down.
            var gridPositions = new List<Vector3>();
            foreach (var racer in race.Racers)
                gridPositions.Add(racer.Vehicle.transform.position);
            Assert.That(race.State, Is.EqualTo(RaceState.Countdown));
            Assert.That(hud.CountdownText.text, Is.EqualTo("3"));
            yield return new WaitForSeconds(1.5f);
            Assert.That(race.State, Is.EqualTo(RaceState.Countdown));
            Assert.That(new[] { "1", "2" }, Does.Contain(hud.CountdownText.text));
            for (int i = 0; i < fieldSize; i++)
                Assert.That(Vector3.Distance(gridPositions[i], race.Racers[i].Vehicle.transform.position), Is.LessThan(0.3f), "cars must wait for the start");
            float waitEnd = Time.time + 3f;
            while (race.State == RaceState.Countdown && Time.time < waitEnd)
                yield return null;
            Assert.That(race.State, Is.EqualTo(RaceState.Racing), "GO after three seconds");
            yield return null; // the HUD redraws in its own Update
            Assert.That(hud.CountdownText.text, Is.EqualTo("GO!"));

            // Race: the player car is driven by the autopilot through the normal input interface.
            var playerLaps = new List<(int lap, float time)>();
            race.LapCompleted += (racer, lap, time, _) => { if (racer.IsPlayer) playerLaps.Add((lap, time)); };
            race.SetPlayerAutopilot(true);
            Time.timeScale = 5f;

            float lastRaceTime = 0f, nextHudCheck = 5f;
            bool pauseTested = false;
            while (race.State == RaceState.Racing && race.RaceTime < 420f)
            {
                yield return new WaitForFixedUpdate();
                Assert.That(race.RaceTime, Is.GreaterThanOrEqualTo(lastRaceTime));
                lastRaceTime = race.RaceTime;

                var positions = new HashSet<int>();
                foreach (var racer in race.Racers)
                    positions.Add(racer.Position);
                Assert.That(positions.Count, Is.EqualTo(fieldSize), "positions must be a permutation");

                if (race.RaceTime > nextHudCheck)
                {
                    nextHudCheck += 10f;
                    yield return null; // let the HUD redraw this frame
                    var car = race.Player.Vehicle;
                    Assert.That(int.Parse(hud.SpeedText.text), Is.EqualTo(car.SpeedKmh).Within(4f), "speedometer");
                    Assert.That(hud.GearText.text, Is.EqualTo(car.GearLabel), "gear indicator");
                    Assert.That(hud.RpmFill.fillAmount, Is.EqualTo(Mathf.Clamp01(car.EngineRpm / car.Settings.redlineRpm) * 0.75f).Within(0.05f), "RPM ring");
                    Assert.That(hud.PositionText.text, Is.EqualTo($"{race.Player.Position}/{fieldSize}"), "position");
                    Assert.That(hud.LapText.text, Is.EqualTo($"{race.Player.CompletedLaps + 1}/{sprint.laps}"), "lap counter");
                }

                if (!pauseTested && race.RaceTime > 30f)
                {
                    pauseTested = true;
                    race.SetPaused(true);
                    float frozen = race.RaceTime;
                    for (int i = 0; i < 10; i++)
                        yield return null;
                    Assert.That(Time.timeScale, Is.EqualTo(0f));
                    Assert.That(race.RaceTime, Is.EqualTo(frozen), "time stops while paused");
                    Assert.That(menus.PausePanel.activeSelf, Is.True);
                    Click("Button RESUME");
                    Assert.That(menus.PausePanel.activeSelf, Is.False);
                    Assert.That(Time.timeScale, Is.EqualTo(5f));
                }
            }

            var player = race.Player;
            Assert.That(race.State, Is.EqualTo(RaceState.Finished), $"player did not finish within 420 s (laps {player.CompletedLaps})");
            Assert.That(playerLaps.Count, Is.EqualTo(sprint.laps));
            Assert.That(playerLaps[0].lap, Is.EqualTo(1));
            Assert.That(player.Progress.HighestCheckpoint, Is.EqualTo(sprint.laps * player.Progress.CheckpointsPerLap));
            float lapSum = 0f;
            foreach (var (_, time) in playerLaps)
            {
                Assert.That(time, Is.InRange(70f, 160f), "plausible lap time");
                lapSum += time;
            }
            Assert.That(player.FinishTime, Is.EqualTo(lapSum).Within(0.05f), "race time = sum of laps");
            Debug.Log($"Player finished P{player.Position}/{fieldSize} in {RaceTimeFormat.Format(player.FinishTime)}, best lap {RaceTimeFormat.Format(player.BestLap)}");

            // Completion screen, then results.
            yield return new WaitForSecondsRealtime(2f);
            Assert.That(menus.CompletionPanel.activeSelf, Is.True, "race completion screen");
            Click("Button VIEW RESULTS");
            Assert.That(menus.ResultsPanel.activeSelf, Is.True, "results screen");

            float classifyEnd = race.RaceTime + 120f;
            while (!race.ResultsFinal && race.RaceTime < classifyEnd)
                yield return new WaitForFixedUpdate();
            Assert.That(race.ResultsFinal, Is.True);
            yield return new WaitForSecondsRealtime(0.6f);
            foreach (var racer in race.Racers)
                Debug.Log($"  P{racer.Position} {racer.Name,-14} {racer.Car.displayName,-13} finished={racer.Finished} " +
                          $"time={RaceTimeFormat.Format(racer.FinishTime)} best={RaceTimeFormat.Format(racer.BestLap)} respawns={racer.Respawns}");
            var record = GameSession.Profile.RecordFor(sprint.id);
            Assert.That(record.starts, Is.EqualTo(1));
            Assert.That(record.bestLap, Is.EqualTo(player.BestLap));

            // Restart reloads the same race from the countdown (asynchronously, behind the loading screen).
            Click("Button RESTART");
            RaceManager restarted = null;
            float reloadEnd = Time.realtimeSinceStartup + 15f;
            while (Time.realtimeSinceStartup < reloadEnd && (restarted == null || restarted == race))
            {
                yield return null;
                restarted = Object.FindAnyObjectByType<RaceManager>();
            }
            yield return null;
            Assert.That(restarted, Is.Not.EqualTo(race));
            Assert.That(restarted.State, Is.EqualTo(RaceState.Countdown));
            Assert.That(restarted.Race, Is.EqualTo(sprint));
            Assert.That(Time.timeScale, Is.EqualTo(1f));

            // Pause menu → Main menu.
            yield return null;
            Object.FindAnyObjectByType<RaceHud>().PauseButton.onClick.Invoke();
            Assert.That(restarted.IsPaused, Is.True);
            Click("Button MAIN MENU");
            yield return WaitUntilSceneLoaded(SceneFlow.MainMenuScene);
            Assert.That(Object.FindAnyObjectByType<MainMenuController>(), Is.Not.Null);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }
    }
}
