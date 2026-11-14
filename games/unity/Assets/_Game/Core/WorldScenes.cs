using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace ContextSpeak.Cafe
{
    // Load a replacement before unloading the current district, so a failed load is recoverable.
    public sealed class WorldScenes : MonoBehaviour
    {
        private static WorldScenes instance;
        private bool loading;
        private AsyncOperationHandle<SceneInstance>? current;
        public static void Open(string address, Action<string> failed = null)
        {
            if (WorldSceneCatalog.Find(address) == null)
            {
                failed?.Invoke("This district is not available in this build.");
                return;
            }
            if (instance == null)
            {
                instance = new GameObject("World scene loader").AddComponent<WorldScenes>();
                DontDestroyOnLoad(instance.gameObject);
            }
            instance.StartCoroutine(instance.LoadWhenReady(address, failed));
        }
        private IEnumerator LoadWhenReady(string address, Action<string> failed)
        {
            // The new scene can show controls before the previous one finishes unloading.
            // Retain fast confirm/cancel requests, and acquire the gate before yielding.
            while (loading) yield return null;
            loading = true;
            yield return Load(address, failed);
        }

        private IEnumerator Load(string address, Action<string> failed)
        {
            var previous = SceneManager.GetActiveScene();
            if (!WorldSceneCatalog.Find(address).Streamed)
            {
                if (!Application.CanStreamedLevelBeLoaded(address))
                {
                    loading = false;
                    failed?.Invoke(address + " is missing from this browser build.");
                    yield break;
                }
                var operation = SceneManager.LoadSceneAsync(address, LoadSceneMode.Additive);
                yield return operation;
                SceneManager.SetActiveScene(SceneManager.GetSceneByName(address));
                yield return UnloadPrevious(previous);
                loading = false;
                yield break;
            }
            var request = Addressables.LoadSceneAsync(address, LoadSceneMode.Additive, false);
            yield return request;
            if (request.Status != AsyncOperationStatus.Succeeded)
            {
                Addressables.Release(request);
                loading = false;
                failed?.Invoke("This district could not load. Your current task is kept; try again.");
                yield break;
            }
            yield return request.Result.ActivateAsync();
            SceneManager.SetActiveScene(request.Result.Scene);
            yield return UnloadPrevious(previous);
            current = request;
            loading = false;
        }
        private IEnumerator UnloadPrevious(Scene previous)
        {
            // Normal Single loads may already have released the old Addressables scene.
            if (current.HasValue && current.Value.IsValid() && current.Value.Result.Scene.isLoaded)
                yield return Addressables.UnloadSceneAsync(current.Value);
            else if (previous.IsValid() && previous.isLoaded)
                yield return SceneManager.UnloadSceneAsync(previous);
            current = null;
        }
        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }
    }
}
