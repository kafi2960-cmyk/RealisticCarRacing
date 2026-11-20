using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using RealisticCarRacing.AI;
using RealisticCarRacing.Controls;
using RealisticCarRacing.Managers;
using RealisticCarRacing.Racing;
using RealisticCarRacing.Tracks;
using RealisticCarRacing.Vehicles;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RealisticCarRacing.Tests
{
    /// <summary>Loads the generated Grand Prix circuit and drives the test car around all of it.</summary>
    public class CircuitTests
    {
        const string SceneName = "Circuit_GrandPrix";
        const float SimulationSpeed = 5f;

        RaceTrack track;
        VehicleController car;

        RaceDefinition soloRace;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // A solo race long enough that the race never finishes (and takes over the car) during a test.
            GameSession.UseStorage(new MemoryStorage());
            soloRace = ScriptableObject.CreateInstance<RaceDefinition>();
            soloRace.id = "test_solo";
            soloRace.laps = 10;
            soloRace.opponents = 0;
            GameSession.PendingRace = soloRace;

            SceneManager.LoadScene(SceneName);
            yield return null;
            var race = Object.FindAnyObjectByType<RaceManager>();
            Assert.That(race, Is.Not.Null, "circuit scene has no RaceManager");
            race.SkipCountdown();
            yield return null;
            yield return new WaitForFixedUpdate();
            Assert.That(race.State, Is.EqualTo(RaceState.Racing));
            track = race.Track;
            car = race.Player.Vehicle;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // Unload the circuit so later tests start from an empty world.
            yield return TestScenes.ResetToEmptyScene();
            Object.Destroy(soloRace);
            GameSession.PendingRace = null;
        }

        [UnityTest]
        public IEnumerator Road_HasContinuousCollisionAndBarriersAllTheWayRound()
        {
            var failures = new List<string>();
            float half = track.RoadWidth * 0.5f;
            float barrierOffset = track.Layout.BarrierOffset;
            for (int i = 0; i < track.SampleCount; i++)
            {
                var sample = track.GetSample(i);
                foreach (float lateral in new[] { -half + 0.3f, -half * 0.5f, 0f, half * 0.5f, half - 0.3f })
                {
                    Vector3 origin = sample.position + sample.right * lateral + Vector3.up * 5f;
                    if (!HitsCollider(origin, Vector3.down, 10f, "Road", out RaycastHit hit) ||
                        Mathf.Abs(hit.point.y - track.RoadSurfaceHeight) > 0.01f)
                        failures.Add($"no road surface at {sample.distance:0} m, lateral {lateral:0.0} m");
                }

                foreach (float side in new[] { -1f, 1f })
                {
                    Vector3 origin = sample.position + Vector3.up * 0.5f;
                    if (!HitsCollider(origin, sample.right * side, barrierOffset + 1f, "Barriers", out RaycastHit hit) ||
                        hit.distance < barrierOffset - 0.5f)
                        failures.Add($"no barrier {(side > 0 ? "right" : "left")} at {sample.distance:0} m");
                }
            }
            Assert.That(failures, Is.Empty, string.Join("\n", failures.GetRange(0, Mathf.Min(10, failures.Count))));
            yield break;
        }

        [UnityTest]
        public IEnumerator Autopilot_CompletesALap_OnEveryLine([Values(0f, 4.5f, -4.5f)] float lineOffset)
        {
            var driver = new TrackFollowerDriver(car, track) { LateralOffset = lineOffset };
            car.SetInputSource(driver);
            Time.timeScale = SimulationSpeed;

            int startSample = track.FindClosestSample(car.transform.position);
            int previous = startSample;
            int progress = 0;
            float elapsed = 0f, slowFor = 0f, worstOffTrack = 0f, topSpeed = 0f;
            string worstPlace = "";
            const float timeLimit = 240f;

            while (progress < track.SampleCount && elapsed < timeLimit)
            {
                yield return new WaitForFixedUpdate();
                elapsed += Time.fixedDeltaTime;

                int current = driver.ClosestSample >= 0 ? driver.ClosestSample : previous;
                int delta = current - previous;
                if (delta > track.SampleCount / 2) delta -= track.SampleCount;
                if (delta < -track.SampleCount / 2) delta += track.SampleCount;
                progress += delta;
                previous = current;

                float speed = car.Body.linearVelocity.magnitude;
                topSpeed = Mathf.Max(topSpeed, speed);
                slowFor = elapsed > 5f && speed < 1f ? slowFor + Time.fixedDeltaTime : 0f;
                Assert.That(slowFor, Is.LessThan(4f), $"car stuck at {track.GetSample(current).distance:0} m");
                Assert.That(car.transform.up.y, Is.GreaterThan(0.3f), $"car rolled over at {track.GetSample(current).distance:0} m");

                float offTrack = Mathf.Abs(track.LateralOffset(car.transform.position, current)) - track.RoadWidth * 0.5f;
                if (offTrack > worstOffTrack)
                {
                    worstOffTrack = offTrack;
                    worstPlace = $"{track.GetSample(current).distance:0} m";
                }
            }

            Debug.Log($"Line {lineOffset:+0.0;-0.0;0.0} m: lap {elapsed:0.0} s, top speed {topSpeed * 3.6f:0} km/h, " +
                      $"widest {Mathf.Max(0f, worstOffTrack):0.00} m beyond the road edge at {worstPlace}");
            Assert.That(progress, Is.GreaterThanOrEqualTo(track.SampleCount), $"lap not finished in {timeLimit} s (reached {progress * track.Length / track.SampleCount:0} m)");
            // The car's centre may run onto the kerbs (1.2 m) but never onto the grass.
            Assert.That(worstOffTrack, Is.LessThan(track.Layout.kerbWidth), $"left the track by {worstOffTrack:0.00} m at {worstPlace}");
        }

        [UnityTest]
        public IEnumerator BarrierContact_DeflectsTheCarWithoutTrappingIt()
        {
            Time.timeScale = SimulationSpeed;
            // 120 m down the main straight, clear of the grid.
            int startIndex = Mathf.RoundToInt(120f / (track.Length / track.SampleCount));
            var sample = track.GetSample(startIndex);

            // Aim 25° towards the right-hand barrier at 25 m/s.
            var rotation = Quaternion.LookRotation(Quaternion.AngleAxis(25f, Vector3.up) * sample.forward, Vector3.up);
            car.ResetVehicle();
            car.Body.position = sample.position + Vector3.up * 0.4f;
            car.Body.rotation = rotation;
            car.transform.SetPositionAndRotation(car.Body.position, rotation);
            car.Body.linearVelocity = rotation * Vector3.forward * 25f;
            var scripted = new HoldInput { State = new VehicleInputState { Throttle = 0.5f } };
            car.SetInputSource(scripted);
            yield return Simulate(3f);

            Assert.That(car.transform.up.y, Is.GreaterThan(0.5f), "car rolled over on impact");
            int afterImpact = track.FindClosestSample(car.transform.position);
            Assert.That(Mathf.Abs(track.LateralOffset(car.transform.position, afterImpact)), Is.LessThan(track.Layout.BarrierOffset),
                "car went through the barrier");

            // Autopilot must be able to recover and carry on round the lap.
            var driver = new TrackFollowerDriver(car, track);
            car.SetInputSource(driver);
            float topSpeedAfter = 0f, end = Time.fixedTime + 15f;
            while (Time.fixedTime < end)
            {
                yield return new WaitForFixedUpdate();
                topSpeedAfter = Mathf.Max(topSpeedAfter, car.Body.linearVelocity.magnitude);
            }
            int recovered = track.FindClosestSample(car.transform.position);
            float metres = (recovered - afterImpact + track.SampleCount) % track.SampleCount * track.Length / track.SampleCount;
            Debug.Log($"Barrier recovery: {metres:0} m in 15 s, back up to {topSpeedAfter * 3.6f:0} km/h");
            // "Not trapped" = it gets back to racing speed and makes real progress (the exact distance depends on how the crash plays out).
            Assert.That(topSpeedAfter * 3.6f, Is.GreaterThan(60f), "car never got back up to speed after the impact");
            Assert.That(metres, Is.GreaterThan(100f), "car could not get going again after the impact");
        }

        sealed class HoldInput : IVehicleInputSource
        {
            public VehicleInputState State;
            public VehicleInputState ReadInput() => State;
        }

        static IEnumerator Simulate(float seconds)
        {
            float end = Time.fixedTime + seconds;
            while (Time.fixedTime < end)
                yield return new WaitForFixedUpdate();
        }

        static bool HitsCollider(Vector3 origin, Vector3 direction, float distance, string objectName, out RaycastHit hit)
        {
            foreach (var candidate in Physics.RaycastAll(origin, direction, distance))
            {
                if (candidate.collider.name == objectName)
                {
                    hit = candidate;
                    return true;
                }
            }
            hit = default;
            return false;
        }
    }
}
