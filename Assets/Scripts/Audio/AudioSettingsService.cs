using RealisticCarRacing.Managers;
using UnityEngine;

namespace RealisticCarRacing.Audio
{
    /// <summary>
    /// Player volume settings: master, one per mixer group, and mute. Saved in the profile and applied to the mixer's
    /// exposed parameters (MasterVolume, EngineVolume, ...), so every sound in a group follows its slider.
    /// </summary>
    public static class AudioSettingsService
    {
        public const string MasterParameter = "MasterVolume";
        const float Silent = -80f;

        public static AudioPreferences Preferences => GameSession.Profile.audio ??= new AudioPreferences();

        public static string Parameter(AudioCategory category) => category + "Volume";

        public static float Volume(AudioCategory category) => Preferences.Get(category);

        public static void SetVolume(AudioCategory category, float volume)
        {
            Preferences.Set(category, Mathf.Clamp01(volume));
            Apply();
            GameSession.Save();
        }

        public static void SetMasterVolume(float volume)
        {
            Preferences.master = Mathf.Clamp01(volume);
            Apply();
            GameSession.Save();
        }

        public static void SetMuted(bool muted)
        {
            Preferences.muted = muted;
            Apply();
            GameSession.Save();
        }

        /// <summary>Linear 0..1 slider value to mixer decibels (0 → silent).</summary>
        public static float ToDecibels(float volume) => volume <= 0.0001f ? Silent : Mathf.Max(Silent, 20f * Mathf.Log10(volume));

        /// <summary>Pushes the saved settings to the mixer. Called at start-up and after every change.</summary>
        public static void Apply()
        {
            var audio = GameAudio.Instance;
            if (audio == null || audio.mixer == null)
                return;
            var prefs = Preferences;
            audio.mixer.SetFloat(MasterParameter, ToDecibels(prefs.muted ? 0f : prefs.master));
            foreach (AudioCategory category in System.Enum.GetValues(typeof(AudioCategory)))
                audio.mixer.SetFloat(Parameter(category), ToDecibels(prefs.Get(category)));
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void ApplyOnStart() => Apply();
    }
}
