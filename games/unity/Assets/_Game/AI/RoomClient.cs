using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace ContextSpeak.Cafe
{
    [Serializable] public class RoomPose
    {
        public float x, y, z, yaw;
        public bool moving;
        public string activity = "exploring", npc = "";
    }
    [Serializable] public sealed class RoomPlayer : RoomPose { public string id, name, look; }
    [Serializable] internal sealed class RoomReply { public string code, self, token, error; public RoomPlayer[] players; }
    [Serializable] internal sealed class RoomHello { public string name, look, world = "ForestSlice"; public RoomPose pose; }
    [Serializable] internal sealed class RoomJoin { public string name, look, world = "ForestSlice", code; public RoomPose pose; }
    [Serializable] internal sealed class RoomPoll { public RoomPose pose; public string name, look; }
    [Serializable] internal sealed class RoomConfig { public string url; }

    /// Independent HTTP presence; changing rooms cannot cancel a voice assessment.
    public sealed class RoomClient : MonoBehaviour
    {
        public CafePlayer Player;
        public Func<RoomPose> Capture;
        public string Endpoint { get; private set; } = "http://127.0.0.1:4330";
        public string Code { get; private set; } = "";
        public string Status { get; private set; } = "Preparing friend rooms…";
        public bool Busy { get; private set; } = true;
        public bool Connected => token != "";
        public bool Ready { get; private set; }
        public int Count => Connected ? peers.Count + 1 : 0;
        private const string EndpointKey = "ContextSpeak.RoomServer";
        private string token = "", self = "";
        private int generation, failures;
        private float nextPoll;
        private bool polling;
        private readonly Dictionary<string, RemotePlayer> peers = new Dictionary<string, RemotePlayer>();
        private RoomPlayer[] players = Array.Empty<RoomPlayer>();
        public RoomPlayer[] Players => players;

        private IEnumerator Start()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            if (PlayerPrefs.HasKey(EndpointKey)) Endpoint = PlayerPrefs.GetString(EndpointKey);
            else
#endif
            {
                var origin = "http://127.0.0.1:4320";
#if UNITY_WEBGL && !UNITY_EDITOR
                origin = new Uri(Application.absoluteURL).GetLeftPart(UriPartial.Authority);
#endif
                using var request = UnityWebRequest.Get(origin + "/rooms-config");
                request.timeout = 3;
                request.redirectLimit = 0;
                yield return request.SendWebRequest();
                if (request.result == UnityWebRequest.Result.Success)
                {
                    RoomConfig config = null;
                    try { config = JsonUtility.FromJson<RoomConfig>(request.downloadHandler.text); }
                    catch (ArgumentException) { }
                    if (config != null && ValidEndpoint(config.url)) Endpoint = config.url.TrimEnd('/');
                }
            }
            Ready = true;
            Busy = false;
            Status = "Create a room or enter a friend's code.";
        }

        private static bool ValidEndpoint(string value) => Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && (uri.Scheme == "https" || uri.Scheme == "http") && string.IsNullOrEmpty(uri.UserInfo)
            && uri.AbsolutePath == "/" && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment);

        public void SetEndpoint(string value)
        {
            if (Connected || Busy) return;
            if (!ValidEndpoint(value)) { Status = "Use an HTTP(S) server origin without a path or credentials."; return; }
            Endpoint = value.Trim().TrimEnd('/');
            PlayerPrefs.SetString(EndpointKey, Endpoint);
            PlayerPrefs.Save();
            Status = "Room server updated.";
        }

        public void Join(string code = "")
        {
            if (!Ready || Busy || Connected || Capture == null) return;
            code = code.Trim().ToUpperInvariant();
            if (code != "" && (code.Length != 12 || code.Any(letter => !Uri.IsHexDigit(letter))))
            { Status = "Enter the 12-character room code."; return; }
            Busy = true;
            Status = code == "" ? "Creating room…" : "Joining room…";
            var name = PlayerName();
            var look = Appearance().Selected.ToString();
            var body = code == "" ? JsonUtility.ToJson(new RoomHello { name = name, look = look, pose = Capture() })
                : JsonUtility.ToJson(new RoomJoin { name = name, look = look, code = code, pose = Capture() });
            var current = ++generation;
            var endpoint = Endpoint;
            StartCoroutine(Request(endpoint, code == "" ? "create" : "join", body, "", (reply, error) => {
                if (current != generation || !isActiveAndEnabled)
                {
                    if (!string.IsNullOrEmpty(reply?.token)) SendLeave(endpoint, reply.token);
                    return;
                }
                Busy = false;
                if (error != "" || !ValidReply(reply) || !ValidToken(reply.token))
                { Status = error != "" ? error : "Invalid room response. Try again."; return; }
                Code = reply.code;
                self = reply.self;
                token = reply.token;
                failures = 0;
                Apply(reply);
                Status = "Connected · share the code with friends.";
                nextPoll = Time.unscaledTime;
            }));
        }

        private PlayerAppearance Appearance() => Player.Model.GetComponent<PlayerAppearance>();
        private static string PlayerName()
        {
            var name = new string(CharacterProfiles.Draft().Name.Where(letter => !char.IsControl(letter) && letter != '<' && letter != '>').Take(24).ToArray()).Trim();
            return name.Length == 0 ? "Friend" : name;
        }

        private void Update()
        {
            if (!Connected || polling || Time.unscaledTime < nextPoll) return;
            polling = true;
            var current = generation;
            var body = JsonUtility.ToJson(new RoomPoll { pose = Capture(), name = PlayerName(), look = Appearance().Selected.ToString() });
            StartCoroutine(Request(Endpoint, "poll", body, token, (reply, error) => {
                if (current != generation) return;
                polling = false;
                nextPoll = Time.unscaledTime + (error == "" ? 0.2f : 1);
                if (error != "" || !ValidReply(reply) || reply.self != self || reply.code != Code)
                {
                    failures++;
                    Status = "Reconnecting to room…";
                    if (failures >= 3) Leave("Connection lost. Your personal progress is safe; join again.");
                    return;
                }
                failures = 0;
                Status = "Connected · " + reply.players.Length + "/4 players";
                Apply(reply);
            }));
        }

        private static bool ValidReply(RoomReply reply) => reply?.players != null && reply.players.Length >= 1 && reply.players.Length <= 4
            && reply.code != null && reply.code.Length == 12 && reply.code.All(Uri.IsHexDigit)
            && reply.self != null && reply.players.Any(player => player?.id == reply.self)
            && reply.players.Where(player => player != null).Select(player => player.id).Distinct().Count() == reply.players.Length
            && reply.players.All(player => player != null && !string.IsNullOrEmpty(player.id)
                && (player.look == "Girl" || player.look == "Boy") && !float.IsNaN(player.x) && !float.IsInfinity(player.x)
                && Mathf.Abs(player.x) <= 70 && Mathf.Abs(player.z) <= 90 && Mathf.Abs(player.y) <= 20 && Mathf.Abs(player.yaw) <= 360);

        private static bool ValidToken(string value) => value != null && value.Length == 43
            && value.All(letter => char.IsLetterOrDigit(letter) || letter == '_' || letter == '-');

        private void Apply(RoomReply reply)
        {
            players = reply.players;
            var present = new HashSet<string>();
            foreach (var player in players.Where(player => player.id != self))
            {
                present.Add(player.id);
                if (!peers.TryGetValue(player.id, out var peer))
                {
                    peer = new GameObject("Friend · " + player.name).AddComponent<RemotePlayer>();
                    peer.transform.SetParent(transform, false);
                    peers[player.id] = peer;
                }
                if (peer.Look != player.look)
                {
                    var appearance = Appearance();
                    peer.SetVisual(player.look == "Boy" ? appearance.Boy : appearance.Girl, player.name);
                    peer.Look = player.look;
                }
                peer.Move(player);
            }
            foreach (var id in peers.Keys.Where(id => !present.Contains(id)).ToArray())
            {
                Destroy(peers[id].gameObject);
                peers.Remove(id);
            }
        }

        public void Leave(string message = "Left room. Your personal progress is kept.")
        {
            generation++;
            if (token != "") SendLeave(Endpoint, token);
            token = self = Code = "";
            Busy = !Ready;
            polling = false;
            foreach (var peer in peers.Values) if (peer != null) Destroy(peer.gameObject);
            peers.Clear();
            players = Array.Empty<RoomPlayer>();
            Status = message;
        }

        private static UnityWebRequest Post(string endpoint, string action, string json, string bearer)
        {
            var request = new UnityWebRequest(endpoint + "/rooms/" + action, "POST");
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = 5;
            request.redirectLimit = 0;
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("X-ContextSpeak-Client", "native");
            if (bearer != "") request.SetRequestHeader("Authorization", "Bearer " + bearer);
            return request;
        }

        private static IEnumerator Request(string endpoint, string action, string json, string bearer, Action<RoomReply, string> done)
        {
            using var request = Post(endpoint, action, json, bearer);
            yield return request.SendWebRequest();
            RoomReply reply = null;
            try { reply = JsonUtility.FromJson<RoomReply>(request.downloadHandler.text); }
            catch (ArgumentException) { }
            var error = request.result == UnityWebRequest.Result.Success ? "" : reply?.error;
            done(reply, string.IsNullOrEmpty(error) && request.result != UnityWebRequest.Result.Success
                ? "Room service unavailable. Start the companion or check the server address." : error ?? "");
        }

        private static void SendLeave(string endpoint, string bearer)
        {
            // This request owns itself: it may outlive the scene that created it.
            var request = Post(endpoint, "leave", "{}", bearer);
            request.SendWebRequest().completed += _ => request.Dispose();
        }
        private void OnDestroy() { Leave(); }
    }
}
