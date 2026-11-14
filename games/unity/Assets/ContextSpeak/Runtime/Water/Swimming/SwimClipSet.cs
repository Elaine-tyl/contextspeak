using UnityEngine;

namespace ContextSpeak.Life
{
    /// Swimming clips for one character model (matched by AuthoredMotion.ModelName, e.g. "Academy" =
    /// female lead, "Daydream" = male lead). Built by ContextSpeak > Water > Setup Swimming System from
    /// the supplied FBX files; any slot left empty is covered by a clearly labelled temporary
    /// procedural animation (see SwimmingAnimationController.CurrentAnimation).
    [CreateAssetMenu(menuName = "ContextSpeak/Water/Swim Clip Set", fileName = "Swim Clips")]
    public sealed class SwimClipSet : ScriptableObject
    {
        public string ModelName;
        [Header("Authored (from FBX)")]
        [Tooltip("Looping front-crawl/breaststroke cycle, body horizontal. Used for SwimForward/SwimFast/Turn/Dive/UnderwaterSwim/Surface (with posture).")]
        public AnimationClip Swim;
        [Tooltip("Swim to the edge and take hold of it (start of ExitWater / ClimbPoolLadder).")]
        public AnimationClip EdgeReach;
        [Header("Optional — add FBX to fill (otherwise temporary procedural)")]
        public AnimationClip TreadWater;
        public AnimationClip BackFloat;
        public AnimationClip UnderwaterSwim;
        public AnimationClip ClimbLadder;
        public AnimationClip PullOut;
        [Header("Measured by the setup tool")]
        [Tooltip("Yaw (°) that turns the swim clip's head direction to the model's forward.")] public float SwimYaw;
        [Tooltip("Swim clip body direction vs horizontal (°): ~0 = horizontal, as expected.")] public float SwimBodyPitch;
        [Tooltip("Height (m) the ClimbLadder clip climbs per cycle when played as authored (a climbing-down clip descends; it is played backwards to climb up).")]
        public float ClimbRise;
        [Tooltip("True when ClimbLadder was authored climbing DOWN.")] public bool ClimbAuthoredDown = true;
        [TextArea] public string Notes;
    }
}
