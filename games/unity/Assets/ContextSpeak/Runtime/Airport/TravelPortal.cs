using UnityEngine;
using UnityEngine.SceneManagement;

namespace ContextSpeak.Life
{
    /// A door between the airport scenes (jet bridge → cabin, aircraft door → arrival hall).
    /// It only opens when the journey has reached RequiredStage. Does not overwrite the city return point.
    public sealed class TravelPortal : InteractableObject
    {
        public string TargetScene = "AircraftCabin";
        public TravelStage RequiredStage = TravelStage.Boarded;
        public string Locked = "Not yet.";
        public string Gate = "";     // jet bridges: only your own flight's gate
        private bool Open => TravelQuest.Booking.stage >= (int)RequiredStage && (Gate == "" || TravelQuest.Booking.gate == Gate);
        public override string Label => Open ? Prompt : Prompt + " — " + (Gate != "" && TravelQuest.Booking.gate != Gate ? "not your flight" : Locked);

        public override void Interact(InteractionManager manager)
        {
            if (Gate != "" && TravelQuest.Booking.gate != Gate) { LifeUI.Toast("This aircraft isn't your flight — yours leaves from gate " + TravelQuest.Booking.gate + "."); return; }
            if (TravelQuest.Booking.stage < (int)RequiredStage) { LifeUI.Toast(Locked); return; }
            if (!Application.CanStreamedLevelBeLoaded(TargetScene)) { LifeUI.Toast(TargetScene + " is not built yet. Run ContextSpeak > Build International Airport."); return; }
            SaveManager.Save();
            SceneManager.LoadScene(TargetScene);
        }
    }
}
