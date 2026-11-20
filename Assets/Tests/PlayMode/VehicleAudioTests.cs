using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using RealisticCarRacing.Audio;
using RealisticCarRacing.Controls;
using RealisticCarRacing.Managers;
using RealisticCarRacing.Vehicles;
using UnityEngine;
using UnityEngine.TestTools;

namespace RealisticCarRacing.Tests
{
    /// <summary>
    /// The car audio logic, driven by the real vehicle physics: engine pitch and layer blend follow RPM and load,
    /// gear sounds fire once per actual gear change, tyre and brake layers follow wheel slip, AI cars fall silent with
    /// distance, collisions and the horn, volume settings reach the mixer, no AudioSources are created during play,
    /// and the physics are identical with sound attached.
    /// <para>
    /// The clips here are short test tones made in code, only so the sources have something to play; they never ship.
    /// What these tests check is behaviour (levels, pitch, timing), not how the game's recordings sound.
    /// </para>
    /// </summary>
    public class VehicleAudioTests
    {
        sealed class ScriptedInput : IVehicleInputSource
        {
            public VehicleInputState State;
            public VehicleInputState ReadInput() => State;
        }

        GameObject ground;
        VehicleSettings settings;
        VehicleAudioProfile profile;
        readonly List<Object> created = new List<Object>();

