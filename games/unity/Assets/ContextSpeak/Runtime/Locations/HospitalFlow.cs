using GUI = ContextSpeak.Life.ToolkitGui;
using GUILayout = ContextSpeak.Life.ToolkitLayout;
using GUILayoutUtility = ContextSpeak.Life.ToolkitLayoutUtility;
using Screen = ContextSpeak.Life.ToolkitScreen;
using System.Linq;
using UnityEngine;

namespace ContextSpeak.Life
{
    /// City Hospital outpatients: reception → registration form → appointment choice → queue ticket
    /// → wait to be called → nurse → doctor (fictional practice case) → summary → pharmacy →
    /// instructions → payment → leave. All medical content is fictional language practice.
    public sealed class HospitalFlow : LocationFlow
    {
        public enum Stage { Arrive, Registration, Appointment, Ticket, Waiting, Called, ToConsultation, Case, Consultation, Summary, ToPharmacy, Instructions, Paying, Done }
        public Stage Phase { get; private set; } = Stage.Arrive;
        public override string QuestId => "hospital-visit";
        public override string QuestTitle => "See a doctor at City Hospital";
        public TextMesh QueueBoard;
        public DoorController ConsultationDoor;
        public Transform WaitingArea, ConsultationRoom;
        public float WaitSeconds = 14f;

        public string Ticket { get; private set; } = "";
        public int Serving { get; private set; }
        private int number;
        private float nextCall;
        private string patientName = "", birth = "14/03/1998", phone = "077 123 456", reason = "See a doctor";
        private bool walkIn = true;
        private int slot;
        private int practiceCase;
        private static readonly string[] Slots = { "Walk-in now (≈20 min)", "2:30 pm today", "4:15 pm today" };
        private static readonly string[] Reasons = { "See a doctor", "Follow-up visit", "Prescription question", "Test results" };
        private static readonly (string title, string card)[] Cases =
        {
            ("Sore throat and fever", "Started 2 days ago · getting worse · pain 6/10 when swallowing · no allergies"),
            ("Twisted ankle", "Fell while running yesterday · swollen · pain 5/10 when walking · no allergies"),
            ("Headache and tiredness", "For a week · worse in the afternoon · pain 4/10 · not taking medicine"),
        };
        private Vector2 scroll;

        protected override void Start()
        {
            base.Start();
            patientName = SaveManager.PlayerName;
            if (ConsultationDoor != null) ConsultationDoor.Locked = true;
            number = Random.Range(14, 30);
            Serving = number - 4;
            Step(0, "Talk to the receptionist at the front desk.");
            UpdateBoard();
        }

        public override void Talk(NPCController npc)
        {
            if (npc == null) return;
            switch (npc.Key)
            {
                case "receptionist" when Phase == Stage.Arrive:
                    Run("Hospital Registration", npc, () => Open(Stage.Registration));
                    return;
                case "receptionist" when Phase == Stage.Waiting || Phase == Stage.Ticket:
                    LifeUI.Toast(npc.DisplayName + ": Please take a seat in the waiting area. Your number is " + Ticket + ".");
                    return;
                case "nurse" when Phase == Stage.Called:
                    Run("Nurse Call", npc, () =>
                    {
                        if (ConsultationDoor != null) ConsultationDoor.Locked = false;
                        Phase = Stage.ToConsultation;
                        Step(4, "Go into Consultation Room 2 and talk to the doctor.");
                        if (ConsultationRoom != null) npc.GoTo(ConsultationRoom.position + ConsultationRoom.right * 1.2f, () => npc.ReturnHome());
                    });
                    return;
                case "doctor" when Phase == Stage.ToConsultation:
                    Open(Stage.Case);
                    return;
                case "pharmacist" when Phase == Stage.ToPharmacy:
                    Run("Pharmacy Pickup", npc, () => Open(Stage.Instructions));
                    return;
                case "doctor":
                case "pharmacist":
                    LifeUI.Toast(npc.DisplayName + ": " + (Phase < Stage.ToConsultation ? "Please register at reception first." : "Have a good day, and get well soon."));
                    return;
                default:
                    Run("Ask for Directions", npc, null);
                    return;
            }
        }

        private void Open(Stage stage)
        {
            Phase = stage;
            ownsPanel = true;
            LifeUI.SetPanelOpen(this, true);
        }

