using System.Collections.Generic;
using NUnit.Framework;
using RealisticCarRacing.Managers;
using RealisticCarRacing.Racing;
using RealisticCarRacing.Tracks;
using RealisticCarRacing.Vehicles;
using UnityEngine;

namespace RealisticCarRacing.Tests
{
    /// <summary>Checkpoints, laps, standings, gaps and the racing line, on the real circuit layout without physics.</summary>
    public class RaceLogicTests
    {
        const int Checkpoints = 16;

        TrackLayout layout;
        GameObject trackObject;
        RaceTrack track;
        float spacing;

        [SetUp]
        public void SetUp()
        {
            layout = ScriptableObject.CreateInstance<TrackLayout>();
            trackObject = new GameObject("Track");
            track = trackObject.AddComponent<RaceTrack>();
            track.Bake(layout, layout.Sample());
            spacing = track.Length / track.SampleCount;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(trackObject);
            Object.DestroyImmediate(layout);
        }

        Vector3 PointAt(float distance)
        {
            float d = Mathf.Repeat(distance, track.Length);
            int index = Mathf.FloorToInt(d / spacing);
            var a = track.GetSample(index);
            var b = track.GetSample(index + 1);
            return Vector3.Lerp(a.position, b.position, (d - a.distance) / spacing);
        }

        /// <summary>Moves a progress tracker along the centreline in 1 m steps.</summary>
        void Drive(RaceProgress progress, float from, float to)
        {
            float direction = Mathf.Sign(to - from);
            for (float d = from; direction > 0f ? d <= to : d >= to; d += direction)
            {
                var sample = track.GetSample(Mathf.FloorToInt(Mathf.Repeat(d, track.Length) / spacing));
                progress.Update(PointAt(d), sample.forward * (20f * direction), 0.05f);
            }
        }

        RaceProgress NewProgress(float startDistance) => new RaceProgress(track, Checkpoints, PointAt(startDistance));

        [Test]
        public void Progress_StartsBehindTheLineOnTheGrid()
        {
            var progress = new RaceProgress(track, Checkpoints, track.GetGridPose(0).position);
            Assert.That(progress.Distance, Is.EqualTo(-10f).Within(1.5f));
            Assert.That(progress.HighestCheckpoint, Is.EqualTo(-1));
            Assert.That(progress.CompletedLaps, Is.EqualTo(0));
        }

        [Test]
        public void Progress_PassesEveryCheckpointInOrderAndCountsLaps()
        {
            var progress = NewProgress(-10f);
            var checkpoints = new List<int>();
            var laps = new List<int>();
            progress.CheckpointReached += checkpoints.Add;
            progress.LapCompleted += laps.Add;

            Drive(progress, -10f, 2f * track.Length + 5f);

            Assert.That(laps, Is.EqualTo(new[] { 1, 2 }));
            Assert.That(progress.CompletedLaps, Is.EqualTo(2));
            Assert.That(checkpoints.Count, Is.EqualTo(2 * Checkpoints + 1), "start line + every checkpoint of two laps");
            for (int i = 0; i < checkpoints.Count; i++)
                Assert.That(checkpoints[i], Is.EqualTo(i), "checkpoints must arrive in order");
        }

        [Test]
        public void Progress_BackingOverTheLineNeverCountsALapTwice()
        {
            var progress = NewProgress(-10f);
            var laps = new List<int>();
            progress.LapCompleted += laps.Add;

            Drive(progress, -10f, track.Length + 20f);
            Drive(progress, track.Length + 20f, track.Length - 40f); // reverse back over the line
            Assert.That(progress.Distance, Is.LessThan(track.Length));
            Drive(progress, track.Length - 40f, track.Length + 30f);

            Assert.That(laps, Is.EqualTo(new[] { 1 }));
        }

        [Test]
        public void Progress_LapNeedsTheWholeLapDriven()
        {
            // Teleporting from just after the start to just before it must not complete a lap.
            var progress = NewProgress(5f);
            int laps = 0;
            progress.LapCompleted += _ => laps++;
            Drive(progress, 5f, 60f);
            progress.ResetTracking(PointAt(track.Length - 30f));
            Drive(progress, track.Length - 30f, track.Length + 10f);
            Assert.That(laps, Is.EqualTo(0));
        }