        static AudioClip Tone(string name, float seconds = 1f, float frequency = 110f)
        {
            int rate = 22050, length = Mathf.RoundToInt(rate * seconds);
            var data = new float[length];
            for (int i = 0; i < length; i++)
                data[i] = 0.25f * Mathf.Sin(2f * Mathf.PI * frequency * i / rate);
            var clip = AudioClip.Create(name, length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        [SetUp]
        public void SetUp()
        {
            GameSession.UseStorage(new MemoryStorage());
            ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.transform.localScale = new Vector3(300f, 1f, 300f);
            settings = ScriptableObject.CreateInstance<VehicleSettings>();
            profile = ScriptableObject.CreateInstance<VehicleAudioProfile>();
            profile.engineLayers = new[]
            {
                new VehicleAudioProfile.EngineLayer { clip = Tone("on 1000"), rpm = 1000f, load = 1f },
                new VehicleAudioProfile.EngineLayer { clip = Tone("on 3000"), rpm = 3000f, load = 1f },
                new VehicleAudioProfile.EngineLayer { clip = Tone("on 5000"), rpm = 5000f, load = 1f },
                new VehicleAudioProfile.EngineLayer { clip = Tone("on 7000"), rpm = 7000f, load = 1f },
                new VehicleAudioProfile.EngineLayer { clip = Tone("off 1500"), rpm = 1500f, load = 0f },
                new VehicleAudioProfile.EngineLayer { clip = Tone("off 5000"), rpm = 5000f, load = 0f }
            };
            profile.startup = Tone("startup", 0.6f);
            profile.shutdown = Tone("shutdown", 0.6f);
            profile.exhaustPops = new[] { Tone("pop", 0.1f) };
            profile.gearUp = Tone("gear up", 0.2f);
            profile.gearDown = Tone("gear down", 0.2f);
            profile.gearEngage = Tone("engage", 0.2f);
            profile.tyreSqueal = Tone("squeal");
            profile.brake = Tone("brake");
            profile.asphalt = Tone("road");
            profile.wind = Tone("wind");
            profile.horn = Tone("horn");
            profile.scrape = Tone("scrape");
            profile.lightImpacts = new[] { Tone("light", 0.2f) };
            profile.heavyImpacts = new[] { Tone("heavy", 0.2f) };
            created.Add(profile);
            created.Add(settings);
            created.Add(ground);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var vehicle in Object.FindObjectsByType<VehicleController>())
                Object.Destroy(vehicle.gameObject);
            foreach (var item in created)
                Object.Destroy(item);
            created.Clear();
        }

        VehicleController Spawn(Vector3 position, out ScriptedInput input, bool withAudio = true, bool isPlayer = true)
        {
            var car = TestVehicleFactory.Create(settings, position, Quaternion.identity, default, addPlayerInput: false);
            input = new ScriptedInput();
            car.SetInputSource(input);
            if (withAudio)
                VehicleAudioManager.Attach(car, isPlayer, profile).Engine.StartEngine();
            return car;
        }

        static IEnumerator Simulate(float seconds)
        {
            float end = Time.fixedTime + seconds;
            while (Time.fixedTime < end)
                yield return new WaitForFixedUpdate();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Engine_StartsUp_ThenIdles_WithPitchFollowingRpm()
        {
            var car = Spawn(new Vector3(0f, 0.5f, 0f), out _);
            var engine = car.GetComponent<CarEngineAudio>();
            Assert.That(engine.Running, Is.True);
            yield return null;
            Assert.That(engine.Loudness, Is.LessThan(0.05f), "silent at first: the startup recording plays");
            yield return Simulate(2f);
            Assert.That(engine.Loudness, Is.GreaterThan(0.4f), "then the idle loops fade in");
            // At idle the lowest recordings carry the sound, pitched to the engine's actual RPM.
            for (int i = 0; i < engine.LayerCount; i++)
                Assert.That(engine.LayerPitch(i), Is.EqualTo(ExpectedPitch(engine.Rpm, i)).Within(0.001f));
            // Idling makes no load, so the lowest coasting recording (1500 rpm, index 4) carries it.
            for (int i = 0; i < engine.LayerCount; i++)
                if (i != 4)
                    Assert.That(engine.LayerVolume(4), Is.GreaterThan(engine.LayerVolume(i)), "idle: the low coasting loop leads");

            engine.StopEngine();
            yield return Simulate(1f);
            Assert.That(engine.Loudness, Is.EqualTo(0f).Within(0.01f), "shutdown fades the engine out");
        }

        [UnityTest]
        public IEnumerator Engine_PitchRisesSmoothlyWithRpm_AndThrottleSoundsHarderThanCoasting()
        {
            var car = Spawn(new Vector3(0f, 0.5f, 0f), out var input);
            var engine = car.GetComponent<CarEngineAudio>();
            yield return Simulate(2f);

            input.State = new VehicleInputState { Throttle = 1f };
            float startRpm = engine.Rpm, maxVolumeRate = 0f, maxLoad = 0f, maxRpm = 0f;
            var previousVolume = new float[engine.LayerCount];
            for (int i = 0; i < engine.LayerCount; i++)
                previousVolume[i] = engine.LayerVolume(i);
            float end = Time.time + 4f;
            while (Time.time < end)
            {
                yield return null;
                maxLoad = Mathf.Max(maxLoad, engine.Load);
                maxRpm = Mathf.Max(maxRpm, engine.Rpm);
                for (int i = 0; i < engine.LayerCount; i++)
                {
                    // Pitch is the engine's own (continuous) RPM over the recording's: no steps of its own.
                    Assert.That(engine.LayerPitch(i), Is.EqualTo(ExpectedPitch(engine.Rpm, i)).Within(0.001f));
                    maxVolumeRate = Mathf.Max(maxVolumeRate, Mathf.Abs(engine.LayerVolume(i) - previousVolume[i]) / Mathf.Max(Time.deltaTime, 0.008f));
                    previousVolume[i] = engine.LayerVolume(i);
                }
            }
            Assert.That(maxRpm, Is.GreaterThan(startRpm + 1500f), "revs rose");
            // A click is a jump between samples; a fade over 50 ms or more is heard as a fade. Gear changes (the torque
            // cut drops the load) are the fastest legitimate change.
            Assert.That(maxVolumeRate, Is.LessThan(20f), "levels never jump: full scale takes at least 50 ms");
            Assert.That(maxLoad, Is.GreaterThan(0.8f), "full throttle reads as high load");

            // Same revs, two states: on throttle, then the instant after lifting off.
            float onThrottleOnLoadLevel = OnLoadLevel(engine);
            input.State = default;
            yield return Simulate(0.4f);
            Assert.That(engine.Load, Is.LessThan(0.2f), "lifting off reads as low load");
            Assert.That(OnLoadLevel(engine), Is.LessThan(onThrottleOnLoadLevel * 0.5f), "the on-throttle recordings give way to coasting ones");
        }

        float ExpectedPitch(float rpm, int layer) =>
            Mathf.Clamp(Mathf.Max(rpm, settings.idleRpm * 0.9f) / profile.engineLayers[layer].rpm, 0.35f, 2.5f);

        float OnLoadLevel(CarEngineAudio engine)
        {
            float level = 0f;
            for (int i = 0; i < engine.LayerCount; i++)
                if (profile.engineLayers[i].load >= 0.5f)
                    level += engine.LayerVolume(i);
            return level;
        }

        [UnityTest]
        public IEnumerator Engine_CrossFadeKeepsConstantPower_AcrossRecordings()
        {
            var car = Spawn(new Vector3(0f, 0.5f, 0f), out var input);
            var engine = car.GetComponent<CarEngineAudio>();
            yield return Simulate(2f);
            input.State = new VehicleInputState { Throttle = 1f };
            float end = Time.time + 3f;
            while (Time.time < end)
            {
                yield return null;
                float power = 0f;
                for (int i = 0; i < engine.LayerCount; i++)
                    power += engine.LayerVolume(i) * engine.LayerVolume(i);
                // Equal-power blends: total power stays within the master level (no doubling where loops overlap).
                Assert.That(Mathf.Sqrt(power), Is.LessThanOrEqualTo(profile.engineVolume + 0.001f), "no build-up past full level");
            }
        }

        [UnityTest]
        public IEnumerator GearSounds_FireOncePerActualGearChange_IncludingReverse()
        {
            var car = Spawn(new Vector3(0f, 0.5f, 0f), out var input);
            var gearbox = car.GetComponent<GearShiftAudio>();
            yield return Simulate(1.5f);
            Assert.That(gearbox.ShiftsPlayed, Is.EqualTo(0), "nothing while idling in gear");

            input.State = new VehicleInputState { Throttle = 1f };
            int changes = 0, lastGear = car.Gear;
            float end = Time.time + 8f;
            while (Time.time < end)
            {
                yield return null;
                if (car.Gear != lastGear) { changes++; lastGear = car.Gear; }
            }
            Assert.That(changes, Is.GreaterThanOrEqualTo(3), "the automatic shifted up through the gears");
            Assert.That(gearbox.ShiftsPlayed, Is.EqualTo(changes), "exactly one sound per gear change");

            // Stop, then hold the brake: reverse is engaged once.
            input.State = new VehicleInputState { Brake = 1f };
            while (car.Gear != VehicleTransmission.Reverse && Time.time < end + 20f)
            {
                yield return null;
                if (car.Gear != lastGear) { changes++; lastGear = car.Gear; }
            }
            yield return null;
            Assert.That(car.Gear, Is.EqualTo(VehicleTransmission.Reverse));
            Assert.That(gearbox.LastSound, Is.EqualTo(ShiftSound.Engage));
            Assert.That(gearbox.ShiftsPlayed, Is.EqualTo(changes));
            int beforeHold = gearbox.ShiftsPlayed;
            yield return Simulate(1f);
            Assert.That(gearbox.ShiftsPlayed, Is.EqualTo(beforeHold), "no repeats while staying in reverse");
        }

        [Test]
        public void ShiftSounds_AreClassifiedByDirection()
        {
            Assert.That(GearShiftAudio.Classify(1, 2), Is.EqualTo(ShiftSound.Up));
            Assert.That(GearShiftAudio.Classify(4, 3), Is.EqualTo(ShiftSound.Down));
            Assert.That(GearShiftAudio.Classify(VehicleTransmission.Neutral, 1), Is.EqualTo(ShiftSound.Engage));
            Assert.That(GearShiftAudio.Classify(1, VehicleTransmission.Reverse), Is.EqualTo(ShiftSound.Engage));
            Assert.That(GearShiftAudio.Classify(VehicleTransmission.Reverse, 1), Is.EqualTo(ShiftSound.Engage));
            Assert.That(GearShiftAudio.Classify(2, 2), Is.EqualTo(ShiftSound.None));
        }

        [UnityTest]
        public IEnumerator Tyres_QuietWhenCruising_BrakeAndLockupUnderHardBraking_DriftInASlide()
        {
            var car = Spawn(new Vector3(0f, 0.5f, -600f), out var input);
            var tires = car.GetComponent<TireAudio>();
            yield return Simulate(1.5f);
            input.State = new VehicleInputState { Throttle = 0.7f };
            while (car.SpeedKmh < 90f)
                yield return null;
            input.State = new VehicleInputState { Throttle = 0.35f };
            yield return Simulate(1.5f);
            Assert.That(tires.Squeal, Is.LessThan(0.05f), "no squeal driving straight");
            Assert.That(tires.Drift, Is.EqualTo(0f));
            Assert.That(tires.Brake, Is.EqualTo(0f));
            Assert.That(tires.SurfaceShare(Tracks.TrackSurface.Asphalt), Is.EqualTo(1f), "no surface map: everything is asphalt");

            input.State = new VehicleInputState { Brake = 1f };
            float maxBrake = 0f, maxLock = 0f;
            float end = Time.time + 1.5f;
            while (Time.time < end && car.SpeedKmh > 5f)
            {
                yield return null;
                maxBrake = Mathf.Max(maxBrake, tires.Brake);
                maxLock = Mathf.Max(maxLock, tires.Lockup);
            }
            Assert.That(maxBrake + maxLock, Is.GreaterThan(0.3f), "hard braking is heard (pads, or locked tyres)");
            yield return Simulate(2f);
            Assert.That(tires.Brake, Is.LessThan(0.05f), "silent once stopped");

            // Throw the car sideways at speed: handbrake and full steering.
            input.State = new VehicleInputState { Throttle = 0.8f };
            while (car.SpeedKmh < 80f)
                yield return null;
            input.State = new VehicleInputState { Throttle = 0.6f, Steer = 1f, Handbrake = true };
            float maxDrift = 0f, maxSqueal = 0f;
            end = Time.time + 2f;
            while (Time.time < end)
            {
                yield return null;
                maxDrift = Mathf.Max(maxDrift, tires.Drift);
                maxSqueal = Mathf.Max(maxSqueal, tires.Squeal);
            }
            Assert.That(maxDrift, Is.GreaterThan(0.2f), "a sustained rear slide is a drift");
            Assert.That(maxSqueal + maxDrift, Is.GreaterThan(0.3f));
        }

        [UnityTest]
        public IEnumerator AiCars_AreSpatial_AndGoQuietFarFromTheListener()
        {
            var listener = new GameObject("Listener", typeof(AudioListener));
            created.Add(listener);
            var near = Spawn(new Vector3(0f, 0.5f, 20f), out _, isPlayer: false);
            var far = Spawn(new Vector3(0f, 0.5f, 400f), out _, isPlayer: false);
            yield return Simulate(0.5f);
            var nearAudio = near.GetComponent<VehicleAudioManager>();
            var farAudio = far.GetComponent<VehicleAudioManager>();
            Assert.That(nearAudio.Audible, Is.True);
            Assert.That(farAudio.Audible, Is.False, "beyond the cull distance an AI car is paused");
            foreach (var source in near.GetComponentsInChildren<AudioSource>())
            {
                Assert.That(source.spatialBlend, Is.EqualTo(1f), "3D: level and stereo position depend on the listener");
                Assert.That(source.rolloffMode, Is.EqualTo(AudioRolloffMode.Logarithmic));
                Assert.That(source.dopplerLevel, Is.GreaterThan(0f), "rivals passing by bend in pitch");
            }
            Assert.That(near.GetComponent<EnvironmentAudio>(), Is.Null, "AI cars skip wind and horn");
            listener.transform.position = far.transform.position;
            yield return null;
            yield return null;
            Assert.That(farAudio.Audible, Is.True, "comes back when the listener approaches");
        }

        [UnityTest]
        public IEnumerator Collisions_ScaleWithImpact_AndTheHornFollowsTheButton()
        {
            var car = Spawn(new Vector3(0f, 0.5f, 0f), out var input);
            var environment = car.GetComponent<EnvironmentAudio>();
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0f, 1f, 60f);
            wall.transform.localScale = new Vector3(20f, 2f, 1f);
            created.Add(wall);
            yield return Simulate(1f);

            input.State = new VehicleInputState { Horn = true };
            yield return Simulate(0.3f);
            Assert.That(environment.Horn, Is.GreaterThan(0.9f), "horn sounds while held");
            input.State = default;
            yield return Simulate(0.3f);
            Assert.That(environment.Horn, Is.LessThan(0.05f), "and stops on release");
            Assert.That(environment.ImpactsPlayed, Is.EqualTo(0));

            input.State = new VehicleInputState { Throttle = 1f };
            float end = Time.time + 10f;
            while (environment.ImpactsPlayed == 0 && Time.time < end)
                yield return null;
            Assert.That(environment.ImpactsPlayed, Is.GreaterThanOrEqualTo(1), "hitting the wall makes a sound");
            Assert.That(environment.LastImpactVolume, Is.GreaterThan(0.3f), "a hard hit is loud");
        }

        [UnityTest]
        public IEnumerator NoAudioSourcesAreCreatedDuringPlay_AndPhysicsAreUnchanged()
        {
            var withSound = Spawn(new Vector3(0f, 0.5f, 0f), out var soundInput);
            var silent = Spawn(new Vector3(20f, 0.5f, 0f), out var silentInput, withAudio: false);
            yield return Simulate(1.5f);
            int sources = Object.FindObjectsByType<AudioSource>().Length;
            var manager = withSound.GetComponent<VehicleAudioManager>();
            Assert.That(manager.SourceCount, Is.LessThanOrEqualTo(24), "a bounded set per car, made once at spawn");
            Assert.That(manager.PlayingLoops, Is.LessThanOrEqualTo(4), "idling: only the loops that are heard use a voice");

            var drive = new VehicleInputState { Throttle = 1f, Steer = 0.2f };
            soundInput.State = drive;
            silentInput.State = drive;
            yield return Simulate(4f);
            Assert.That(withSound.SpeedKmh, Is.EqualTo(silent.SpeedKmh).Within(0.5f), "sound doesn't touch the physics");
            Assert.That(withSound.transform.position.x - silent.transform.position.x, Is.EqualTo(-20f).Within(0.3f));
            Assert.That(withSound.transform.position.z - silent.transform.position.z, Is.EqualTo(0f).Within(0.3f));

            // Every sound in use at once (slide, brakes, gear changes): still no new sources.
            soundInput.State = silentInput.State = new VehicleInputState { Brake = 1f, Steer = -0.5f, Handbrake = true, Horn = true };
            yield return Simulate(2f);
            Assert.That(Object.FindObjectsByType<AudioSource>().Length, Is.EqualTo(sources), "no sources created while driving");
        }

        [UnityTest]
        public IEnumerator Circuit_SurfaceMap_FindsAsphaltKerbsGravelAndGrass()
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("Circuit_GrandPrix");
            yield return null;
            yield return null;
            var surfaces = Object.FindAnyObjectByType<Tracks.TrackSurfaces>();
            Assert.That(surfaces, Is.Not.Null, "the circuit has a surface map");
            var track = surfaces.Track;
            int hint = -1, kerbs = 0, gravel = 0;
            float roadHalf = track.RoadWidth * 0.5f;
            for (int i = 0; i < track.SampleCount; i += 4)
            {
                var sample = track.GetSample(i);
                Assert.That(surfaces.SurfaceAt(sample.position, ref hint), Is.EqualTo(Tracks.TrackSurface.Asphalt), "the road is asphalt");
                foreach (float side in new[] { -1f, 1f })
                {
                    var edge = surfaces.SurfaceAt(sample.position + sample.right * side * (roadHalf + 0.6f), ref hint);
                    Assert.That(edge == Tracks.TrackSurface.Kerb || edge == Tracks.TrackSurface.Grass, Is.True, $"just off the road: kerb or grass, not {edge}");
                    if (edge == Tracks.TrackSurface.Kerb) kerbs++;
                    if (surfaces.SurfaceAt(sample.position + sample.right * side * (roadHalf + 5f), ref hint) == Tracks.TrackSurface.Gravel) gravel++;
                }
            }
            Assert.That(kerbs, Is.GreaterThan(10), "kerbs are found in the corners");
            Assert.That(gravel, Is.GreaterThan(5), "gravel traps are found outside the tightest corners");
            yield return TestScenes.ResetToEmptyScene();
        }

