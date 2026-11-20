using UnityEngine;

namespace RealisticCarRacing.Racing
{
    public enum AIDifficulty
    {
        Easy,
        Medium,
        Hard
    }

    /// <summary>One race event: which scene/track, how many laps, how many AI opponents and how fast they are.</summary>
    [CreateAssetMenu(fileName = "Race", menuName = "RealisticCarRacing/Race Definition")]
    public sealed class RaceDefinition : ScriptableObject
    {
        [Tooltip("Stable identifier used in save data (best times).")]
        public string id = "race";
        public string displayName = "Race";
        [TextArea] public string description;
        public string sceneName = "Circuit_GrandPrix";
        public string trackName = "Grand Prix Circuit";
        [Min(1)] public int laps = 3;
        [Tooltip("AI cars on the grid (the starting grid has 12 slots).")]
        [Range(0, 11)] public int opponents = 5;
        public AIDifficulty difficulty = AIDifficulty.Medium;

        public string Summary => opponents == 0
            ? $"{laps} laps · time trial"
            : $"{laps} laps · {opponents} opponents · {difficulty}";
    }
}
