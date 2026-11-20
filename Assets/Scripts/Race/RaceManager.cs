using System;
using System.Collections.Generic;
using RealisticCarRacing.AI;
using RealisticCarRacing.Cameras;
using RealisticCarRacing.Controls;
using RealisticCarRacing.Managers;
using RealisticCarRacing.Tracks;
using RealisticCarRacing.Vehicles;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RealisticCarRacing.Racing
{
    public enum RaceState
    {
        Countdown,
        Racing,
        /// <summary>The player has taken the chequered flag; AI cars may still be running.</summary>
        Finished
    }

    /// <summary>
    /// Runs one race: builds the grid from <see cref="GameSession"/> (player car + AI opponents), holds the cars
    /// during the countdown, times laps, tracks checkpoints and positions, finishes racers, recovers stuck AI,
    /// and handles pause / restart / quit. UI listens to its events and reads its state.
    /// </summary>
    public sealed class RaceManager : MonoBehaviour
    {
        const int MaxGridSlots = 12;

        [SerializeField] RaceTrack track;
        [SerializeField] VehicleCameraRig cameraRig;
        [SerializeField] float countdownSeconds = 3f;
        [SerializeField] int checkpointsPerLap = 16;
        [Tooltip("How far the AI racing line swings from the centre, metres.")]
        [SerializeField] float racingLineOffset = 3.2f;
        [Tooltip("Seconds after the player finishes before unfinished cars are classified where they are.")]
        [SerializeField] float classificationTimeout = 45f;
        [Tooltip("Seconds an AI (or autopilot) car may stay stuck, off track or facing the wrong way before it is put back on track.")]
        [SerializeField] float recoverySeconds = 4f;
        [Tooltip("Development: P toggles autopilot for the player car.")]
        [SerializeField] bool allowAutopilotKey = true;

        readonly List<Racer> racers = new List<Racer>();
        readonly List<Racer> standings = new List<Racer>();
        RacingLine racingLine;
        IVehicleInputSource playerControls;
        TrackFollowerDriver playerAutopilot;
        float savedTimeScale = 1f;
        float playerFinishTime = -1f;

        public RaceDefinition Race { get; private set; }
        public RaceTrack Track => track;
        public RaceState State { get; private set; }
        /// <summary>Seconds since the start signal, in simulation time.</summary>
        public float RaceTime { get; private set; }
        public float CountdownRemaining { get; private set; }
        public bool IsPaused { get; private set; }
        /// <summary>True once every car has finished, or the classification timeout has passed.</summary>
        public bool ResultsFinal { get; private set; }
        public bool PlayerAutopilot { get; private set; }
        public IReadOnlyList<Racer> Racers => racers;
        /// <summary>Racers in race order.</summary>
        public IReadOnlyList<Racer> Standings => standings;
        public Racer Player { get; private set; }
        public RecordUpdate PlayerRecords { get; private set; }

        public event Action<RaceState> StateChanged;
        /// <summary>(racer, laps completed, lap time, personal best).</summary>
        public event Action<Racer, int, float, bool> LapCompleted;
        public event Action<Racer> RacerFinished;
        public event Action<bool> PauseChanged;
        public event Action ResultsFinalised;

        public void Configure(RaceTrack raceTrack, VehicleCameraRig rig)
        {
            track = raceTrack;
            cameraRig = rig;
        }

        void Awake()
        {
            Race = GameSession.SelectedRace;
            racingLine = new RacingLine(track, racingLineOffset);
            BuildGrid();
            CountdownRemaining = countdownSeconds;
            State = RaceState.Countdown;
        }

        void BuildGrid()
        {
            var catalog = GameSession.Catalog;
            int fieldSize = Mathf.Clamp(Race.opponents + 1, 1, MaxGridSlots);
            int playerSlot = fieldSize - 1; // the player starts at the back and races through the field
            var random = new System.Random(StableHash(Race.id));
            var playerCar = GameSession.SelectedCar;
            var playerSetup = GameSession.SelectedSetup;

            for (int slot = 0; slot < fieldSize; slot++)
            {
                Pose pose = track.GetGridPose(slot);
                bool isPlayer = slot == playerSlot;
                Racer racer;
                if (isPlayer)
                {
                    var vehicle = CarBuilder.Build(playerCar, playerSetup.paint, playerSetup.tuning, pose.position, pose.rotation, true, "Player");
                    racer = new Racer("YOU", true, playerCar, vehicle, new RaceProgress(track, checkpointsPerLap, pose.position));
                    Player = racer;
                    playerControls = vehicle.InputSource;
                }
                else
                {
                    var car = catalog.cars[random.Next(catalog.cars.Length)];
                    int paint = random.Next(car.paints.Length);
                    if (car == playerCar && paint == playerSetup.paint)
                        paint = (paint + 1) % car.paints.Length; // never mistake an opponent for yourself
                    string name = catalog.opponentNames.Length > 0 ? catalog.opponentNames[slot % catalog.opponentNames.Length] : $"Driver {slot + 1}";
                    var vehicle = CarBuilder.Build(car, paint, TuningPreset.Balanced, pose.position, pose.rotation, false, $"AI {name}");
                    racer = new Racer(name, false, car, vehicle, new RaceProgress(track, checkpointsPerLap, pose.position));
                    var ai = new RaceAIDriver(vehicle, track, racingLine, racers, AIProfile.For(Race.difficulty, (float)random.NextDouble() * 2f - 1f)) { Self = racer };
                    vehicle.SetInputSource(ai);
                }

                var entrant = racer;
                entrant.Vehicle.InputLocked = true;
                entrant.Vehicle.RespawnPoseProvider = () => RespawnPose(entrant);
                entrant.Vehicle.Respawned += () => entrant.Progress.ResetTracking(entrant.Vehicle.transform.position);
                entrant.Progress.CheckpointReached += checkpoint => entrant.RecordCheckpoint(checkpoint, RaceTime);
                entrant.Progress.LapCompleted += laps => OnLapCompleted(entrant, laps);
                racers.Add(entrant);
            }

            standings.AddRange(racers);
            RaceStandings.Sort(standings);
            if (cameraRig != null)
                cameraRig.SetTarget(Player.Vehicle.transform);
        }

        void Update()
        {
            if (State == RaceState.Countdown && !IsPaused)
            {
                CountdownRemaining -= Time.deltaTime;
                if (CountdownRemaining <= 0f)
                    StartRacing();
            }

            if (State == RaceState.Finished && !ResultsFinal && (AllFinished() || RaceTime - playerFinishTime > classificationTimeout))
                FinaliseResults();

            if (allowAutopilotKey && State == RaceState.Racing && !IsPaused && Keyboard.current != null && Keyboard.current.pKey.wasPressedThisFrame)
                SetPlayerAutopilot(!PlayerAutopilot);
        }

        void FixedUpdate()
        {
            if (State == RaceState.Countdown)
                return;

            float dt = Time.fixedDeltaTime;
            RaceTime += dt;
            foreach (var racer in racers)
            {
                var body = racer.Vehicle.Body;
                racer.Progress.Update(racer.Vehicle.transform.position, body.linearVelocity, dt);
                if (!racer.IsPlayer || PlayerAutopilot || racer.Finished)
                    RecoverIfStuck(racer, dt);
            }
            if (!ResultsFinal)
                RaceStandings.Sort(standings);
        }

        void StartRacing()
        {
            CountdownRemaining = 0f;
            foreach (var racer in racers)
                racer.Vehicle.InputLocked = false;
            State = RaceState.Racing;
            StateChanged?.Invoke(State);
        }

        void OnLapCompleted(Racer racer, int laps)
        {
            if (racer.Finished)
                return;
            float lapTime = racer.CompleteLap(RaceTime);
            LapCompleted?.Invoke(racer, laps, lapTime, Mathf.Approximately(lapTime, racer.BestLap));
            if (laps >= Race.laps)
                Finish(racer);
        }

        void Finish(Racer racer)
        {
            racer.Finish(RaceTime);
            RaceStandings.Sort(standings);

            if (racer.IsPlayer)
            {
                playerFinishTime = RaceTime;
                // Cool-down lap: the car drives itself gently while the results come in.
                racer.Vehicle.SetInputSource(new TrackFollowerDriver(racer.Vehicle, track) { MaxSpeed = 22f });
                PlayerAutopilot = false;
                PlayerRecords = GameSession.Profile.SubmitResult(Race.id, racer.Position, racer.FinishTime, racer.BestLap);
                GameSession.Save();
                State = RaceState.Finished;
                RacerFinished?.Invoke(racer);
                StateChanged?.Invoke(State);
            }
            else
            {
                if (racer.Vehicle.InputSource is RaceAIDriver ai)
                    ai.MaxSpeed = 25f;
                RacerFinished?.Invoke(racer);
            }
        }

        bool AllFinished()
        {
            foreach (var racer in racers)
            {
                if (!racer.Finished)
                    return false;
            }
            return true;
        }

        void FinaliseResults()
        {
            RaceStandings.Sort(standings);
            ResultsFinal = true;
            ResultsFinalised?.Invoke();
        }

        /// <summary>Puts AI (and autopilot) cars back on the racing line if stuck, beached or facing the wrong way.</summary>
        void RecoverIfStuck(Racer racer, float dt)
        {
            float speed = racer.Vehicle.Body.linearVelocity.magnitude;
            float lateral = Mathf.Abs(track.LateralOffset(racer.Vehicle.transform.position, racer.Progress.Sample));
            bool inTrouble = speed < 1.5f || lateral > track.RoadWidth * 0.5f + 4f || racer.Progress.WrongWaySeconds > 0f
                             || racer.Vehicle.transform.up.y < 0.3f;
            racer.StuckSeconds = inTrouble ? racer.StuckSeconds + dt : 0f;
            if (racer.StuckSeconds < recoverySeconds)
                return;
            racer.StuckSeconds = 0f;
            racer.Respawns++;
            racer.Vehicle.ResetVehicle();
        }

        /// <summary>On the racing line at the car's progress, moved back along the track if another car is in the way.</summary>
        Pose RespawnPose(Racer racer)
        {
            int sample = racer.Progress.Sample;
            float spacing = track.Length / track.SampleCount;
            Vector3 position = Vector3.zero;
            TrackSample s = default;
            for (int attempt = 0; attempt < 12; attempt++, sample -= Mathf.CeilToInt(8f / spacing))
            {
                s = track.GetSample(sample);
                position = s.position + s.right * racingLine.OffsetAt(sample) + Vector3.up * (track.RoadSurfaceHeight + 0.4f);
                if (IsClear(position, racer))
                    break;
            }
            return new Pose(position, Quaternion.LookRotation(s.forward, Vector3.up));
        }

        bool IsClear(Vector3 position, Racer except)
        {
            foreach (var other in racers)
            {
                if (other != except && (other.Vehicle.transform.position - position).sqrMagnitude < 36f)
                    return false;
            }
            return true;
        }

        public void SetPlayerAutopilot(bool engaged)
        {
            if (State == RaceState.Finished)
                return;
            PlayerAutopilot = engaged;
            playerAutopilot ??= new TrackFollowerDriver(Player.Vehicle, track);
            Player.Vehicle.SetInputSource(engaged ? playerAutopilot : playerControls);
        }

        public void SetPaused(bool paused)
        {
            if (paused == IsPaused)
                return;
            IsPaused = paused;
            if (paused)
            {
                savedTimeScale = Time.timeScale;
                Time.timeScale = 0f;
            }
            else
            {
                Time.timeScale = savedTimeScale;
            }
            AudioListener.pause = paused;
            PauseChanged?.Invoke(paused);
        }

        public void TogglePause() => SetPaused(!IsPaused);

        public void Restart()
        {
            SetPaused(false);
            SceneFlow.StartRace(Race);
        }

        public void QuitToMenu()
        {
            SetPaused(false);
            SceneFlow.LoadMainMenu();
        }

        /// <summary>Starts the race immediately (tests, replays).</summary>
        public void SkipCountdown() => CountdownRemaining = 0f;

        /// <summary>The race after this one in the catalog, wrapping round.</summary>
        public RaceDefinition NextRace()
        {
            var races = GameSession.Catalog.races;
            int index = GameSession.Catalog.IndexOf(Race);
            return races[(index + 1 + races.Length) % races.Length];
        }

        void OnDestroy()
        {
            if (IsPaused)
                Time.timeScale = savedTimeScale;
            AudioListener.pause = false;
        }

        static int StableHash(string text)
        {
            unchecked
            {
                int hash = 23;
                foreach (char c in text)
                    hash = hash * 31 + c;
                return hash;
            }
        }
    }
}