        [Test]
        public void VolumeSettings_ReachTheMixer_AndAreSaved()
        {
            var audio = GameAudio.Instance;
            Assert.That(audio, Is.Not.Null, "Resources/Audio/GameAudio exists");
            Assert.That(audio.mixer, Is.Not.Null);
            foreach (AudioCategory category in System.Enum.GetValues(typeof(AudioCategory)))
                Assert.That(audio.Group(category), Is.Not.Null, $"{category} mixer group");

            AudioSettingsService.SetVolume(AudioCategory.Engine, 0.5f);
            Assert.That(audio.mixer.GetFloat(AudioSettingsService.Parameter(AudioCategory.Engine), out float engineDb), Is.True);
            Assert.That(engineDb, Is.EqualTo(20f * Mathf.Log10(0.5f)).Within(0.01f), "half volume is -6 dB");
            AudioSettingsService.SetVolume(AudioCategory.Music, 0f);
            audio.mixer.GetFloat(AudioSettingsService.Parameter(AudioCategory.Music), out float musicDb);
            Assert.That(musicDb, Is.EqualTo(-80f), "zero is silent");
            AudioSettingsService.SetMuted(true);
            audio.mixer.GetFloat(AudioSettingsService.MasterParameter, out float masterDb);
            Assert.That(masterDb, Is.EqualTo(-80f), "mute silences the master");

            var reloaded = ProfileStore.Load(GameSession.Storage);
            Assert.That(reloaded.audio.engine, Is.EqualTo(0.5f), "saved with the profile");
            Assert.That(reloaded.audio.muted, Is.True);
            AudioSettingsService.SetMuted(false);
            AudioSettingsService.SetVolume(AudioCategory.Engine, 1f);
            AudioSettingsService.SetVolume(AudioCategory.Music, 0.6f);
        }
    }
}
