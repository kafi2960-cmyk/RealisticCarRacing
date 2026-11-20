using System.Collections;
using System.IO;
using NUnit.Framework;
using RealisticCarRacing.Audio;
using RealisticCarRacing.Managers;
using RealisticCarRacing.Racing;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RealisticCarRacing.Tests
{
    /// <summary>
    /// The game's real recordings: every car's engine set covers its whole rev range. Skipped where the licensed
    /// recordings aren't installed (a fresh clone: see Tools/Audio/prepare_sonniss_audio.sh).
    /// </summary>
    public class AudioContentTests
    {
        [Test]
        public void EveryCarsEngineLoopsCoverIdleToRedline()
        {
            foreach (var car in GameSession.Catalog.cars)
            {
                var profile = car.audioProfile != null ? car.audioProfile : GameAudio.Instance.defaultVehicle;
                Assume.That(System.Array.TrueForAll(profile.engineLayers, l => l.clip != null) && profile.engineLayers.Length > 0,
                    "engine recordings not installed (run Tools/Audio/prepare_sonniss_audio.sh)");
                Assert.That(profile.engineLayers.Length, Is.GreaterThanOrEqualTo(2), $"{car.displayName}: at least two engine recordings");
                float lowest = float.MaxValue, highest = 0f;
                foreach (var layer in profile.engineLayers)
                {
                    Assert.That(layer.clip, Is.Not.Null);
                    Assert.That(layer.clip.length, Is.GreaterThan(1f), "loops long enough not to buzz");
                    lowest = Mathf.Min(lowest, layer.rpm);
                    highest = Mathf.Max(highest, layer.rpm);
                }
                Assert.That(car.settings.idleRpm / lowest, Is.GreaterThanOrEqualTo(CarEngineAudio.MinPitch), $"{car.displayName}: idle within pitch range");
                Assert.That(car.settings.redlineRpm / highest, Is.LessThanOrEqualTo(CarEngineAudio.MaxPitch), $"{car.displayName}: redline within pitch range");
            }
        }
    }

    /// <summary>
    /// Records the game's final mix (what the listener hears) during a race with the player car on autopilot and
    /// reports peak level and clipped samples. Explicit (writes a WAV): <c>CAPTURE_DIR=/path ... -testFilter AudioOutputTests</c>
    /// </summary>
    [Explicit("Records the audio output; run on demand")]
    public class AudioOutputTests
    {
        sealed class Recorder : MonoBehaviour
        {
            public readonly System.Collections.Generic.List<float> samples = new System.Collections.Generic.List<float>();
            public int channels;
            void OnAudioFilterRead(float[] data, int count)
            {
                channels = count;
                lock (samples) samples.AddRange(data);
            }
        }

        [UnityTest]
        public IEnumerator RaceMix_HasNoClipping()
        {
            string directory = System.Environment.GetEnvironmentVariable("CAPTURE_DIR");
            Assume.That(!string.IsNullOrEmpty(directory), "set CAPTURE_DIR");
            Directory.CreateDirectory(directory);
            GameSession.UseStorage(new MemoryStorage());
            var race = ScriptableObject.CreateInstance<RaceDefinition>();
            race.id = "audio";
            race.laps = 3;
            race.opponents = 5;
            GameSession.PendingRace = race;
            SceneManager.LoadScene("Circuit_GrandPrix");
            yield return null;
            yield return null;
            var manager = Object.FindAnyObjectByType<RaceManager>();
            var listener = Object.FindAnyObjectByType<AudioListener>();
            var recorder = listener.gameObject.AddComponent<Recorder>();
            manager.SetPlayerAutopilot(true);
            float end = Time.realtimeSinceStartup + 30f; // countdown, start, first corners
            while (Time.realtimeSinceStartup < end)
                yield return null;

            float[] data;
            lock (recorder.samples) data = recorder.samples.ToArray();
            int rate = AudioSettings.outputSampleRate, channels = Mathf.Max(1, recorder.channels);
            Assume.That(data.Length, Is.GreaterThan(rate), $"no audio output was produced (driver: {AudioSettings.driverCapabilities})");
            float peak = 0f; int clipped = 0; double sum = 0;
            foreach (float v in data)
            {
                float a = Mathf.Abs(v);
                peak = Mathf.Max(peak, a);
                if (a >= 0.999f) clipped++;
                sum += v * v;
            }
            float rms = Mathf.Sqrt((float)(sum / data.Length));
            WriteWav(Path.Combine(directory, "race_mix.wav"), data, rate, channels);
            Debug.Log($"AUDIO MIX: {data.Length / channels / (float)rate:0.0} s, peak {20f * Mathf.Log10(Mathf.Max(peak, 1e-6f)):0.0} dBFS, rms {20f * Mathf.Log10(Mathf.Max(rms, 1e-6f)):0.0} dBFS, clipped samples {clipped}");
            Assert.That(clipped, Is.EqualTo(0), "the mix never reaches full scale");
            manager.SetPaused(false);
            Object.Destroy(race);
            yield return TestScenes.ResetToEmptyScene();
        }

        static void WriteWav(string path, float[] data, int rate, int channels)
        {
            using var writer = new BinaryWriter(File.Create(path));
            int bytes = data.Length * 2;
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + bytes);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16); writer.Write((short)1); writer.Write((short)channels);
            writer.Write(rate); writer.Write(rate * channels * 2); writer.Write((short)(channels * 2)); writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(bytes);
            foreach (float v in data)
                writer.Write((short)Mathf.Clamp(Mathf.RoundToInt(v * 32767f), -32768, 32767));
        }
    }
}
