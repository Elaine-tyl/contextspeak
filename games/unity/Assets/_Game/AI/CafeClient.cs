using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace ContextSpeak.Cafe
{
    [Serializable] internal class LocalSession { public string token; }
    [Serializable] public class Transcript { public string text; }
    [Serializable] public class Interpretation { public string text; public string stage; }
    [Serializable] public class Conversation
    {
        public string text;
        public int turn;
        public string order;
        public FriendAnswer[] history;
    }
    [Serializable] internal class SpeechRequest { public string text; }

    // Only the existing loopback guest companion is allowed. Tokens never enter saves.
    public sealed class CafeClient : MonoBehaviour
    {
        private const string Origin = "http://127.0.0.1:4320";
        private string token = "";
        private int generation;
        private int sessionGeneration;
        private UnityWebRequest sessionRequest;
        private readonly HashSet<UnityWebRequest> active = new HashSet<UnityWebRequest>();
        public int MaxTranscriptCharacters = 400;
        public bool Connected => token != "";
        public IEnumerator Connect(Action<string> done)
        {
            CancelSession();
            token = "";
            yield return Request("/session", null, bytes => {
                try {
                    var session = JsonUtility.FromJson<LocalSession>(Encoding.UTF8.GetString(bytes));
                    if (session != null && !string.IsNullOrEmpty(session.token) && session.token.Length <= 100) token = session.token;
                }
                catch (ArgumentException) { }
                done(Connected ? "" : "Local connection unavailable. Start pnpm world:dev.");
            }, done);
        }
        public IEnumerator Json<T>(string path, object value, Action<T> success, Action<string> failure) where T : class
        {
            yield return Request(path, Encoding.UTF8.GetBytes(JsonUtility.ToJson(value)), bytes => {
                try {
                    var parsed = JsonUtility.FromJson<T>(Encoding.UTF8.GetString(bytes));
                    if (parsed == null) failure("Local AI returned an empty reply. Please retry.");
                    else success(parsed);
                }
                catch (ArgumentException) { failure("Local AI returned an invalid reply. Please retry."); }
            }, failure);
        }
        public IEnumerator Transcribe(byte[] wave, Action<string> success, Action<string> failure)
        {
            if (wave == null || wave.Length > 8000000) { failure("Use a shorter recording."); yield break; }
            yield return Request("/transcribe", wave, bytes => {
                try { success(ReadTranscript(bytes, MaxTranscriptCharacters)); }
                catch (FormatException error) { failure(error.Message); }
            }, failure, "audio/wav");
        }
        public static string ReadTranscript(byte[] bytes, int maximumCharacters = 400)
        {
            Transcript result;
            try { result = JsonUtility.FromJson<Transcript>(Encoding.UTF8.GetString(bytes)); }
            catch (ArgumentException) { throw new FormatException("Recording could not be checked. Retry or type your reply."); }
            if (result == null || string.IsNullOrWhiteSpace(result.text)) throw new FormatException("No clear speech. Retry or type your reply.");
            if (result.text.Length > Math.Min(2400, Math.Max(400, maximumCharacters))) throw new FormatException("Use a shorter reply. Your task is kept.");
            return result.text;
        }
        public IEnumerator Speak(string text, Action<AudioClip> success, Action<string> failure)
        {
            if (text.Length > 1000) { failure("The spoken line is too long."); yield break; }
            yield return Request("/speech", Encoding.UTF8.GetBytes(JsonUtility.ToJson(new SpeechRequest { text = text })), bytes => {
                try {
                    var wave = CafeWave.Decode(bytes);
                    var clip = AudioClip.Create("Local AI voice", wave.Samples.Length, 1, wave.Rate, false);
                    clip.SetData(wave.Samples, 0);
                    success(clip);
                }
                catch (FormatException) { failure("Voice unavailable. Use Hear to retry; your task is kept."); }
            }, failure);
        }
        private IEnumerator Request(string path, byte[] body, Action<byte[]> success, Action<string> failure, string contentType = "application/json")
        {
            if (path != "/session" && !Connected) { failure("Start pnpm world:dev, then reconnect."); yield break; }
            var session = path == "/session";
            var current = session ? sessionGeneration : generation;
            var connection = sessionGeneration;
            using var request = new UnityWebRequest(Origin + path, body == null ? "GET" : "POST");
            request.downloadHandler = new DownloadHandlerBuffer();
            if (body != null) {
                request.uploadHandler = new UploadHandlerRaw(body);
                request.SetRequestHeader("Content-Type", contentType);
            }
            request.SetRequestHeader("X-ContextSpeak-Client", Application.platform == RuntimePlatform.WebGLPlayer ? "web" : "native");
            if (Connected) request.SetRequestHeader("Authorization", "Bearer " + token);
            request.timeout = path == "/speech" ? 90 : 40;
            if (session) sessionRequest = request;
            else active.Add(request);
            yield return request.SendWebRequest();
            if (session && sessionRequest == request) sessionRequest = null;
            else active.Remove(request);
            if (current != (session ? sessionGeneration : generation)) yield break;
            if (request.result != UnityWebRequest.Result.Success || request.downloadHandler.data.Length > 3200000)
            {
                // An old request must not invalidate a newer successful reconnection.
                if (connection == sessionGeneration &&
                    (request.result == UnityWebRequest.Result.ConnectionError || request.responseCode == 401))
                    token = "";
                failure("Local AI is unavailable or busy. Your task is kept; reconnect and retry.");
            }
            else success(request.downloadHandler.data);
        }
        public void Cancel()
        {
            // Closing a conversation must not cancel the initial connection handshake.
            generation++;
            foreach (var request in active) request.Abort();
            active.Clear();
        }
        private void CancelSession()
        {
            sessionGeneration++;
            sessionRequest?.Abort();
            sessionRequest = null;
        }
        private void OnDestroy()
        {
            Cancel();
            CancelSession();
        }
    }
}