        private void Close(Stage next)
        {
            Phase = next;
            ownsPanel = false;
            LifeUI.SetPanelOpen(this, false);
        }

        private void Update()
        {
            var seat = InteractionManager.Instance != null ? InteractionManager.Instance.Seat : null;
            if (Phase == Stage.Waiting && seat != null && seat.Group == "waiting" && Time.time >= nextCall)
            {
                Serving++;
                nextCall = Time.time + WaitSeconds / 4f;
                UpdateBoard();
                if (Serving >= number)
                {
                    Phase = Stage.Called;
                    var line = "Number " + Ticket + ", please. " + patientName + ", please come to the nurse's station.";
                    LifeUI.Toast("🔔 " + line);
                    if (ScenarioManager.Instance != null) ScenarioManager.Instance.Speech.Speak(line);
                    Step(3, "Your number was called. Stand up and talk to the nurse.");
                    var nurse = Npc("nurse");
                    if (nurse != null && WaitingArea != null) nurse.GoTo(WaitingArea.position + WaitingArea.forward * 1.5f);
                }
            }
        }

        private void UpdateBoard()
        {
            if (QueueBoard != null) QueueBoard.text = "NOW SERVING\nA-" + Serving.ToString("000") + (Ticket != "" ? "\nYOUR TICKET " + Ticket : "");
        }

        private bool ownsPanel;
        private void OnDisable() { LifeUI.SetPanelOpen(this, false); }
        private void OnEnable() { ToolkitView.Attach(this, DrawToolkit); if (ownsPanel) LifeUI.SetPanelOpen(this, true); }

        private void DrawToolkit()
        {
            if (!LifeUI.PanelOpen || !(Phase == Stage.Registration || Phase == Stage.Appointment || Phase == Stage.Ticket ||
                Phase == Stage.Case || Phase == Stage.Summary || Phase == Stage.Instructions)) return;
            LifeUI.Begin(out var width, out var height);
            var box = new Rect(width / 2 - 340, height / 2 - 300, 680, 600);
            GUI.Box(box, GUIContent.none, LifeUI.Panel);
            GUILayout.BeginArea(new Rect(box.x + 24, box.y + 18, box.width - 48, box.height - 36));
            switch (Phase)
            {
                case Stage.Registration: RegistrationPanel(); break;
                case Stage.Appointment: AppointmentPanel(); break;
                case Stage.Ticket: QueuePanel(); break;
                case Stage.Case: SymptomDescriptionPanel(); break;
                case Stage.Summary: ConsultationPanel(); break;
                case Stage.Instructions: PharmacyPanel(); break;
            }
            GUILayout.EndArea();
        }

        private void RegistrationPanel()
        {
            GUILayout.Label("Patient Registration Form", LifeUI.Title);
            GUILayout.Label("Check the details you just gave the receptionist. (Practice data only.)", LifeUI.Body);
            patientName = Field("Full name", patientName);
            birth = Field("Date of birth", birth);
            phone = Field("Contact number", phone);
            GUILayout.Label("Reason for visit", LifeUI.Body);
            var index = System.Array.IndexOf(Reasons, reason);
            reason = Reasons[GUILayout.SelectionGrid(Mathf.Max(0, index), Reasons, 2, LifeUI.Small)];
            GUILayout.FlexibleSpace();
            GUI.enabled = patientName.Trim() != "" && birth.Trim() != "";
            if (GUILayout.Button("Submit form", LifeUI.Button)) { Phase = Stage.Appointment; Step(1, "Choose an appointment time."); }
            GUI.enabled = true;
        }

        private static string Field(string label, string value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, LifeUI.Body, GUILayout.Width(180));
            value = GUILayout.TextField(value, 60, GUILayout.Height(32));
            GUILayout.EndHorizontal();
            return value;
        }

        private void AppointmentPanel()
        {
            GUILayout.Label("Appointment", LifeUI.Title);
            GUILayout.Label("You told the receptionist whether you want to wait now or book a time.", LifeUI.Body);
            slot = GUILayout.SelectionGrid(slot, Slots, 1, LifeUI.Button);
            walkIn = slot == 0;
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Confirm", LifeUI.Button))
            {
                if (!walkIn) LifeUI.Toast("Booked for " + Slots[slot] + " — for practice, the clinic can see you early today.");
                Ticket = "A-" + number.ToString("000");
                UpdateBoard();
                Phase = Stage.Ticket;
            }
        }

