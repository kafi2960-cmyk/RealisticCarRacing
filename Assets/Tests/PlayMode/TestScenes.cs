using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RealisticCarRacing.Tests
{
    /// <summary>Isolation for tests that load game scenes: leave the world empty for whatever test runs next.</summary>
    public static class TestScenes
    {
        public static IEnumerator ResetToEmptyScene()
        {
            Time.timeScale = 1f;
            var empty = SceneManager.CreateScene($"Empty_{Time.frameCount}_{Random.Range(0, int.MaxValue)}");
            SceneManager.SetActiveScene(empty);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene != empty && scene.isLoaded)
                    yield return SceneManager.UnloadSceneAsync(scene);
            }
        }
    }
}