        [Test]
        public void Progress_DetectsWrongWay()
        {
            var progress = NewProgress(200f);
            Drive(progress, 200f, 140f);
            Assert.That(progress.WrongWaySeconds, Is.GreaterThan(2f));
            Drive(progress, 140f, 150f);
            Assert.That(progress.WrongWaySeconds, Is.EqualTo(0f));
        }

        Racer RacerAt(string name, float distance, float raceTimePerMetre = 0.04f)
        {
            var progress = NewProgress(-10f);
            var racer = new Racer(name, false, null, null, progress);
            progress.CheckpointReached += checkpoint => racer.RecordCheckpoint(checkpoint, (checkpoint * track.Length / Checkpoints + 10f) * raceTimePerMetre);
            Drive(progress, -10f, distance);
            return racer;
        }

        [Test]
        public void Standings_OrderFinishersByTimeThenRunnersByDistance()
        {
            var leader = RacerAt("Leader", 1500f);
            var second = RacerAt("Second", 900f);
            var finishedLate = RacerAt("FinishedLate", 300f);
            var finishedEarly = RacerAt("FinishedEarly", 200f);
            finishedLate.Finish(200f);
            finishedEarly.Finish(150f);

            var order = new List<Racer> { second, finishedLate, leader, finishedEarly };
            RaceStandings.Sort(order);

            Assert.That(order, Is.EqualTo(new[] { finishedEarly, finishedLate, leader, second }));
            Assert.That(order[2].Position, Is.EqualTo(3));
        }

        [Test]
        public void Standings_GapsUseCheckpointTimesAndShowLapsBehind()
        {
            var leader = RacerAt("Leader", track.Length + 700f, 0.04f);
            var close = RacerAt("Close", track.Length + 650f, 0.045f);
            var lapped = RacerAt("Lapped", 600f, 0.05f);

            string gap = RaceStandings.Gap(close, leader);
            Assert.That(gap, Does.StartWith("+"));
            Assert.That(float.Parse(gap.Substring(1), System.Globalization.CultureInfo.InvariantCulture), Is.GreaterThan(0f));
            Assert.That(RaceStandings.Gap(lapped, leader), Is.EqualTo("+1 LAP"));
            Assert.That(RaceStandings.Gap(leader, leader), Is.EqualTo("LEADER"));
        }

        [Test]
        public void TimeFormat_IsMinutesSecondsMilliseconds()
        {
            Assert.That(RaceTimeFormat.Format(95.4321f), Is.EqualTo("1:35.432"));
            Assert.That(RaceTimeFormat.Format(0f), Is.EqualTo("-:--.---"));
            Assert.That(RaceTimeFormat.FormatGap(1.5f), Is.EqualTo("1.500"));
        }

        [Test]
        public void RacingLine_GoesWideBeforeACornerAndTightAtTheApex()
        {
            var line = new RacingLine(track, 3.2f);
            int apex = 0;
            for (int i = 0; i < track.SampleCount; i++)
            {
                Assert.That(Mathf.Abs(line.OffsetAt(i)), Is.LessThanOrEqualTo(3.2f + 1e-4f));
                if (Mathf.Abs(track.GetSample(i).curvature) > Mathf.Abs(track.GetSample(apex).curvature))
                    apex = i;
            }
            float turn = Mathf.Sign(track.GetSample(apex).curvature); // +1 left
            // Positive offset is to the right, so "inside" of a left turn is negative.
            Assert.That(line.OffsetAt(apex) * turn, Is.LessThan(-0.5f), "inside at the apex");
            int approach = apex - Mathf.RoundToInt(60f / spacing);
            Assert.That(line.OffsetAt(approach) * turn, Is.GreaterThan(0f), "outside on the approach");
        }
    }

