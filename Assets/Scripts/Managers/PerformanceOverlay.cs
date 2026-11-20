using RealisticCarRacing.UI;
using UnityEngine;
using UnityEngine.UI;

namespace RealisticCarRacing.Managers
{
    /// <summary>
    /// Small FPS / frame-time / memory read-out (graphics menu → Show performance). Persists across scenes and
    /// updates twice a second so it costs next to nothing.
    /// </summary>
    public sealed class PerformanceOverlay : MonoBehaviour
    {
        const float RefreshSeconds = 0.5f;
        readonly FrameTiming[] timings = new FrameTiming[1];
        Text text;
        int frames;
        float elapsed;

        public string LastText => text != null ? text.text : string.Empty;

        public static PerformanceOverlay Create()
        {
            var canvas = UiFactory.Canvas("PerformanceOverlay", 100);
            DontDestroyOnLoad(canvas.gameObject);
            var overlay = canvas.gameObject.AddComponent<PerformanceOverlay>();
            var background = UiFactory.Image(canvas.transform, "Background", new Color(0f, 0f, 0f, 0.55f), UiFactory.White);
            background.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -178f), new Vector2(560f, 40f));
            overlay.text = UiFactory.Text(background.transform, "", 22, UiTheme.Text, TextAnchor.MiddleCenter, FontStyle.Bold, "Stats");
            return overlay;
        }

        void Update()
        {
            frames++;
            elapsed += Time.unscaledDeltaTime;
            FrameTimingManager.CaptureFrameTimings();
            if (elapsed < RefreshSeconds)
                return;

            float fps = frames / elapsed;
            string cpu = "";
            if (FrameTimingManager.GetLatestTimings(1, timings) > 0 && timings[0].cpuMainThreadFrameTime > 0)
                cpu = $" · CPU {timings[0].cpuMainThreadFrameTime:0.0} ms";
            long memoryMB = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / (1024 * 1024);
            text.text = $"{fps:0} FPS · {1000f / fps:0.0} ms{cpu} · {memoryMB} MB · {QualitySettings.names[QualitySettings.GetQualityLevel()]}";
            frames = 0;
            elapsed = 0f;
        }
    }
}
