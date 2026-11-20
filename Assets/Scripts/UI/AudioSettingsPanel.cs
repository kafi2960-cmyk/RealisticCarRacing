using System.Collections.Generic;
using RealisticCarRacing.Audio;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace RealisticCarRacing.UI
{
    /// <summary>
    /// Audio menu contents: master volume, one volume per mixer group (engine, transmission, tyres, environment, UI,
    /// music) in 10 % steps, and mute. Changes apply immediately through <see cref="AudioSettingsService"/> and are
    /// saved. Used by the main menu's AUDIO screen and the pause menu.
    /// </summary>
    public sealed class AudioSettingsPanel : MonoBehaviour
    {
        const float Step = 0.1f;
        static readonly (AudioCategory category, string title)[] Channels =
        {
            (AudioCategory.Engine, "ENGINE"), (AudioCategory.Transmission, "GEARBOX"), (AudioCategory.Tires, "TYRES & BRAKES"),
            (AudioCategory.Environment, "WIND, HORN & IMPACTS"), (AudioCategory.UI, "MENUS"), (AudioCategory.Music, "MUSIC")
        };

        readonly List<(AudioCategory? category, Text value)> values = new List<(AudioCategory?, Text)>();
        Text muteLabel;

        public Selectable FirstSelectable { get; private set; }

        public static AudioSettingsPanel Build(RectTransform parent, UnityAction onBack)
        {
            var panel = parent.gameObject.AddComponent<AudioSettingsPanel>();
            panel.Create(parent, onBack);
            return panel;
        }

        void Create(RectTransform parent, UnityAction onBack)
        {
            FirstSelectable = VolumeRow(parent, "MASTER", null);
            foreach (var (category, title) in Channels)
                VolumeRow(parent, title, category);
            var mute = UiFactory.Button(parent, "", () => { AudioSettingsService.SetMuted(!AudioSettingsService.Preferences.muted); Refresh(); }, 26, 64f, "Mute");
            muteLabel = mute.GetComponentInChildren<Text>();
            UiFactory.Button(parent, "BACK", onBack, 32, 76f, "Button BACK");
            Refresh();
        }

        Selectable VolumeRow(Transform parent, string title, AudioCategory? category)
        {
            var row = UiFactory.Rect($"Volume {title}", parent);
            UiFactory.Row(row, 10f);
            UiFactory.Size(row, height: 52f);
            UiFactory.Size(UiFactory.Text(row, title, 20, UiTheme.TextDim, TextAnchor.MiddleLeft, FontStyle.Bold), flexibleWidth: 1f);
            string id = category.HasValue ? category.Value.ToString() : "Master";
            var down = UiFactory.Button(row, "−", () => Change(category, -Step), 32, 52f, $"VolumeDown {id}");
            UiFactory.Size(down, width: 72f, height: 52f);
            var value = UiFactory.Text(row, "", 26, UiTheme.Text, TextAnchor.MiddleCenter, FontStyle.Bold, "Value");
            UiFactory.Size(value, width: 90f);
            UiFactory.Size(UiFactory.Button(row, "+", () => Change(category, Step), 32, 52f, $"VolumeUp {id}"), width: 72f, height: 52f);
            values.Add((category, value));
            return down;
        }

        static float Get(AudioCategory? category) =>
            category.HasValue ? AudioSettingsService.Volume(category.Value) : AudioSettingsService.Preferences.master;

        void Change(AudioCategory? category, float step)
        {
            float volume = Mathf.Round((Get(category) + step) * 10f) / 10f;
            if (category.HasValue)
                AudioSettingsService.SetVolume(category.Value, volume);
            else
                AudioSettingsService.SetMasterVolume(volume);
            Refresh();
        }

        void OnEnable()
        {
            if (muteLabel != null)
                Refresh();
        }

        public void Refresh()
        {
            foreach (var (category, value) in values)
                value.text = $"{Mathf.RoundToInt(Get(category) * 100f)}%";
            muteLabel.text = AudioSettingsService.Preferences.muted ? "SOUND: MUTED" : "SOUND: ON";
        }
    }
}