        private void QueuePanel()
        {
            GUILayout.Label("Queue Ticket", LifeUI.Title);
            GUILayout.Label("<size=64><b>" + Ticket + "</b></size>", LifeUI.Heading);
            GUILayout.Label("Now serving A-" + Serving.ToString("000") + ". Please sit in the waiting area and watch the board. " +
                            "A nurse will call your number.", LifeUI.Body);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Take ticket", LifeUI.Button))
            {
                InventoryManager.Add("hospital-ticket", "Queue ticket " + Ticket);
                Close(Stage.Waiting);
                nextCall = Time.time + WaitSeconds / 4f;
                Step(2, "Sit down in the waiting area (press E at a chair) until " + Ticket + " is called.");
            }
        }

        private void SymptomDescriptionPanel()
        {
            GUILayout.Label("Practice Case Card", LifeUI.Title);
            GUILayout.Label("Choose a fictional case to describe to the doctor. Use your own words — the card is just ideas.", LifeUI.Body);
            practiceCase = GUILayout.SelectionGrid(practiceCase, Cases.Select(item => item.title).ToArray(), 1, LifeUI.Button);
            GUILayout.Label(Cases[practiceCase].card, LifeUI.Heading);
            GUILayout.Label("Useful: “I've had … for …”, “It hurts when …”, “It's getting better / worse.”", LifeUI.Body);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Start the consultation", LifeUI.Button))
            {
                Close(Stage.Consultation);
                Step(5, "Describe your symptoms to Doctor Patel.");
                Run("Doctor Consultation", Npc("doctor"), () => Open(Stage.Summary));
            }
        }

        private void ConsultationPanel()
        {
            GUILayout.Label("Consultation Summary", LifeUI.Title);
            GUILayout.Label("<b>Patient:</b> " + patientName + "    <b>Reason:</b> " + reason, LifeUI.Body);
            GUILayout.Label("<b>Practice case:</b> " + Cases[practiceCase].title + "\n" + Cases[practiceCase].card, LifeUI.Body);
            GUILayout.Label("<b>Doctor's advice (fictional):</b> rest, drink fluids, collect the prescription at the pharmacy, come back if it isn't better in five days.", LifeUI.Body);
            GUILayout.Label("This is a language-learning simulation, not medical advice. If you are unwell, contact a real health professional.", LifeUI.Warning);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Go to the pharmacy", LifeUI.Button))
            {
                Close(Stage.ToPharmacy);
                InventoryManager.Add("prescription", "Prescription (practice)");
                Step(6, "Collect your prescription at the pharmacy counter.");
            }
        }

        private void PharmacyPanel()
        {
            GUILayout.Label("Medicine Label (fictional)", LifeUI.Title);
            GUILayout.BeginVertical(LifeUI.Paper);
            GUILayout.Label("<b>PRACTICE-CILLIN 250</b>  ·  for " + patientName, LifeUI.Dark);
            GUILayout.Label("Take ONE tablet THREE times a day AFTER meals.\nFinish the full 5-day course.\nDo not take with other medicine without asking a pharmacist.", LifeUI.Dark);
            GUILayout.EndVertical();
            GUILayout.Label("Not a real medicine — vocabulary practice only.", LifeUI.Warning);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Pay $18.50", LifeUI.Button))
            {
                Close(Stage.Paying);
                PaymentController.Instance.Show("City Hospital Pharmacy", "Prescription (practice) × 1", 1850, () =>
                {
                    InventoryManager.Remove("prescription");
                    InventoryManager.Add("medicine-bag", "Pharmacy bag (practice)");
                    InventoryManager.Remove("hospital-ticket");
                    Phase = Stage.Done;
                    Objective = "Visit complete! Leave through the main entrance.";
                    QuestManager.Complete(QuestId, 60, Objective);
                    LifeUI.Toast("Hospital visit complete · +60 XP");
                }, () => Open(Stage.Instructions));
            }
        }

        public void SetForTests(Stage stage) => Phase = stage;
    }
}
