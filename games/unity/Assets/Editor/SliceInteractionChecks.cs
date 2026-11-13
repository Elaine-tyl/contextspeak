using System;
using System.Linq;
using ContextSpeak.Slice;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace ContextSpeak.Editor
{
    /// Validate runtime agent placement; Edit-mode path sampling alone can hide an NPC on furniture.
    [InitializeOnLoad]
    public static class SliceInteractionChecks
    {
        private const string Phase = "ContextSpeak.SliceInteraction.Phase";
        private const string Started = "ContextSpeak.SliceInteraction.Started";
        static SliceInteractionChecks()
        {
            if (Environment.GetCommandLineArgs().Contains("-contextspeak-interaction-test")) EditorApplication.update += Tick;
        }
        public static void Begin()
        {
            EditorSceneManager.OpenScene("Assets/Generated/ForestSlice.unity");
            var game = UnityEngine.Object.FindFirstObjectByType<SliceGame>();
            game.enabled = false; // no network request or access to the player's actual save
            SessionState.SetInt(Phase, 1);
            SessionState.SetFloat(Started, (float)EditorApplication.timeSinceStartup);
            EditorApplication.isPlaying = true;
        }
        private static void Tick()
        {
            if (SessionState.GetInt(Phase, 0) == 0 || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            var elapsed = EditorApplication.timeSinceStartup - SessionState.GetFloat(Started, 0);
            if (elapsed < 3) return;
            var game = UnityEngine.Object.FindFirstObjectByType<SliceGame>();
            if (game == null) return;
            var arrival = game.OrderArrival;
            var distance = Vector3.Distance(arrival, game.Rowan.position);
            var agent = game.Rowan.GetComponent<NavMeshAgent>();
            Debug.Log("Counter runtime: arrival=" + arrival + "; counter=" + game.Counter.position + "; Rowan=" + game.Rowan.position + "; agent=" + agent.isOnNavMesh + "; distance=" + distance);
            SessionState.SetInt(Phase, 0);
            game.Player.Place(arrival);
            var nearest = (Partner)typeof(SliceGame).GetMethod("NearestPartner", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(game, null);
            var queueFront = game.Counter.position + game.Counter.forward * 2.4f;
            var passed = distance <= game.CounterTalkRange && nearest == Partner.Rowan && Vector3.Distance(arrival, queueFront) > 0.8f;
            Debug.Log(passed ? "Slice counter interaction check passed" : "Slice counter interaction check FAILED");
            EditorApplication.Exit(passed ? 0 : 1);
        }
    }
}
