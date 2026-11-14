using UnityEngine;

namespace ContextSpeak.Life
{
    /// One living animal: its needs change over time, the behaviour state machine decides what it does
    /// next, it walks to the right place in its habitat (water, grass, shade, its herd), does the
    /// activity for a while and then decides again. A calf follows its mother.
    [RequireComponent(typeof(AnimalMovement))]
    public sealed class AnimalController : MonoBehaviour
    {
        public string SpeciesId = "elephant";
        public AnimalHabitat Habitat;
        public AnimalController Follow;                 // calf → mother, herd member → leader
        public Vector3 HomeOffset;                      // preferred part of the habitat (relative to its centre)
        public float HomeRange = 25, SizeScale = 1;
        public bool PlaceholderModel;                   // a temporary box model stands in for the real one
        public AnimalNeeds Needs = new AnimalNeeds();
        public AnimalState State = AnimalState.Idle;
        public float UpdateScale = 1;                   // set by the LOD controller (far animals think less often)
        public AnimalSpecies Species { get; private set; }
        public AnimalMovement Movement { get; private set; }
        public AnimalAnimator Animator { get; private set; }
        public bool Travelling { get; private set; }    // walking to where the current state happens
        private System.Random random;
        private float timer, think;
        private float pendingSeconds;

        private void Awake()
        {
            Species = AnimalCatalog.Find(SpeciesId) ?? AnimalCatalog.All[0];
            Movement = GetComponent<AnimalMovement>();
            Animator = GetComponent<AnimalAnimator>();
            random = new System.Random(GetInstanceID());
            Movement.Habitat = Habitat;
            Movement.TurnSpeed = Species.TurnSpeed;
            Movement.Radius = Species.Radius * SizeScale;
            Movement.Acceleration = Mathf.Max(0.5f, Species.WalkSpeed);
            if (Animator != null)
            {
                Animator.Stride = Species.Stride * SizeScale; Animator.LegSwing = Species.LegSwing; Animator.HeadDrop = Species.HeadDrop; Animator.NeckLift = Species.NeckLift;
                Animator.WalkSpeed = Species.WalkSpeed; Animator.Height = Species.Height * SizeScale; Animator.RigidLegs = Species.RigidLegs; Animator.Browser = Species.Browser;
            }
            // Start each animal at a different point of its day.
            Needs.Hunger = (float)random.NextDouble() * 0.5f;
            Needs.Thirst = (float)random.NextDouble() * 0.5f;
            Needs.Tiredness = (float)random.NextDouble() * 0.3f;
            timer = (float)random.NextDouble() * 4;
        }

        private void OnEnable() { if (Habitat != null && !Habitat.Animals.Contains(this)) Habitat.Animals.Add(this); }
        private void OnDisable() { if (Habitat != null) Habitat.Animals.Remove(this); }

        private Vector3 Home => Habitat != null ? Habitat.transform.position + HomeOffset : transform.position;

        private bool HerdFar(out Vector3 where)
        {
            where = transform.position;
            if (Follow != null) { where = Follow.transform.position; return Vector3.Distance(where, transform.position) > Species.Radius * SizeScale * 4 + 3; }
            if (!Species.Herd || Habitat == null) return false;
            var sum = Vector3.zero; var count = 0;
            foreach (var other in Habitat.Animals)
                if (other != null && other != this && other.SpeciesId == SpeciesId) { sum += other.transform.position; count++; }
            if (count == 0) return false;
            where = sum / count;
            return Vector3.Distance(where, transform.position) > 14 + Species.Radius * 4;
        }

        private void Update()
        {
            var dt = Time.deltaTime;
            Movement.Tick(dt);
            if (Animator != null) { Animator.Speed = Movement.Speed; Animator.State = Travelling ? (Movement.Speed > Species.WalkSpeed * 1.3f ? AnimalState.Running : AnimalState.Walking) : State; }
            if ((think -= dt) > 0) return;
            var step = 0.25f * UpdateScale;
            think = step;

            var herdFar = HerdFar(out var herd);
            var disturbed = SkyState.Rain > 0.7f && SkyState.WindSpeed > 9;      // storms unsettle the animals
            Needs.Tick(step / 60f, Species, Travelling ? AnimalState.Walking : State, !herdFar, disturbed);

            if (Travelling)
            {
                if (!Movement.Arrived) return;
                Travelling = false;
                timer = pendingSeconds * (0.7f + (float)random.NextDouble() * 0.6f);
                if (State == AnimalState.Drinking && Habitat != null)
                {
                    var water = Habitat.Nearest(Habitat.Water, transform.position);
                    if (water != null) Movement.Face(water.position);
                }
                return;
            }
            // A calf drops what it is doing when its mother walks away.
            if (Follow != null && herdFar && State != AnimalState.Sleeping) timer = 0;
            if ((timer -= step) > 0) return;
            Decide(herdFar, herd);
        }

