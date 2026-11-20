using System;
using RealisticCarRacing.Racing;
using UnityEngine;

namespace RealisticCarRacing.Managers
{
    /// <summary>Moves between the main menu and race scenes (asynchronously, behind the loading screen).</summary>
    public static class SceneFlow
    {
        public const string MainMenuScene = "MainMenu";

        /// <summary>What the Quit button does; replaceable for tests.</summary>
        public static Action QuitHandler { get; set; } = DefaultQuit;

        public static void LoadMainMenu()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            UI.LoadingScreen.Load(MainMenuScene);
        }

        public static void StartRace(RaceDefinition race)
        {
            GameSession.PendingRace = race;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            UI.LoadingScreen.Load(race.sceneName);
        }

        public static void RestartRace() => StartRace(GameSession.SelectedRace);

        public static void Quit() => QuitHandler();

        static void DefaultQuit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
