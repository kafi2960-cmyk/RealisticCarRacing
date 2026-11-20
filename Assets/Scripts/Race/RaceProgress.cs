using System;
using RealisticCarRacing.Tracks;
using UnityEngine;

namespace RealisticCarRacing.Racing
{
    /// <summary>
    /// Tracks one car's race distance along the track centreline and turns it into checkpoints and laps.
    /// <para>
    /// <see cref="Distance"/> is continuous: it starts slightly negative on the grid (behind the line), grows as the
    /// car drives forward and shrinks if it reverses. Checkpoints sit at equal spacing along the lap; checkpoint
    /// <c>g</c> (counted over the whole race) is passed when Distance first exceeds <c>g × spacing</c>, so they can only
    /// be reached in order and a lap only counts once every checkpoint on it has been passed. Re-crossing a line
    /// after backing up never counts twice.
    /// </para>
    /// </summary>
    public sealed class RaceProgress
    {
        const int SampleSearchRadius = 30;

        readonly RaceTrack track;
        readonly float checkpointSpacing;
        int sample;
        float lapDistance;

        public RaceProgress(RaceTrack track, int checkpointsPerLap, Vector3 startPosition)
        {
            this.track = track;
            CheckpointsPerLap = Mathf.Max(1, checkpointsPerLap);
            checkpointSpacing = track.Length / CheckpointsPerLap;
            sample = track.FindClosestSample(startPosition);
            lapDistance = Project(startPosition);
            // Grid slots are behind the line: treat "almost a full lap" as "just before the start".
            Distance = lapDistance > track.Length * 0.5f ? lapDistance - track.Length : lapDistance;
            HighestCheckpoint = Mathf.FloorToInt(Distance / checkpointSpacing);
        }

        public int CheckpointsPerLap { get; }
        /// <summary>Metres driven along the track since the start line (negative on the grid).</summary>
        public float Distance { get; private set; }
        /// <summary>Highest checkpoint index reached over the whole race; 0 is the first crossing of the start line.</summary>
        public int HighestCheckpoint { get; private set; }
        public int CompletedLaps => HighestCheckpoint <= 0 ? 0 : HighestCheckpoint / CheckpointsPerLap;
        /// <summary>Closest centreline sample (useful for AI and respawning).</summary>
        public int Sample => sample;
        /// <summary>How long the car has been driving against the race direction.</summary>
        public float WrongWaySeconds { get; private set; }

        /// <summary>Raised once per checkpoint, in order, with the race-wide checkpoint index.</summary>
        public event Action<int> CheckpointReached;
        /// <summary>Raised with the number of laps now completed.</summary>
        public event Action<int> LapCompleted;

        public void Update(Vector3 position, Vector3 velocity, float deltaTime)
        {
            sample = track.FindClosestSample(position, sample, SampleSearchRadius);
            float current = Project(position);
            float delta = current - lapDistance;
            float half = track.Length * 0.5f;
            if (delta > half) delta -= track.Length;
            else if (delta < -half) delta += track.Length;
            lapDistance = current;
            Distance += delta;

            int reached = Mathf.FloorToInt(Distance / checkpointSpacing);
            while (reached > HighestCheckpoint)
            {
                HighestCheckpoint++;
                CheckpointReached?.Invoke(HighestCheckpoint);
                if (HighestCheckpoint > 0 && HighestCheckpoint % CheckpointsPerLap == 0)
                    LapCompleted?.Invoke(HighestCheckpoint / CheckpointsPerLap);
            }

            bool againstTraffic = Vector3.Dot(velocity, track.GetSample(sample).forward) < -2f;
            WrongWaySeconds = againstTraffic ? WrongWaySeconds + deltaTime : 0f;
        }

        /// <summary>Call after the car is teleported (respawn) so tracking searches the whole track again.</summary>
        public void ResetTracking(Vector3 position) => sample = track.FindClosestSample(position);

        float Project(Vector3 position)
        {
            var s = track.GetSample(sample);
            return Mathf.Repeat(s.distance + Vector3.Dot(position - s.position, s.forward), track.Length);
        }
    }
}
