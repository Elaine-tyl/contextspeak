using System;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace ContextSpeak.Cafe
{
    /// Adapted from the supplied character-creation prototype; only supported model options are stored.
    [Serializable]
    public sealed class CharacterProfile
    {
        public int Version = 1;
        public bool Created;
        public string Name = "Emma";
        public PlayerLook Look = PlayerLook.Girl;
        public int Band = 5;
        public float Height = 1, Build = 1;

        public CharacterProfile Copy() => JsonUtility.FromJson<CharacterProfile>(JsonUtility.ToJson(this));

        public void Normalize()
        {
            if (!Enum.IsDefined(typeof(PlayerLook), Look)) Look = PlayerLook.Girl;
            Name = new string((Name ?? "").Where(letter => !char.IsControl(letter)).ToArray()).Trim();
            if (Name.Length == 0) Name = Look == PlayerLook.Girl ? "Emma" : "Lucas";
            var text = new StringInfo(Name);
            if (text.LengthInTextElements > 24) Name = text.SubstringByTextElements(0, 24);
            Band = Mathf.Clamp(Band, 3, 9);
            Height = Bounded(Height);
            Build = Bounded(Build);
        }

        private static float Bounded(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 1 : Mathf.Clamp(value, 0.95f, 1.05f);
    }
}