    public class GameDataTests
    {
        [Test]
        public void Profile_RoundTripsThroughStorage()
        {
            var storage = new MemoryStorage();
            var profile = new PlayerProfile { selectedCarId = "apex_rs", selectedRaceId = "sprint" };
            profile.SetupFor("apex_rs").paint = 3;
            profile.SetupFor("apex_rs").tuning = TuningPreset.Grip;
            profile.SubmitResult("sprint", 2, 190f, 93.2f);
            ProfileStore.Save(storage, profile);

            var loaded = ProfileStore.Load(storage);
            Assert.That(loaded.selectedCarId, Is.EqualTo("apex_rs"));
            Assert.That(loaded.SetupFor("apex_rs").paint, Is.EqualTo(3));
            Assert.That(loaded.SetupFor("apex_rs").tuning, Is.EqualTo(TuningPreset.Grip));
            Assert.That(loaded.RecordFor("sprint").bestLap, Is.EqualTo(93.2f));
        }

        [Test]
        public void Profile_KeepsOnlyImprovedRecords()
        {
            var profile = new PlayerProfile();
            var first = profile.SubmitResult("gp", 3, 300f, 95f);
            Assert.That(first.newBestLap && first.newBestRaceTime && first.newBestPosition, Is.True);
            var slower = profile.SubmitResult("gp", 4, 310f, 96f);
            Assert.That(slower.newBestLap || slower.newBestRaceTime || slower.newBestPosition, Is.False);
            var win = profile.SubmitResult("gp", 1, 305f, 94f);
            Assert.That(win.newBestLap && win.newBestPosition && !win.newBestRaceTime, Is.True);

            var record = profile.RecordFor("gp");
            Assert.That(record.starts, Is.EqualTo(3));
            Assert.That(record.wins, Is.EqualTo(1));
            Assert.That(record.bestLap, Is.EqualTo(94f));
            Assert.That(record.bestRaceTime, Is.EqualTo(300f));
        }

        [Test]
        public void Profile_SurvivesCorruptSaveData()
        {
            var storage = new MemoryStorage();
            storage.Write(ProfileStore.Key, "{ not json");
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            var profile = ProfileStore.Load(storage);
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false;
            Assert.That(profile, Is.Not.Null);
            Assert.That(profile.records, Is.Empty);
        }

        [Test]
        public void Tuning_ChangesTheCopyNotTheAsset()
        {
            var source = ScriptableObject.CreateInstance<VehicleSettings>();
            float downforce = source.downforceCoefficient;
            var grip = VehicleTuning.CreateTuned(source, TuningPreset.Grip);
            var speed = VehicleTuning.CreateTuned(source, TuningPreset.TopSpeed);

            Assert.That(source.downforceCoefficient, Is.EqualTo(downforce));
            var balancedStats = CarStats.From(source);
            Assert.That(CarStats.From(grip).grip, Is.GreaterThan(balancedStats.grip));
            Assert.That(CarStats.From(speed).topSpeedKmh, Is.GreaterThan(balancedStats.topSpeedKmh));
            Assert.That(CarStats.From(speed).grip, Is.LessThan(balancedStats.grip));

            Object.DestroyImmediate(source);
            Object.DestroyImmediate(grip);
            Object.DestroyImmediate(speed);
        }

        [Test]
        public void Catalog_HasDistinctCarsAndPlayableRaces()
        {
            var catalog = Resources.Load<GameCatalog>(GameCatalog.ResourcePath);
            Assert.That(catalog, Is.Not.Null, "Resources/GameCatalog is missing — run Create Game Content");
            Assert.That(catalog.cars.Length, Is.GreaterThanOrEqualTo(3));
            Assert.That(catalog.races.Length, Is.GreaterThanOrEqualTo(3));

            var ids = new HashSet<string>();
            foreach (var car in catalog.cars)
            {
                Assert.That(ids.Add(car.id), Is.True, $"duplicate car id {car.id}");
                Assert.That(car.settings, Is.Not.Null);
                var stats = CarStats.From(car.settings);
                Assert.That(stats.powerKw, Is.InRange(60f, 400f), car.displayName);
                Assert.That(stats.topSpeedKmh, Is.InRange(150f, 350f), car.displayName);
            }
            foreach (var race in catalog.races)
            {
                Assert.That(race.opponents, Is.InRange(0, 11));
                Assert.That(UnityEditor.EditorBuildSettings.scenes, Has.Some.Matches<UnityEditor.EditorBuildSettingsScene>(
                    scene => scene.path.EndsWith($"/{race.sceneName}.unity") && scene.enabled), $"{race.sceneName} not in build");
            }
        }
    }
}