        private void Decide(bool herdFar, Vector3 herd)
        {
            var inside = Habitat == null || Habitat.Contains(transform.position, Habitat.Margin * 0.5f);
            var (state, seconds) = AnimalBehaviorStateMachine.Choose(Species, Needs, SkyState.Hour, inside, herdFar, (float)random.NextDouble());
            State = state;
            pendingSeconds = seconds;
            var here = transform.position;
            var speed = Species.WalkSpeed * (0.85f + (float)random.NextDouble() * 0.3f);
            Vector3? destination = null;
            if (Habitat != null)
            {
                var slot = Mathf.Abs(GetInstanceID());
                switch (state)
                {
                    case AnimalState.Drinking:
                        var water = Habitat.Nearest(Habitat.Water, here);
                        if (water != null) destination = Habitat.Slot(water, slot, Species.Radius * SizeScale + 0.6f);
                        break;
                    case AnimalState.Eating:
                        var places = Species.Browser ? Habitat.Browse : Habitat.Food;
                        var food = places.Length > 0 ? places[random.Next(places.Length)] : null;
                        destination = food != null && (Species.Browser || random.NextDouble() < 0.5)
                            ? Habitat.Slot(food, slot, Species.Radius * SizeScale + (Species.Browser ? 1.2f : 0.5f))
                            : Habitat.RandomPoint(random, here, 9);            // grazers also eat wherever there is grass
                        break;
                    case AnimalState.Resting: case AnimalState.Sleeping:
                        var rest = Habitat.Nearest(Habitat.Rest, here);
                        if (rest != null) destination = Habitat.Slot(rest, slot, Species.Radius * SizeScale + 1);
                        break;
                    case AnimalState.Socializing:
                        destination = Habitat.Clamp(herd + (here - herd).normalized * (Species.Radius * SizeScale * 2 + 1.5f));
                        break;
                    case AnimalState.Running:
                        destination = Habitat.RandomPoint(random, Home, HomeRange); speed = Species.RunSpeed;
                        break;
                    case AnimalState.Playing:
                        destination = Habitat.RandomPoint(random, here, 8); speed = Mathf.Lerp(Species.WalkSpeed, Species.RunSpeed, 0.5f);
                        break;
                    case AnimalState.Walking:
                        destination = Habitat.RandomPoint(random, Vector3.Lerp(here, Home, 0.5f), HomeRange);
                        break;
                    case AnimalState.ReturningToHabitat:
                        destination = Habitat.Clamp(Home);
                        break;
                }
                if (Follow != null && herdFar) { destination = Habitat.Clamp(herd + (here - herd).normalized * (Species.Radius * 2 + 1)); speed = Species.WalkSpeed * 1.25f; }
            }
            if (destination.HasValue && Vector3.Distance(destination.Value, here) > 0.8f)
            {
                Movement.GoTo(destination.Value, speed);
                Travelling = true;
            }
            else { Movement.Stop(); timer = seconds; }
        }

        /// What a visitor would say the animal is doing (used by the information panel and guides).
        public string Activity => Travelling ? (State == AnimalState.Drinking ? "walking to the water" : State == AnimalState.Eating ? "looking for food" : "walking")
            : State == AnimalState.Eating ? (Species.Browser ? "eating leaves" : "grazing") : State == AnimalState.Drinking ? "drinking" : State == AnimalState.Resting ? "resting"
            : State == AnimalState.Sleeping ? "sleeping" : State == AnimalState.LookingAround ? "looking around" : State == AnimalState.Socializing ? "staying close to the herd"
            : State == AnimalState.Playing ? "playing" : State == AnimalState.Grooming ? "grooming" : "standing still";
    }
}
