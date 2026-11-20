using System.Collections.Generic;
using RealisticCarRacing.Vehicles;

namespace RealisticCarRacing.Racing
{
    /// <summary>One entrant: who drives it, its car, its progress and its timing.</summary>
    public sealed class Racer
    {
        readonly List<float> checkpointTimes = new List<float>();

        public Racer(string name, bool isPlayer, CarDefinition car, VehicleController vehicle, RaceProgress progress)
        {
            Name = name;
            IsPlayer = isPlayer;
            Car = car;
            Vehicle = vehicle;
            Progress = progress;
        }

        public string Name { get; }
        public bool IsPlayer { get; }
        public CarDefinition Car { get; }
        public VehicleController Vehicle { get; }
        public RaceProgress Progress { get; }

        /// <summary>1-based position, updated every physics step.</summary>
        public int Position { get; set; }
        public bool Finished { get; private set; }
        public float FinishTime { get; private set; }
        /// <summary>Seconds; 0 until a lap is completed.</summary>
        public float BestLap { get; private set; }
        public float LastLap { get; private set; }
        public float LapStartTime { get; private set; }
        public int Respawns { get; set; }
        public float StuckSeconds { get; set; }

        public int CompletedLaps => Progress.CompletedLaps;

        public void RecordCheckpoint(int checkpoint, float raceTime)
        {
            while (checkpointTimes.Count <= checkpoint)
                checkpointTimes.Add(raceTime);
        }

        public bool TryGetCheckpointTime(int checkpoint, out float raceTime)
        {
            bool known = checkpoint >= 0 && checkpoint < checkpointTimes.Count;
            raceTime = known ? checkpointTimes[checkpoint] : 0f;
            return known;
        }

        /// <returns>The lap time just completed.</returns>
        public float CompleteLap(float raceTime)
        {
            LastLap = raceTime - LapStartTime;
            if (BestLap <= 0f || LastLap < BestLap)
                BestLap = LastLap;
            LapStartTime = raceTime;
            return LastLap;
        }

        public void Finish(float raceTime)
        {
            Finished = true;
            FinishTime = raceTime;
        }
    }
}
