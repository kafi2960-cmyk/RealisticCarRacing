using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RealisticCarRacing.Audio
{
    public enum UiSound
    {
        Hover,
        Click,
        Back,
        CountdownBeep,
        CountdownGo,
        LapComplete,
        RaceFinish
    }

    /// <summary>
    /// Menu, HUD and music sound. One instance for the whole session (survives scene loads), with a fixed pair of
    /// 2D sources: one for UI cues, one for music. Both ignore the pause (AudioListener.pause), so menus and the
    /// pause screen still click while the race is frozen.
    /// </summary>
    public sealed class UIAudioManager : MonoBehaviour
    {
        const float HoverInterval = 0.05f;

        static UIAudioManager instance;
        AudioSource cues, music;
        float lastHover;

        public static UIAudioManager Instance
        {
            get
            {
                if (instance == null && GameAudio.Instance != null)
                {
                    var go = new GameObject("UIAudio");
                    DontDestroyOnLoad(go);
                    instance = go.AddComponent<UIAudioManager>();
                    instance.Create(GameAudio.Instance);
                }
                return instance;
            }
        }

        public int CuesPlayed { get; private set; }
        public UiSound LastCue { get; private set; }
        public AudioSource MusicSource => music;

        void Create(GameAudio audio)
        {
            cues = gameObject.AddComponent<AudioSource>();
            cues.playOnAwake = false;
            cues.spatialBlend = 0f;
            cues.ignoreListenerPause = true;
            cues.outputAudioMixerGroup = audio.ui;
            cues.priority = 16;
            music = gameObject.AddComponent<AudioSource>();
            music.playOnAwake = false;
            music.loop = true;
            music.spatialBlend = 0f;
            music.ignoreListenerPause = true;
            music.outputAudioMixerGroup = audio.music;
            music.priority = 8;
        }

        public static void Play(UiSound sound)
        {
            var manager = Instance;
            if (manager != null)
                manager.PlayCue(sound);
        }

        void PlayCue(UiSound sound)
        {
            if (sound == UiSound.Hover)
            {
                if (Time.unscaledTime - lastHover < HoverInterval)
                    return;
                lastHover = Time.unscaledTime;
            }
            var audio = GameAudio.Instance;
            var clip = sound switch
            {
                UiSound.Hover => audio.hover,
                UiSound.Click => audio.click,
                UiSound.Back => audio.back != null ? audio.back : audio.click,
                UiSound.CountdownBeep => audio.countdownBeep,
                UiSound.CountdownGo => audio.countdownGo != null ? audio.countdownGo : audio.countdownBeep,
                UiSound.LapComplete => audio.lapComplete,
                _ => audio.raceFinish
            };
            CuesPlayed++;
            LastCue = sound;
            if (clip != null)
                cues.PlayOneShot(clip, sound == UiSound.Hover ? 0.5f : 1f);
        }

        /// <summary>Starts (or keeps) the menu music; fades out with <see cref="StopMusic"/>.</summary>
        public static void PlayMusic(AudioClip clip)
        {
            var manager = Instance;
            if (manager == null || clip == null)
                return;
            if (manager.music.clip == clip && manager.music.isPlaying)
                return;
            manager.music.clip = clip;
            manager.music.volume = 1f;
            manager.music.Play();
        }

        public static void StopMusic()
        {
            if (instance != null && instance.music.isPlaying)
                instance.music.Stop();
        }
    }

    /// <summary>Hover/select and click sounds for one button (added by <c>UiFactory.Button</c>).</summary>
    public sealed class UiButtonSound : MonoBehaviour, IPointerEnterHandler, ISelectHandler
    {
        public bool isBack;

        void Start()
        {
            if (TryGetComponent(out Button button))
                button.onClick.AddListener(() => UIAudioManager.Play(isBack ? UiSound.Back : UiSound.Click));
        }

        public void OnPointerEnter(PointerEventData eventData) => UIAudioManager.Play(UiSound.Hover);

        public void OnSelect(BaseEventData eventData)
        {
            if (eventData is not PointerEventData)
                UIAudioManager.Play(UiSound.Hover); // keyboard / gamepad moving through the menu
        }
    }
}
