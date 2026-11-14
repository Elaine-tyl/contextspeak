using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ContextSpeak.Life
{
    /// LivingNPCController: one person with a life. Each second the utility AI picks a goal (the routine
    /// from NPCDailySchedule, or eating / resting / sheltering / socialising when needs say so, with
    /// hysteresis), and a program — a sequence of real steps tied to objects — carries it out:
    /// walk to the place through its door, reserve a spot, do the action for a while, put things back.
    /// Programs: barista and cashier shifts, café customer, shopping, tram commute, school run, home life
    /// (cook, eat, sofa, bed), tourist exploring, errands, sheltering from rain, meeting friends.
    [RequireComponent(typeof(NPCNavigationController))]
    public sealed class LivingNPC : MonoBehaviour
    {
        public static readonly List<LivingNPC> All = new List<LivingNPC>();
        public NPCProfile Profile;
        public NPCNeeds Needs = new NPCNeeds();
        public NPCUtilityAI Brain = new NPCUtilityAI();

        public NPCNavigationController Navigation { get; private set; }
        public NPCAnimationController Animation { get; private set; }
        public NPCController Legacy { get; private set; }
        public int Tier { get; set; } = 1;
        public string Activity { get; private set; } = "";
        public string Step { get; private set; } = "starting";
        public NPCGoal Goal { get; private set; }
        public LivingPlace Place { get; private set; }
        public LivingPlace Target { get; private set; }
        public bool Interruptible { get; private set; }
        public bool InConversation { get; set; }
        public bool Riding { get; private set; }
        public bool Hidden { get; private set; }
        public string Id => Profile != null ? Profile.Id : name;
        public string DisplayName => Profile != null ? Profile.DisplayName : name;
        public System.Random Random { get; private set; }
        public float PersonalSpeed { get; private set; } = 1;
        public int Served { get; private set; }

        private Coroutine program;
        private readonly List<ActivitySpot> held = new List<ActivitySpot>();
        private ServicePoint queued;
        private float think, sinceGoal;
        private bool enteredOnRed;
        private LivingCrossing lastCrossing;

        private void Awake()
        {
            Navigation = GetComponent<NPCNavigationController>();
            Animation = GetComponent<NPCAnimationController>();
            if (Animation == null) Animation = gameObject.AddComponent<NPCAnimationController>();
            Legacy = GetComponent<NPCController>();
            var hash = Profile != null ? Profile.Id.GetHashCode() : GetInstanceID();
            Random = new System.Random(hash);
            PersonalSpeed = 0.9f + (float)Random.NextDouble() * 0.22f;
            if (Profile != null && Profile.Personality.Busyness > 0.6f) PersonalSpeed *= 1.06f;
            if (Profile != null && (Profile.Age == AgeGroup.Senior)) PersonalSpeed *= 0.82f;
        }

        private void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        private void OnDisable() { All.Remove(this); ReleaseAll(); }

        // ------------------------------------------------------------------ main loop (driven by NPCLifeSimulationManager)

        public void Simulate(float dt, float gameHours)
        {
            Needs.Tick(gameHours, Activity);
            think -= dt;
            if (think <= 0 && !InConversation)
            {
                think = 1f;
                var block = Profile != null ? Profile.Schedule.At(LivingClock.Hour) : null;
                var working = block != null && (block.activity == "Work" || block.activity == "ServeCustomers" || block.activity == "Teach" || block.activity == "School" || block.activity == "Study");
                var outdoors = Place == null || !(Place.Indoor || Place.Shelter);
                var goal = Brain.Evaluate(block, Needs, Profile != null ? Profile.Personality : new NPCPersonality(), working, SkyState.Raining, outdoors && !Riding, 1);
                if (goal.Activity != Goal.Activity || goal.Location != Goal.Location || program == null) Begin(goal);
            }
            if (Tier < 3 && !Riding) Navigation.Tick(dt, new Vector3(PersonalSpeed, 0, 0));
            Animation.Speed = Navigation.Speed;
            if (Tier < 3) Animation.Tick(dt, Navigation.Speed, Tier == 1);
            Place = LivingPlace.At(transform.position) ?? (Riding ? Place : null);
            // Crossing audit: did we step on the zebra while cars had green?
            var crossing = LivingCrossing.All.Count > 0 ? LivingCrossing.All.Find(c => c != null && c.OnCrossing(transform.position)) : null;
            if (crossing != lastCrossing) { enteredOnRed = crossing != null && !crossing.WalkAllowed && crossing.PhaseTime > crossing.Clearance + 1.5f; lastCrossing = crossing; }
        }

        public bool EnteredCrossingOnRed(LivingCrossing crossing) => lastCrossing == crossing && enteredOnRed;

        private void LateUpdate()
        {
            if (Tier < 3) Animation.Late(Time.deltaTime, Tier == 1);
        }

        public void Begin(NPCGoal goal)
        {
            Goal = goal;
            sinceGoal = 0;
            if (program != null) StopCoroutine(program);
            Cleanup();
            Activity = goal.Activity;
            program = StartCoroutine(Run(goal));
        }

        /// Pauses the current program for something else (greeting a friend, an event), then resumes the goal.
        public void Interrupt(IEnumerator routine)
        {
            if (program != null) StopCoroutine(program);
            Cleanup();
            program = StartCoroutine(Then(routine));
        }

        private IEnumerator Then(IEnumerator routine)
        {
            yield return routine;
            program = StartCoroutine(Run(Goal));
        }

        private void Cleanup()
        {
            ReleaseAll();
            if (queued != null) { queued.Leave(this); queued = null; }
            Animation.Play(NPCAction.None);
            Animation.LookAt(null);
            Interruptible = false;
            if (Riding) StopRiding(transform.position);
            Navigation.Jogging = false;
            SetHidden(false);
        }

        private void ReleaseAll() { foreach (var s in held) if (s != null) s.Release(this); held.Clear(); }

        // ------------------------------------------------------------------ primitives

        public void Say(string text, float seconds = 3.2f) => SpeechBubbles.Show(this, text, seconds);

        private IEnumerator Wait(float seconds) { var t = 0f; while (t < seconds) { t += Time.deltaTime; yield return null; } }

        private IEnumerator Act(NPCAction action, float seconds, float seat = 0)
        {
            Animation.Play(action, seat);
            yield return Wait(seconds);
            Animation.Play(NPCAction.None);
        }

        private bool Visible(Vector3 p)
        {
            var camera = Camera.main;
            if (camera == null) return false;
            var v = camera.WorldToViewportPoint(p + Vector3.up);
            return v.z > 0 && v.z < 120 && v.x > -0.1f && v.x < 1.1f && v.y > -0.1f && v.y < 1.1f;
        }

        /// Walk somewhere. Background NPCs far from view travel "logically" (time passes, then they are
        /// there) — but never jump while either end is on screen.
        private IEnumerator GoTo(Vector3 destination, float stop = 0.25f, float timeout = 150, bool interruptible = true)
        {
            Interruptible = interruptible;
            Step = "walking";
            var t = 0f;
            if (Tier == 3 && !Visible(transform.position) && !Visible(destination))
            {
                var travel = Vector3.Distance(transform.position, destination) / Mathf.Max(0.5f, Navigation.WalkSpeed * PersonalSpeed);
                while (t < travel) { t += Time.deltaTime; if (Tier < 3) break; yield return null; }
                if (Tier == 3 && !Visible(destination) && !Visible(transform.position))
                {
                    Navigation.Enable(true);
                    if (Navigation.OnMesh) Navigation.Agent.Warp(destination); else transform.position = destination;
                    Navigation.Stop();
                    Interruptible = false;
                    yield break;
                }
            }
            Navigation.Enable(true);
            Navigation.Go(destination, stop);
            yield return null;
            while (!Navigation.Arrived && t < timeout)
            {
                t += Time.deltaTime;
                if (Navigation.WaitingAtCrossing) Step = "waiting at the crossing";
                else if (Step != "walking") Step = "walking";
                yield return null;
            }
            Navigation.Stop();
            Interruptible = false;
        }

        private IEnumerator Face(Vector3 direction, float seconds = 0.5f)
        {
            var t = 0f;
            while (t < seconds) { Navigation.Face(direction, Time.deltaTime); t += Time.deltaTime; yield return null; }
        }

        private IEnumerator Enter(LivingPlace place)
        {
            if (place == null) yield break;
            Target = place;
            if (place.Contains(transform.position)) yield break;
            if (place.Entrance != null) yield return GoTo(place.Door, 0.4f);
            if (place.Inside != null) yield return GoTo(place.InsidePoint, 0.35f, 30, false);
        }

        private IEnumerator Leave(LivingPlace place)
        {
            if (place == null || !place.Contains(transform.position)) yield break;
            if (place.Inside != null) yield return GoTo(place.InsidePoint, 0.4f, 30, false);
            if (place.Entrance != null) yield return GoTo(place.Door, 0.5f, 30, false);
        }

        /// Reserve a spot, walk to it, face it, do the action for `seconds`, release it.
        private IEnumerator Use(LivingPlace place, SpotKind kind, NPCAction action, float seconds, string group = null, bool release = true)
        {
            var spot = place != null ? place.Reserve(kind, this, transform.position, group) : null;
            if (spot == null) { yield return Act(NPCAction.LookAround, 2); yield break; }
            held.Add(spot);
            Step = kind + " · " + action;
            yield return GoTo(spot.transform.position, 0.12f, 60, false);
            yield return Face(spot.transform.forward, 0.4f);
            if (spot.Sitting) yield return Act(NPCAction.Sit, 0.6f, spot.SeatHeight);
            Animation.Play(action, spot.Sitting || action == NPCAction.Lie ? spot.SeatHeight : 0);
            var t = 0f;
            while (t < seconds) { t += Time.deltaTime; Navigation.Face(spot.transform.forward, Time.deltaTime); yield return null; }
            Animation.Play(NPCAction.None);
            if (release) { spot.Release(this); held.Remove(spot); }
        }

        private void SetHidden(bool hidden)
        {
            if (Hidden == hidden) return;
            Hidden = hidden;
            foreach (var r in GetComponentsInChildren<Renderer>(true)) if (!r.name.StartsWith("Prop")) r.enabled = !hidden;
        }

        // ------------------------------------------------------------------ programs

        private LivingPlace Resolve(string location)
        {
            var seed = Random.Next(1000);
            return LivingPlace.Find(location, Profile != null ? Profile.Home : null, Profile != null ? Profile.Work : null, seed);
        }

        private IEnumerator Run(NPCGoal goal)
        {
            yield return null;
            var activity = goal.Activity;
            var place = Resolve(goal.Location);
            var occupation = Profile != null ? Profile.Occupation.ToLowerInvariant() : "";
            switch (activity)
            {
                case "Sleep": yield return HomeLife(place, sleep: true); break;
                case "Breakfast": case "Dinner": case "Eat": case "Lunch":
                    if (place != null && place.Kind == PlaceKind.Home) yield return HomeLife(place, eat: true);
                    else yield return CafeCustomer(place ?? Resolve("café"), eat: true);
                    break;
                case "Relax": yield return HomeLife(place ?? Resolve("home"), relax: true); break;
                case "Commute": yield return Commute(Resolve(Profile != null && Profile.Work != "" ? Profile.Work : "street")); break;
                case "ServeCustomers": case "Work":
                    if (place != null && place.Service != null && (occupation.Contains("barista") || occupation.Contains("cashier") || occupation.Contains("waiter"))) yield return Shift(place);
                    else yield return Stay(place, NPCAction.Type);
                    break;
                case "Break": yield return Break(place ?? Resolve("staff")); break;
                case "LeaveWork": yield return LeaveWork(); break;
                case "Socialize": yield return Socialize(place); break;
                case "Shop": yield return Shop(place ?? Resolve("supermarket")); break;
                case "Coffee": case "CafeVisit": yield return CafeCustomer(place ?? Resolve("café"), eat: false); break;
                case "School": case "Study": case "Teach": yield return School(place ?? Resolve("school")); break;
                case "Explore": yield return Explore(place); break;
                case "Shelter": yield return Shelter(); break;
                case "Rest": yield return Rest(); break;
                default: yield return Wander(place); break;
            }
            program = null;
        }

        private IEnumerator HomeLife(LivingPlace home, bool sleep = false, bool eat = false, bool relax = false)
        {
            if (home == null) { yield return Wander(null); yield break; }
            yield return Enter(home);
            if (sleep)
            {
                yield return Use(home, SpotKind.Bed, NPCAction.Lie, 9999, release: false);
                yield break;
            }
            if (eat)
            {
                if (home.Count(SpotKind.Stove) > 0 && Random.NextDouble() < 0.6) yield return Use(home, SpotKind.Stove, NPCAction.Cook, 18 + Random.Next(14));
                yield return Use(home, SpotKind.DiningSeat, NPCAction.Eat, 30 + Random.Next(30));
                Needs.Satisfy("Hunger", 0.7f);
            }
            while (true)
            {
                var roll = Random.NextDouble();
                if (roll < 0.35) yield return Use(home, SpotKind.Sofa, Random.NextDouble() < 0.5 ? NPCAction.Phone : NPCAction.Read, 25 + Random.Next(30));
                else if (roll < 0.6) yield return Use(home, SpotKind.Sofa, NPCAction.Sit, 30 + Random.Next(40));       // watching TV
                else if (roll < 0.75) yield return Use(home, SpotKind.Sink, NPCAction.Wipe, 12 + Random.Next(10));     // tidy up
                else if (roll < 0.85) yield return Act(NPCAction.Stretch, 3);
                else yield return Use(home, SpotKind.DiningSeat, NPCAction.Drink, 20 + Random.Next(20));
            }
        }

        private IEnumerator Stay(LivingPlace place, NPCAction action)
        {
            if (place == null) { yield return Wander(null); yield break; }
            yield return Enter(place);
            if (!place.InteriorVisible && place.Indoor) { Step = "inside " + place.Id; SetHidden(true); while (true) yield return Wait(5); }
            while (true) yield return Use(place, SpotKind.Desk, action, 40 + Random.Next(30));
        }

        private IEnumerator Commute(LivingPlace work)
        {
            if (Place != null) yield return Leave(Place);
            var from = NearestStop(transform.position);
            var to = work != null ? NearestStop(work.Door) : null;
            var tram = LivingTram.All.Count > 0 ? LivingTram.All[0] : null;
            if (tram != null && from != null && to != null && from != to && work != null && Vector3.Distance(transform.position, work.Door) > 35)
                yield return Tram(tram, from, to);
            if (work != null) { yield return Enter(work); yield return Wander(work); }
        }

        private LivingPlace NearestStop(Vector3 p)
        {
            LivingPlace best = null; var d = float.MaxValue;
            foreach (var place in LivingPlace.All)
                if (place != null && place.Kind == PlaceKind.TramStop) { var dd = (place.Door - p).sqrMagnitude; if (dd < d) { d = dd; best = place; } }
            return best;
        }

        public IEnumerator Tram(LivingTram tram, LivingPlace from, LivingPlace to)
        {
            Step = "walking to the tram stop";
            var wait = from.Reserve(SpotKind.Wait, this, transform.position);
            if (wait != null) held.Add(wait);
            yield return GoTo(wait != null ? wait.transform.position : from.Door, 0.2f);
            Step = "waiting for the tram";
            var t = 0f;
            while (!(tram.Dwelling && tram.StopId == from.Id) && t < 240)
            {
                t += Time.deltaTime;
                if (Random.NextDouble() < 0.002) yield return Act(Profile != null && Profile.Personality.Distraction > 0.5f ? NPCAction.Phone : NPCAction.LookAround, 4);
                yield return null;
            }
            if (!(tram.Dwelling && tram.StopId == from.Id)) yield break;
            var seat = tram.ReserveSeat(this);
            if (seat == null) { Say("Too full — I'll wait for the next one."); yield break; }
            if (wait != null) { wait.Release(this); held.Remove(wait); }
            held.Add(seat);
            Step = "boarding the tram";
            yield return GoTo(tram.NearestDoor(transform.position), 0.25f, 20, false);
            if (!tram.Dwelling) { seat.Release(this); yield break; }
            // On board: carried by the tram (parented), moving smoothly to the seat.
            Navigation.Enable(false);
            Riding = true;
            transform.SetParent(tram.transform, true);
            yield return Glide(seat.transform, 1.1f);
            tram.CountBoarding(true);
            Animation.Play(NPCAction.Sit, seat.SeatHeight);
            Step = "riding the tram";
            while (!(tram.Dwelling && tram.StopId == to.Id)) yield return null;
            Animation.Play(NPCAction.None);
            Step = "getting off";
            var door = tram.NearestDoor(transform.position);
            yield return GlideTo(door, 1f);
            var outside = door + (to.Door - door).normalized * 1.2f;
            outside.y = to.Door.y;
            StopRiding(outside);
            tram.CountBoarding(false);
            seat.Release(this); held.Remove(seat);
        }

        private IEnumerator Glide(Transform target, float seconds)
        {
            var start = transform.position; var startRotation = transform.rotation; var t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                var k = Mathf.SmoothStep(0, 1, t / seconds);
                transform.SetPositionAndRotation(Vector3.Lerp(start, target.position, k), Quaternion.Slerp(startRotation, target.rotation, k));
                yield return null;
            }
        }

        private IEnumerator GlideTo(Vector3 target, float seconds)
        {
            var local = transform.parent != null ? transform.parent.InverseTransformPoint(target) : target;
            var start = transform.localPosition; var t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                var goal = transform.parent != null ? local : target;
                transform.localPosition = Vector3.Lerp(start, goal, Mathf.SmoothStep(0, 1, t / seconds));
                yield return null;
            }
        }

        private void StopRiding(Vector3 at)
        {
            Riding = false;
            transform.SetParent(NPCLifeSimulationManager.Instance != null ? NPCLifeSimulationManager.Instance.transform.parent : null, true);
            transform.position = at;
            transform.rotation = Quaternion.Euler(0, transform.eulerAngles.y, 0);
            Navigation.Enable(true);
        }

        private IEnumerator Shift(LivingPlace place)
        {
            yield return Enter(place);
            var service = place.Service;
            service.Server = this;
            Step = "at the counter";
            while (true)
            {
                var server = service.ServerPoint != null ? service.ServerPoint : service.transform;
                yield return GoTo(server.position, 0.15f, 30, false);
                yield return Face(server.forward, 0.3f);
                var front = service.Front(o => o.Customer != null && (o.Customer.transform.position - service.QueuePosition(0)).sqrMagnitude < 0.6f * 0.6f);
                if (front != null)
                {
                    yield return Serve(service, front);
                    continue;
                }
                // Nothing to serve: keep busy with real tasks.
                var roll = Random.NextDouble();
                if (place.Kind == PlaceKind.Cafe && DirtyCups.Count > 0 && roll < 0.6) yield return CollectCup(place);
                else if (roll < 0.35) yield return Use(place, SpotKind.Counter, NPCAction.Wipe, 6 + Random.Next(4));
                else if (roll < 0.5 && place.Count(SpotKind.Table) > 0) yield return Use(place, SpotKind.Table, NPCAction.Wipe, 5);
                else if (roll < 0.6 && Profile != null && Profile.Personality.Distraction > 0.5f) yield return Act(NPCAction.Phone, 3);
                else yield return Act(NPCAction.None, 1.5f + (float)Random.NextDouble() * 2);
            }
        }

        private IEnumerator Serve(ServicePoint service, ServicePoint.Order order)
        {
            Step = "taking an order";
            var customer = order.Customer;
            var playerName = order.ForPlayer ? "you" : "";
            if (order.ForPlayer)
            {
                var usual = NPCRelationshipMemory.Of(Id).Get("preferredDrink");
                Say(usual != null && usual == order.Item ? "Your usual " + order.Item + " — coming right up!" : "One " + order.Item + ", coming right up!");
            }
            else
            {
                Animation.LookAt(customer.transform.position + Vector3.up * 1.6f);
                Say(Pick(Profile != null ? Profile.Greetings : null, "Hi! What can I get you?") + (service.NeedsPreparation ? " What can I get you?" : ""));
                yield return Act(NPCAction.Talk, 2.2f);
                order.Item = service.Items[Random.Next(service.Items.Length)].Trim();
                customer.Say(service.NeedsPreparation ? "A " + order.Item + ", please." : "Just these, thanks.");
                yield return Act(NPCAction.Listen, 2f);
            }
            service.Take(order);
            Say(service.NeedsPreparation ? "That's $5.50." : "That's $" + (8 + Random.Next(30)) + ".40, please.");
            yield return Act(NPCAction.Pay, 1.8f);
            order.Paid = true;
            if (service.NeedsPreparation)
            {
                Step = "making a " + order.Item;
                var machine = service.PreparePoint != null ? service.PreparePoint : service.transform;
                yield return GoTo(machine.position, 0.12f, 20, false);
                yield return Face(machine.forward, 0.3f);
                yield return Act(NPCAction.OperateMachine, 6 + (float)Random.NextDouble() * 3);
                var pickup = service.PickupPoint != null ? service.PickupPoint : service.transform;
                yield return GoTo(pickup.position - pickup.forward * 1.0f, 0.12f, 20, false);
                yield return Face(pickup.forward, 0.3f);
                order.Product = Props.Make("cup");
                order.Product.transform.position = pickup.position + Vector3.up * 1.02f;
                Say(order.Item.Substring(0, 1).ToUpperInvariant() + order.Item.Substring(1) + (order.ForPlayer && playerName != "" ? " for " + playerName + "!" : " ready!"));
                yield return Act(NPCAction.PickUp, 0.8f);
            }
            order.Ready = true;
            Served++;
            Animation.LookAt(null);
        }

        private static readonly List<GameObject> DirtyCups = new List<GameObject>();

        private IEnumerator CollectCup(LivingPlace place)
        {
            var cup = DirtyCups[0];
            DirtyCups.RemoveAt(0);
            if (cup == null) yield break;
            Step = "collecting a cup";
            yield return GoTo(cup.transform.position, 0.6f, 30, false);
            yield return Act(NPCAction.PickUp, 1);
            Destroy(cup);
            yield return Use(place, SpotKind.Sink, NPCAction.Wipe, 4);
        }

        private IEnumerator Queue(ServicePoint service)
        {
            var order = service.Join(this);
            queued = service;
            Step = "queueing";
            var impatience = 0f;
            while (!order.Taken)
            {
                var index = service.Position(this);
                if (index < 0) break;
                var spot = service.QueuePosition(index);
                if ((transform.position - spot).sqrMagnitude > 0.2f * 0.2f) yield return GoTo(spot, 0.1f, 20, false);
                var server = service.ServerPoint != null ? service.ServerPoint.position : service.transform.position;
                Navigation.Face(server - transform.position, Time.deltaTime);
                impatience += Time.deltaTime * (1.2f - (Profile != null ? Profile.Personality.Patience : 0.5f));
                if (impatience > 12 && Animation.Action == NPCAction.None) { yield return Act(Random.NextDouble() < 0.5 ? NPCAction.Phone : NPCAction.LookAround, 3); impatience = 0; }
                yield return null;
            }
            yield return Act(NPCAction.Pay, 1.6f);
            queued = null;
            return_order = order;
        }

        private ServicePoint.Order return_order;

        private IEnumerator CafeCustomer(LivingPlace cafe, bool eat)
        {
            if (cafe == null || cafe.Service == null) { yield return Wander(cafe); yield break; }
            if (Place != null && Place != cafe) yield return Leave(Place);
            yield return Enter(cafe);
            yield return Queue(cafe.Service);
            var order = return_order;
            if (order == null) yield break;
            Step = "waiting for the order";
            var pickup = cafe.Service.PickupPoint != null ? cafe.Service.PickupPoint : cafe.Service.transform;
            yield return GoTo(pickup.position + pickup.forward * 0.9f + pickup.right * ((float)Random.NextDouble() - 0.5f), 0.2f, 20, false);
            var t = 0f;
            while (!order.Ready && t < 90) { t += Time.deltaTime; if (Animation.Action == NPCAction.None && Random.NextDouble() < 0.01) yield return Act(NPCAction.Phone, 4); yield return null; }
            if (order.Product != null)
            {
                yield return GoTo(pickup.position + pickup.forward * 0.45f, 0.1f, 10, false);
                yield return Act(NPCAction.PickUp, 0.8f);
                Destroy(order.Product);
                Say("Thanks!");
            }
            cafe.Service.Finish(order);
            // Find a seat: next to a friend if one is sitting here, else any free seat.
            var group = FriendGroup(cafe);
            var seat = cafe.Reserve(SpotKind.Seat, this, transform.position, group) ?? cafe.Reserve(SpotKind.Seat, this, transform.position);
            if (seat != null)
            {
                held.Add(seat);
                yield return GoTo(seat.transform.position, 0.1f, 40, false);
                yield return Face(seat.transform.forward, 0.4f);
                var stay = (eat ? 70 : 50) + Random.Next(60);
                var work = Profile != null && (Profile.Occupation.ToLowerInvariant().Contains("student") || Profile.Occupation.ToLowerInvariant().Contains("designer")) && Random.NextDouble() < 0.6;
                var friend = FriendSitting(cafe, seat.Group);
                var elapsed = 0f;
                while (elapsed < stay)
                {
                    NPCAction action;
                    if (friend != null && Random.NextDouble() < 0.5) { action = Random.NextDouble() < 0.5 ? NPCAction.Talk : NPCAction.Listen; if (action == NPCAction.Talk) Say(Pick(Profile.SmallTalk, "How's your week going?"), 3); }
                    else if (work) action = NPCAction.Type;
                    else action = eat && elapsed < stay * 0.5f ? NPCAction.Eat : Random.NextDouble() < 0.7 ? NPCAction.Drink : NPCAction.Phone;
                    Animation.Play(action, seat.SeatHeight);
                    var chunk = 6 + (float)Random.NextDouble() * 8;
                    yield return Wait(chunk);
                    elapsed += chunk;
                }
                Needs.Satisfy("Hunger", eat ? 0.6f : 0.1f);
                Needs.Satisfy("Social", friend != null ? 0.5f : 0.1f);
                Animation.Play(NPCAction.None);
                // Leave the cup on the table: the barista collects it.
                var cup = Props.Make("cup");
                cup.transform.position = seat.transform.position + seat.transform.forward * 0.4f + Vector3.up * (seat.SeatHeight + 0.3f);
                DirtyCups.Add(cup);
                seat.Release(this); held.Remove(seat);
            }
            Step = "leaving";
            yield return Leave(cafe);
            if (Goal.Activity == Activity) yield return Wander(null);
        }

        private string FriendGroup(LivingPlace place)
        {
            if (Profile == null) return null;
            foreach (var spot in place.Spots)
                if (spot != null && spot.Kind == SpotKind.Seat && spot.Occupant != null && Profile.IsFriend(spot.Occupant.Id) && !string.IsNullOrEmpty(spot.Group))
                {
                    foreach (var other in place.Spots) if (other != null && other.Group == spot.Group && other.Free(this)) return spot.Group;
                }
            return null;
        }

        private LivingNPC FriendSitting(LivingPlace place, string group)
        {
            if (Profile == null || string.IsNullOrEmpty(group)) return null;
            foreach (var spot in place.Spots)
                if (spot != null && spot.Group == group && spot.Occupant != null && spot.Occupant != this && Profile.IsFriend(spot.Occupant.Id)) return spot.Occupant;
            return null;
        }

        private IEnumerator Break(LivingPlace place)
        {
            if (place == null) { yield return Rest(); yield break; }
            yield return Enter(place);
            while (true)
            {
                yield return Use(place, SpotKind.Seat, Random.NextDouble() < 0.5 ? NPCAction.Eat : NPCAction.Phone, 30 + Random.Next(30));
                Needs.Satisfy("Hunger", 0.4f); Needs.Satisfy("Fatigue", 0.2f);
            }
        }

        private IEnumerator LeaveWork()
        {
            if (Place != null) yield return Leave(Place);
            if (Needs.Hunger > 0.5f || Random.NextDouble() < 0.4) yield return Shop(Resolve("supermarket"));
            yield return Wander(Resolve("home"));
        }

        private IEnumerator Socialize(LivingPlace place)
        {
            if (place == null) place = Resolve("café");
            if (place != null && place.Service != null) { yield return CafeCustomer(place, eat: Needs.Hunger > 0.4f); yield break; }
            yield return Explore(place);
        }

        private IEnumerator Shop(LivingPlace shop)
        {
            if (shop == null) yield break;
            if (Place != null && Place != shop) yield return Leave(Place);
            yield return Enter(shop);
            var items = 2 + Random.Next(3);
            for (var i = 0; i < items; i++)
            {
                yield return Use(shop, SpotKind.Shelf, NPCAction.Browse, 5 + Random.Next(6));
                yield return Act(NPCAction.PickUp, 1.1f);
            }
            if (shop.Service != null)
            {
                yield return Queue(shop.Service);
                var order = return_order;
                var t = 0f;
                while (order != null && !order.Ready && t < 30) { t += Time.deltaTime; yield return null; }
                if (order != null) shop.Service.Finish(order);
            }
            Animation.Carry("bag", true);
            Step = "leaving with shopping";
            yield return Leave(shop);
            Needs.Satisfy("Hunger", 0.2f);
        }

        private IEnumerator School(LivingPlace school)
        {
            if (school == null) yield break;
            if (Place != null && Place != school) yield return Leave(Place);
            // Walk with a friend when one is going the same way.
            var partner = Profile != null && Profile.WalkWith != "" ? All.Find(n => n != null && n.Id == Profile.WalkWith) : null;
            if (partner != null && partner.Goal.Activity == Goal.Activity && Vector3.Distance(partner.transform.position, transform.position) < 25 && string.CompareOrdinal(Id, partner.Id) > 0)
            {
                Step = "walking to school with " + partner.DisplayName;
                Interruptible = false;
                var t = 0f; var chat = 4f;
                while (Vector3.Distance(transform.position, school.Door) > 2.5f && t < 180)
                {
                    t += Time.deltaTime;
                    var beside = partner.transform.position + partner.transform.right * 0.8f;
                    if ((Navigation.Destination - beside).sqrMagnitude > 0.25f || !Navigation.HasDestination) Navigation.Go(beside, 0.3f);
                    if ((chat -= Time.deltaTime) <= 0) { chat = 6 + (float)Random.NextDouble() * 6; Say(Pick(Profile.SmallTalk, "Did you finish the homework?"), 2.8f); }
                    yield return null;
                }
            }
            yield return Enter(school);
            yield return Stay(school, NPCAction.Type);
        }

        private IEnumerator Explore(LivingPlace around)
        {
            while (true)
            {
                var target = around != null ? around.RandomPoint(Random) : RandomStreetPoint();
                yield return GoTo(target, 0.6f);
                var roll = Random.NextDouble();
                if (roll < 0.35) yield return Act(NPCAction.Map, 5 + Random.Next(4));
                else if (roll < 0.6) yield return Act(NPCAction.Photo, 3);
                else if (roll < 0.75) yield return Act(NPCAction.LookAround, 4);
                Needs.Satisfy("Recreation", 0.1f);
            }
        }

        private Vector3 RandomStreetPoint()
        {
            var streets = LivingPlace.All.FindAll(p => p != null && (p.Kind == PlaceKind.Street || p.Kind == PlaceKind.Park));
            if (streets.Count == 0) return transform.position + new Vector3((float)Random.NextDouble() * 10 - 5, 0, (float)Random.NextDouble() * 10 - 5);
            return streets[Random.Next(streets.Count)].RandomPoint(Random);
        }

        /// Errands with a purpose: head for a real place (shop, café, stop, park), look at the phone on the way.
        private IEnumerator Wander(LivingPlace destination)
        {
            var legs = 0;
            while (true)
            {
                Vector3 target;
                if (destination != null && legs == 0) target = destination.Kind == PlaceKind.Home ? destination.Door : destination.RandomPoint(Random);
                else target = RandomStreetPoint();
                Step = "on an errand";
                if (Profile != null && Random.NextDouble() < Profile.Personality.Distraction * 0.5f) Animation.Play(NPCAction.Phone);
                yield return GoTo(target, 0.6f);
                Animation.Play(NPCAction.None);
                if (destination != null && destination.Kind == PlaceKind.Home && legs == 0) { yield return HomeLife(destination, relax: true); yield break; }
                var roll = Random.NextDouble();
                if (roll < 0.3) yield return Act(NPCAction.LookAround, 3);
                else if (roll < 0.45) yield return Act(NPCAction.Phone, 4);
                else if (roll < 0.55) yield return Rest();
                legs++;
            }
        }

        private IEnumerator Shelter()
        {
            Step = "sheltering from the rain";
            if (Random.NextDouble() < 0.5) Animation.Carry("umbrella", true);
            LivingPlace best = null; var d = float.MaxValue;
            foreach (var p in LivingPlace.All)
                if (p != null && (p.Shelter || p.Indoor && p.Kind != PlaceKind.Home)) { var dd = (p.Door - transform.position).sqrMagnitude; if (dd < d) { d = dd; best = p; } }
            if (best == null) { while (true) yield return Act(NPCAction.LookAround, 4); }
            Navigation.Jogging = true;
            yield return Enter(best);
            Navigation.Jogging = false;
            Animation.Carry("umbrella", false);
            while (true) yield return Use(best, best.Count(SpotKind.Bench) > 0 ? SpotKind.Bench : best.Count(SpotKind.Seat) > 0 ? SpotKind.Seat : SpotKind.Wait, NPCAction.Phone, 20);
        }

        private IEnumerator Rest()
        {
            LivingPlace best = null; var d = float.MaxValue;
            foreach (var p in LivingPlace.All)
                if (p != null && p.Count(SpotKind.Bench) > 0) { var dd = (p.transform.position - transform.position).sqrMagnitude; if (dd < d) { d = dd; best = p; } }
            if (best == null) { yield return Act(NPCAction.Stretch, 3); yield break; }
            yield return Use(best, SpotKind.Bench, Random.NextDouble() < 0.5 ? NPCAction.Phone : NPCAction.Sit, 20 + Random.Next(25));
            Needs.Satisfy("Fatigue", 0.3f);
        }

        // ------------------------------------------------------------------ social

        /// Greet a friend; maybe stop for a chat. Both run this (initiator first).
        public IEnumerator Greet(LivingNPC other, bool initiator, bool chat)
        {
            InConversation = true;
            Step = "talking with " + other.DisplayName;
            Navigation.Stop();
            Animation.LookAt(other.transform.position + Vector3.up * 1.6f);
            var t = 0f;
            while (t < 0.6f) { t += Time.deltaTime; Navigation.Face(other.transform.position - transform.position, Time.deltaTime); yield return null; }
            if (initiator) { Say(Pick(Profile != null ? Profile.Greetings : null, "Hi!") + " " + other.DisplayName.Split(' ')[0] + "!"); yield return Act(NPCAction.Wave, 1.6f); }
            else { yield return Wait(0.9f); Say(Pick(Profile != null ? Profile.Greetings : null, "Hey!")); yield return Act(NPCAction.Wave, 1.4f); }
            if (chat)
            {
                var turns = 2 + Random.Next(3);
                for (var i = 0; i < turns; i++)
                {
                    var speaking = (i % 2 == 0) == initiator;
                    if (speaking) Say(Pick(Profile != null ? Profile.SmallTalk : null, "How have you been?"), 3);
                    Animation.Play(speaking ? NPCAction.Talk : (Random.NextDouble() < 0.3 ? NPCAction.Nod : NPCAction.Listen));
                    var turn = 3.2f;
                    t = 0; while (t < turn) { t += Time.deltaTime; Navigation.Face(other.transform.position - transform.position, Time.deltaTime); yield return null; }
                }
                Animation.Play(NPCAction.None);
                if (initiator) Say("See you later!"); else { yield return Wait(0.8f); Say("Bye!"); }
                Needs.Satisfy("Social", 0.3f);
            }
            yield return Wait(1f);
            Animation.LookAt(null);
            InConversation = false;
        }

        public static string Pick(string[] lines, string fallback)
        {
            if (lines == null || lines.Length == 0) return fallback;
            return lines[UnityEngine.Random.Range(0, lines.Length)];
        }

        // ------------------------------------------------------------------ debug / tools

        public void SendTo(string where)
        {
            Brain.Reset();
            Begin(new NPCGoal { Activity = where == "work" ? (Profile != null && Profile.Occupation.ToLowerInvariant().Contains("student") ? "School" : "Work") : "Relax", Location = where == "work" ? (Profile != null ? Profile.Work : "") : "home", Score = 1, Reason = "debug" });
        }

        public void Restore(NPCSavedState state)
        {
            Needs.Hunger = state.Hunger; Needs.Fatigue = state.Fatigue; Needs.Social = state.Social; Needs.Comfort = state.Comfort; Needs.Recreation = state.Recreation;
            var p = new Vector3(state.X, state.Y, state.Z);
            Navigation.Enable(true);
            if (Navigation.OnMesh) Navigation.Agent.Warp(p); else transform.position = p;
        }

        public NPCSavedState Capture(string scene) => new NPCSavedState
        {
            Npc = Id, Scene = scene, Activity = Activity, Place = Place != null ? Place.Id : "",
            X = transform.position.x, Y = transform.position.y, Z = transform.position.z,
            Hunger = Needs.Hunger, Fatigue = Needs.Fatigue, Social = Needs.Social, Comfort = Needs.Comfort, Recreation = Needs.Recreation,
        };
    }
}
