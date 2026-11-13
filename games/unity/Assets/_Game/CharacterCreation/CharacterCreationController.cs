using System;
using System.Globalization;
using System.Runtime.InteropServices;
using UnityEngine;

namespace ContextSpeak.Cafe
{
    /// Shared state/validation for native UI Toolkit controls and the accessible browser form.
    public sealed class CharacterCreationController : MonoBehaviour
    {
        public static string ReturnWorld = "ForestSlice";
        public PlayerAppearance Preview;
        public Camera PreviewCamera;
        private int screenWidth, screenHeight;
        public CharacterProfile Profile { get; private set; }
        public bool CanCancel { get; private set; }
        private CharacterCreationView view;
        private string status = "";
        private float rotation;
        private bool busy;
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void CS_State(string json);
#endif
        public static void Open(string returnWorld)
        {
            ReturnWorld = returnWorld;
            WorldScenes.Open("CharacterCreation");
        }

        private void Start()
        {
            CanCancel = PlayerPrefs.HasKey(CharacterProfiles.Key);
            Profile = CharacterProfiles.Draft();
            view = gameObject.AddComponent<CharacterCreationView>();
            view.Command += Command;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
#if UNITY_WEBGL && !UNITY_EDITOR
            WebGLInput.captureAllKeyboardInput = false;
#endif
            Render();
        }

        private void Update()
        {
            if (PreviewCamera == null || (Screen.width == screenWidth && Screen.height == screenHeight)) return;
            screenWidth = Screen.width; screenHeight = Screen.height;
            var compact = screenWidth < 640;
            PreviewCamera.rect = compact ? new Rect(0, 0.56f, 1, 0.44f) : new Rect(0, 0, 1, 1);
            Preview.transform.position = new Vector3(compact ? 0 : -0.65f, 0, 0);
        }

        private void OnDestroy()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            WebGLInput.captureAllKeyboardInput = true;
#endif
        }

        public void OnBrowserCommand(string json)
        {
            try
            {
                var command = JsonUtility.FromJson<CreationCommand>(json);
                if (command != null) Command(command.action, command.text);
            }
            catch (ArgumentException) { }
        }

        public void Command(string action, string text)
        {
            if (busy || Profile == null) return;
            status = "";
            switch (action)
            {
                case "name": Profile.Name = text ?? ""; break;
                case "look":
                    if (!Enum.TryParse(text, out PlayerLook look) || !Enum.IsDefined(typeof(PlayerLook), look)) break;
                    if (Profile.Name == "Emma" || Profile.Name == "Lucas") Profile.Name = look == PlayerLook.Girl ? "Emma" : "Lucas";
                    Profile.Look = look;
                    break;
                case "band": if (int.TryParse(text, out var band)) Profile.Band = Mathf.Clamp(band, 3, 9); break;
                case "height": if (Number(text, out var height)) Profile.Height = Mathf.Clamp(height, 0.95f, 1.05f); break;
                case "build": if (Number(text, out var build)) Profile.Build = Mathf.Clamp(build, 0.95f, 1.05f); break;
                case "rotate": if (Number(text, out var yaw)) rotation = Mathf.Repeat(yaw, 360); break;
                case "turn-left": rotation = Mathf.Repeat(rotation - 45, 360); break;
                case "turn-right": rotation = Mathf.Repeat(rotation + 45, 360); break;
                case "reset-preview": rotation = 0; break;
                case "confirm":
                    Profile.Normalize();
                    if (CharacterProfiles.TrySave(Profile, out status)) EnterWorld();
                    break;
                case "cancel": if (CanCancel) EnterWorld(); break;
            }
            Render();
        }

        private static bool Number(string text, out float number) => float.TryParse(text, NumberStyles.Float,
            CultureInfo.InvariantCulture, out number) && !float.IsNaN(number) && !float.IsInfinity(number);

        private void EnterWorld()
        {
            busy = true;
            status = "Loading your neighborhood…";
            WorldScenes.Open(ReturnWorld, error => { busy = false; status = error; Render(); });
        }

        private void Render()
        {
            Preview.ApplyProfile(Profile);
            Preview.transform.rotation = Quaternion.Euler(0, rotation, 0);
            view.Render(Profile, rotation, CanCancel, busy, status);
#if UNITY_WEBGL && !UNITY_EDITOR
            CS_State(JsonUtility.ToJson(new CreationState {
                profile = Profile, rotation = rotation, canCancel = CanCancel, busy = busy, status = status
            }));
#endif
        }

        [Serializable] private sealed class CreationCommand { public string action, text; }
        [Serializable] private sealed class CreationState
        {
            public string mode = "character";
            public CharacterProfile profile;
            public float rotation;
            public bool canCancel, busy;
            public string status;
        }
    }
}
