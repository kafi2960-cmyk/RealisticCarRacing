using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using RealisticCarRacing.Racing;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RealisticCarRacing.Managers
{
    /// <summary>
    /// Command-line benchmark for player builds: for each quality level, measures the garage (car turning on the
    /// turntable), then loads the circuit with a 6-car race, lets the autopilot drive the player car and records
    /// <see cref="PerformanceReport"/>s to a JSON file, then quits.
    /// <para>Usage: <c>RealisticCarRacing -benchmark -benchmarkPresets Low,Medium,High -benchmarkSeconds 30 -benchmarkOutput results.json</c></para>
    /// Inert unless <c>-benchmark</c> is on the command line.
    /// </summary>
    public sealed class PerformanceBenchmark : MonoBehaviour
    {
        [Serializable]
        sealed class Results
        {
            public string startedUtc;
            public List<PerformanceReport> reports = new List<PerformanceReport>();
        }

        string[] presets;
        float seconds = 30f;
        string outputPath;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void StartIfRequested()
        {
            var args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "-benchmark") < 0)
                return;
            var runner = new GameObject("PerformanceBenchmark").AddComponent<PerformanceBenchmark>();
            DontDestroyOnLoad(runner.gameObject);
            runner.presets = Argument(args, "-benchmarkPresets", string.Join(",", QualitySettings.names)).Split(',');
            runner.seconds = float.Parse(Argument(args, "-benchmarkSeconds", "30"), System.Globalization.CultureInfo.InvariantCulture);
            runner.outputPath = Argument(args, "-benchmarkOutput", Path.Combine(Application.persistentDataPath, "benchmark.json"));
        }

        static string Argument(string[] args, string name, string fallback)
        {
            int index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback;
        }

        IEnumerator Start()
        {
            Application.runInBackground = true; // keep measuring even when the window loses focus
            GameSession.UseStorage(new MemoryStorage());
            var results = new Results { startedUtc = DateTime.UtcNow.ToString("u") };
            var race = ScriptableObject.CreateInstance<RaceDefinition>();
            race.id = "benchmark";
            race.displayName = "Benchmark";
            race.laps = 10;
            race.opponents = 5;
            race.difficulty = AIDifficulty.Medium;

            foreach (string preset in presets)
            {
                int level = Array.IndexOf(QualitySettings.names, preset.Trim());
                if (level < 0)
                {
                    Debug.LogWarning($"Benchmark: unknown quality level '{preset}'");
                    continue;
                }
                GraphicsSettingsHook?.Invoke(level);
                if (GraphicsSettingsHook == null)
                    QualitySettings.SetQualityLevel(level, true);
                // Measure headroom, not the display's refresh rate.
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = -1;

                yield return MeasureGarage(preset, Mathf.Min(seconds, 15f), results);

                GameSession.PendingRace = race;
                float loadSeconds = 0f;
                yield return LoadScene(race.sceneName, s => loadSeconds = s);
                var manager = FindAnyObjectByType<RaceManager>();
                manager.SkipCountdown();
                yield return null;
                manager.SetPlayerAutopilot(true);
                yield return new WaitForSecondsRealtime(6f); // warm-up: shaders, streaming, pack spreads out

                using (var sampler = new PerformanceSampler())
                {
                    float end = Time.realtimeSinceStartup + seconds;
                    while (Time.realtimeSinceStartup < end)
                    {
                        yield return null;
                        sampler.SampleFrame();
                    }
                    var report = sampler.Report($"{preset} · race (6 cars)");
                    report.sceneLoadSeconds = loadSeconds;
                    results.reports.Add(report);
                    Debug.Log($"Benchmark {report.label}: {report.averageFps:0.0} fps avg, {report.medianFrameMs:0.00} ms median, " +
                              $"{report.onePercentLowFps:0.0} fps 1% low, batches {report.averageBatches:0}, load {loadSeconds:0.00} s");
                }
            }

            File.WriteAllText(outputPath, JsonUtility.ToJson(results, true));
            Debug.Log($"Benchmark: wrote {outputPath}");
            Application.Quit();
        }

        /// <summary>Lets the graphics settings service apply a preset with all its extras (set by that service).</summary>
        public static Action<int> GraphicsSettingsHook { get; set; }

        static IEnumerator LoadScene(string scene, Action<float> loaded)
        {
            float start = Time.realtimeSinceStartup;
            var operation = SceneManager.LoadSceneAsync(scene);
            while (!operation.isDone)
                yield return null;
            yield return null; // first frame rendered
            loaded(Time.realtimeSinceStartup - start);
        }

        /// <summary>Main menu load time, then the garage with the car turning on the turntable.</summary>
        static IEnumerator MeasureGarage(string preset, float seconds, Results results)
        {
            float loadSeconds = 0f;
            yield return LoadScene(SceneFlow.MainMenuScene, s => loadSeconds = s);
            FindAnyObjectByType<UI.MainMenuController>().Show(UI.MainMenuController.Screen.Garage);
            FindAnyObjectByType<UI.ShowroomOrbit>().AutoSpin = true; // every side of the car, as a player turning it would see
            yield return new WaitForSecondsRealtime(4f);

            using (var sampler = new PerformanceSampler())
            {
                float end = Time.realtimeSinceStartup + seconds;
                while (Time.realtimeSinceStartup < end)
                {
                    yield return null;
                    sampler.SampleFrame();
                }
                var report = sampler.Report($"{preset} · garage");
                report.sceneLoadSeconds = loadSeconds;
                results.reports.Add(report);
                Debug.Log($"Benchmark {report.label}: {report.averageFps:0.0} fps avg, {report.medianFrameMs:0.00} ms median, " +
                          $"{report.onePercentLowFps:0.0} fps 1% low, batches {report.averageBatches:0}, load {loadSeconds:0.00} s");
            }
        }
    }
}
