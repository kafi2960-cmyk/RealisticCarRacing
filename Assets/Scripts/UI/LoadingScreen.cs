using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RealisticCarRacing.UI
{
    /// <summary>
    /// Loads a scene asynchronously behind a full-screen loading overlay with a progress bar, so the game stays
    /// responsive (and the OS doesn't think it has hung) while the circuit loads. Records the last load time.
    /// </summary>
    public sealed class LoadingScreen : MonoBehaviour
    {
        static LoadingScreen active;

        Image bar;

        /// <summary>Seconds the last scene load took, from request to first rendered frame.</summary>
        public static float LastLoadSeconds { get; private set; }

        public static bool IsLoading => active != null;

        public static void Load(string sceneName)
        {
            if (active != null)
                return; // a load is already under way
            var canvas = UiFactory.Canvas("LoadingScreen", 200);
            DontDestroyOnLoad(canvas.gameObject);
            active = canvas.gameObject.AddComponent<LoadingScreen>();
            active.Build(canvas.transform);
            active.StartCoroutine(active.Run(sceneName));
        }

        void Build(Transform root)
        {
            var background = UiFactory.Image(root, "Background", new Color(0.03f, 0.035f, 0.045f, 1f), UiFactory.White);
            background.raycastTarget = true;
            background.rectTransform.Stretch();
            var title = UiFactory.Text(background.transform, "LOADING", 48, UiTheme.Text, TextAnchor.MiddleCenter, FontStyle.Bold, "Title");
            title.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(800f, 80f));
            var track = UiFactory.Image(background.transform, "BarBackground", new Color(1f, 1f, 1f, 0.12f), UiFactory.White);
            track.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(700f, 10f));
            bar = UiFactory.Image(track.transform, "Bar", UiTheme.Accent, UiFactory.White);
            bar.rectTransform.Stretch();
            bar.type = Image.Type.Filled;
            bar.fillMethod = Image.FillMethod.Horizontal;
            bar.fillAmount = 0f;
        }

        IEnumerator Run(string sceneName)
        {
            float start = Time.realtimeSinceStartup;
            yield return null; // draw the overlay before loading starts
            var priority = Application.backgroundLoadingPriority;
            Application.backgroundLoadingPriority = ThreadPriority.High;
            var operation = SceneManager.LoadSceneAsync(sceneName);
            while (!operation.isDone)
            {
                bar.fillAmount = Mathf.Clamp01(operation.progress / 0.9f);
                yield return null;
            }
            Application.backgroundLoadingPriority = priority;
            bar.fillAmount = 1f;
            yield return null; // first frame of the new scene
            LastLoadSeconds = Time.realtimeSinceStartup - start;
            active = null;
            Destroy(gameObject);
        }
    }
}
