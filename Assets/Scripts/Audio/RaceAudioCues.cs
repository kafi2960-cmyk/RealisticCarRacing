using RealisticCarRacing.Racing;
using UnityEngine;

namespace RealisticCarRacing.Audio
{
    /// <summary>
    /// Race sounds tied to the race itself: engines start on the grid (the player's first, rivals staggered), the
    /// countdown beeps each second and sounds GO, the player's laps and finish get a cue, and the menu music stops.
    /// </summary>
    public sealed class RaceAudioCues : MonoBehaviour
    {
        RaceManager race;
        int lastCountdownSecond = -1;

        public void Initialise(RaceManager raceManager)
        {
            race = raceManager;
            race.StateChanged += OnStateChanged;
            race.LapCompleted += (racer, lap, time, best) => { if (racer.IsPlayer) UIAudioManager.Play(UiSound.LapComplete); };
            race.RacerFinished += racer => { if (racer.IsPlayer) UIAudioManager.Play(UiSound.RaceFinish); };
            UIAudioManager.StopMusic();
            float delay = 0f;
            foreach (var racer in race.Racers)
            {
                if (racer.Vehicle.TryGetComponent(out VehicleAudioManager audio))
                    audio.Engine.StartEngine(racer.IsPlayer ? 0.2f : (delay += Random.Range(0.15f, 0.35f)));
            }
        }

        void OnDestroy()
        {
            if (race != null)
                race.StateChanged -= OnStateChanged;
        }

        void OnStateChanged(RaceState state)
        {
            if (state == RaceState.Racing)
                UIAudioManager.Play(UiSound.CountdownGo);
        }

        void Update()
        {
            if (race == null || race.State != RaceState.Countdown || race.IsPaused)
                return;
            int second = Mathf.CeilToInt(race.CountdownRemaining);
            if (second != lastCountdownSecond && second >= 1 && second <= 3)
                UIAudioManager.Play(UiSound.CountdownBeep);
            lastCountdownSecond = second;
        }
    }
}
