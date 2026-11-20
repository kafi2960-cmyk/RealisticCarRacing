using System.Collections.Generic;

namespace RealisticCarRacing.Racing
{
    /// <summary>Race order and timing gaps.</summary>
    public static class RaceStandings
    {
        // Cached so sorting every physics step doesn't allocate a new delegate.
        static readonly System.Comparison<Racer> Comparer = Compare;

        /// <summary>Finishers by finishing time, then everyone else by distance covered. Writes 1-based positions.</summary>
        public static void Sort(List<Racer> racers)
        {
            racers.Sort(Comparer);
            for (int i = 0; i < racers.Count; i++)
                racers[i].Position = i + 1;
        }

        public static int Compare(Racer a, Racer b)
        {
            if (a.Finished && b.Finished)
                return a.FinishTime.CompareTo(b.FinishTime);
            if (a.Finished != b.Finished)
                return a.Finished ? -1 : 1;
            return b.Progress.Distance.CompareTo(a.Progress.Distance);
        }

        /// <summary>
        /// Gap to the leader as a timing screen shows it: the difference in the times both cars passed the last
        /// checkpoint this racer has reached, or whole laps behind if lapped.
        /// </summary>
        public static string Gap(Racer racer, Racer leader)
        {
            if (racer == leader)
                return racer.Finished ? RaceTimeFormat.Format(racer.FinishTime) : "LEADER";
            if (racer.Finished && leader.Finished)
                return "+" + RaceTimeFormat.FormatGap(racer.FinishTime - leader.FinishTime);

            int checkpoint = racer.Progress.HighestCheckpoint;
            int lapsBehind = (leader.Progress.HighestCheckpoint - checkpoint) / racer.Progress.CheckpointsPerLap;
            if (lapsBehind >= 1)
                return lapsBehind == 1 ? "+1 LAP" : $"+{lapsBehind} LAPS";
            if (racer.TryGetCheckpointTime(checkpoint, out float racerTime) && leader.TryGetCheckpointTime(checkpoint, out float leaderTime))
                return "+" + RaceTimeFormat.FormatGap(racerTime - leaderTime);
            return "—";
        }
    }

    public static class RaceTimeFormat
    {
        /// <summary>m:ss.fff</summary>
        public static string Format(float seconds)
        {
            if (seconds <= 0f)
                return "-:--.---";
            int minutes = (int)(seconds / 60f);
            return $"{minutes}:{seconds - minutes * 60f:00.000}";
        }

        /// <summary>s.fff for short gaps, m:ss.fff for long ones.</summary>
        public static string FormatGap(float seconds) => seconds < 60f ? $"{seconds:0.000}" : Format(seconds);
    }
}
