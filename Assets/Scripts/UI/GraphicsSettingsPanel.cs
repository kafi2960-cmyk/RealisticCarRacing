using System.Collections.Generic;
using RealisticCarRacing.Managers;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace RealisticCarRacing.UI
{
    /// <summary>
    /// Graphics menu contents: preset (LOW / MEDIUM / HIGH), resolution scale, frame-rate cap and the performance
    /// overlay. Changes apply immediately through <see cref="GraphicsSettingsService"/> and are saved. Used by the
    /// main menu's SETTINGS screen and the pause menu.
    /// </summary>
    public sealed class GraphicsSettingsPanel : MonoBehaviour
    {
        static readonly string[] PresetTitles = { "LOW", "MEDIUM", "HIGH" };
        static readonly string[] PresetDescriptions =
        {
            "Older and lower-powered devices: 75% resolution, short hard shadows, fewer and closer trees, no bloom or city skyline. 30 FPS cap on phones.",
            "Mid-range computers and phones: 90% resolution, soft shadows to 90 m, 2× anti-aliasing, more trees, bloom and skyline.",
            "Powerful computers: full resolution, soft 4-cascade shadows to 180 m, ambient occlusion, 4× anti-aliasing, the full forest."
        };

        readonly List<(int preset, Text label)> presetLabels = new List<(int, Text)>();
        readonly List<(int limit, Text label)> frameRateLabels = new List<(int, Text)>();
        Text presetDescription, scaleValue, overlayLabel;

        public Selectable FirstSelectable { get; private set; }

        /// <summary>Adds the settings rows to a vertical layout and returns the panel.</summary>
        public static GraphicsSettingsPanel Build(RectTransform parent, UnityAction onBack)
        {
            var panel = parent.gameObject.AddComponent<GraphicsSettingsPanel>();
            panel.Create(parent, onBack);
            return panel;
        }

        void Create(RectTransform parent, UnityAction onBack)
        {
            Caption(parent, "PRESET");
            var presets = Row(parent, 64f);
            for (int i = 0; i < Mathf.Min(PresetTitles.Length, GraphicsSettingsService.PresetCount); i++)
            {
                int preset = i;
                var button = UiFactory.Button(presets, PresetTitles[i], () => { GraphicsSettingsService.SetPreset(preset); Refresh(); }, 26, 64f, $"Preset {PresetTitles[i]}");
                presetLabels.Add((preset, button.GetComponentInChildren<Text>()));
                if (i == 0)
                    FirstSelectable = button;
            }
            presetDescription = UiFactory.Text(parent, "", 22, UiTheme.TextDim, TextAnchor.UpperLeft, FontStyle.Normal, "PresetDescription");
            UiFactory.Size(presetDescription, height: 56f);

            Caption(parent, "RESOLUTION SCALE");
            var scale = Row(parent, 64f);
            UiFactory.Size(UiFactory.Button(scale, "−", () => ChangeScale(-0.05f), 36, 72f, "ScaleDown"), width: 90f);
            scaleValue = UiFactory.Text(scale, "", 34, UiTheme.Text, TextAnchor.MiddleCenter, FontStyle.Bold, "ScaleValue");
            UiFactory.Size(scaleValue, width: 140f);
            UiFactory.Size(UiFactory.Button(scale, "+", () => ChangeScale(0.05f), 36, 72f, "ScaleUp"), width: 90f);
            UiFactory.Size(UiFactory.Button(scale, "PRESET", () => { GraphicsSettingsService.ResetRenderScale(); Refresh(); }, 24, 72f, "ScaleReset"), flexibleWidth: 1f);

            Caption(parent, "FRAME RATE LIMIT");
            var rates = Row(parent, 64f);
            foreach (int limit in GraphicsSettingsService.FrameRateOptions)
            {
                if (limit == 120 && !SupportsHighRefresh())
                    continue;
                int chosen = limit;
                string title = limit < 0 ? "MAX" : limit.ToString();
                var button = UiFactory.Button(rates, title, () => { GraphicsSettingsService.SetFrameRate(chosen); Refresh(); }, 26, 72f, $"Fps {title}");
                frameRateLabels.Add((limit, button.GetComponentInChildren<Text>()));
            }

            var overlay = UiFactory.Button(parent, "", () => { GraphicsSettingsService.SetShowPerformance(!GraphicsSettingsService.Preferences.showPerformance); Refresh(); }, 26, 72f, "ShowPerformance");
            overlayLabel = overlay.GetComponentInChildren<Text>();
            UiFactory.Button(parent, "BACK", onBack, 32, 84f, "Button BACK");
            Refresh();
        }

        void OnEnable()
        {
            if (presetDescription != null)
                Refresh();
        }

        void ChangeScale(float step)
        {
            GraphicsSettingsService.SetRenderScale(GraphicsSettingsService.EffectiveRenderScale + step);
            Refresh();
        }

        public void Refresh()
        {
            var prefs = GraphicsSettingsService.Preferences;
            foreach (var (preset, label) in presetLabels)
                label.text = (preset == prefs.preset ? "• " : "") + PresetTitles[preset];
            presetDescription.text = PresetDescriptions[Mathf.Clamp(prefs.preset, 0, PresetDescriptions.Length - 1)];
            int effective = GraphicsSettingsService.EffectiveFrameRate;
            foreach (var (limit, label) in frameRateLabels)
                label.text = (limit == effective ? "• " : "") + (limit < 0 ? "MAX" : limit.ToString());
            overlayLabel.text = prefs.showPerformance ? "PERFORMANCE OVERLAY: ON" : "PERFORMANCE OVERLAY: OFF";
            scaleValue.text = $"{Mathf.RoundToInt(GraphicsSettingsService.EffectiveRenderScale * 100f)}%";
        }

        static bool SupportsHighRefresh() => !Application.isMobilePlatform || Screen.currentResolution.refreshRateRatio.value >= 119.0;

        static void Caption(Transform parent, string text) =>
            UiFactory.Size(UiFactory.Text(parent, text, 22, UiTheme.TextDim, TextAnchor.MiddleLeft, FontStyle.Bold, text), height: 30f);

        static RectTransform Row(Transform parent, float height)
        {
            var row = UiFactory.Rect("Row", parent);
            UiFactory.Row(row, 10f).childForceExpandWidth = true;
            UiFactory.Size(row, height: height);
            return row;
        }
    }
}
